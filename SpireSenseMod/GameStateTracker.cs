using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SpireSenseMod;

/// <summary>
/// Central game state tracker. Updated by Harmony patches, read by HTTP/WebSocket servers.
/// Thread-safe via locking + immutable serialized snapshots.
///
/// <para><b>Thread-safety model:</b></para>
/// <list type="bullet">
///   <item><description>
///     <c>_lock</c> guards <c>_currentState</c> and <c>_serializedState</c>. All state
///     mutations (<see cref="UpdateState"/>, <see cref="SetState"/>, <see cref="SetScreen"/>,
///     <see cref="SetCombatState"/>, <see cref="SetCardRewards"/>) acquire this lock, mutate
///     the state, serialize a snapshot, then release the lock before emitting events.
///   </description></item>
///   <item><description>
///     <c>_eventLock</c> guards the <c>_eventBuffer</c> ring buffer independently, so event
///     reads (<see cref="GetEventsSince"/>) never block state mutations and vice versa.
///   </description></item>
/// </list>
///
/// <para><b>Reentrancy contract for <see cref="OnGameEvent"/> subscribers:</b></para>
/// <para>
///   <see cref="OnGameEvent"/> is invoked <b>outside</b> <c>_lock</c> to prevent deadlocks.
///   This is intentional — holding the lock during event dispatch would deadlock if any
///   subscriber attempted to read or write state (e.g., <see cref="GetSerializedState"/>
///   also acquires <c>_lock</c>).
/// </para>
/// <para>
///   However, this means subscribers <b>MUST NOT</b> call back into any state-mutating
///   method from their event handler. Specifically, do not call <see cref="UpdateState"/>,
///   <see cref="SetState"/>, <see cref="SetScreen"/>, <see cref="SetCombatState"/>, or
///   <see cref="SetCardRewards"/> from within an <see cref="OnGameEvent"/> handler.
///   Doing so would cause recursive event emission (state change -> emit -> handler ->
///   state change -> emit -> ...) with unbounded stack growth.
/// </para>
/// <para>
///   <b>Safe operations inside handlers:</b> <see cref="GetSerializedState"/>,
///   <see cref="GetCurrentState"/>, <see cref="GetEventsSince"/> (read-only), and
///   <see cref="AddEvent"/> (uses separate <c>_eventLock</c>).
/// </para>
/// <para>
///   <b>Current subscribers:</b> <c>WebSocketServer.EnqueueEvent</c> — enqueues to a
///   <c>ConcurrentQueue</c> without calling back into the tracker. This is the correct pattern.
/// </para>
/// </summary>
public class GameStateTracker
{
    /// <summary>Guards <see cref="_currentState"/> and <see cref="_serializedState"/>.</summary>
    private readonly object _lock = new();
    private GameState _currentState = new();
    private string _serializedState = "{}";

    /// <summary>Guards <see cref="_eventBuffer"/> independently from state mutations.</summary>
    private readonly object _eventLock = new();
    private readonly Queue<BufferedEvent> _eventBuffer = new();
    private const int MaxEventBufferSize = 100;

    // Combat polling removed — Godot objects are not thread-safe.
    // Live updates handled by client-side HTTP polling instead.

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Raised after every state mutation and explicit <see cref="EmitEvent"/> call.
    /// <para>
    ///   <b>WARNING:</b> Invoked outside <c>_lock</c>. Subscribers MUST NOT call
    ///   <see cref="UpdateState"/>, <see cref="SetState"/>, <see cref="SetScreen"/>,
    ///   <see cref="SetCombatState"/>, or <see cref="SetCardRewards"/> from their handler.
    ///   See the class-level documentation for the full reentrancy contract.
    /// </para>
    /// </summary>
    public event Action<GameEvent>? OnGameEvent;

    /// <summary>
    /// Returns a deep copy of the current state. Prefer <see cref="GetSerializedState"/> when
    /// you only need the JSON string (avoids deserialization overhead).
    /// </summary>
    public GameState GetCurrentState()
    {
        var json = GetSerializedState();
        var state = JsonSerializer.Deserialize<GameState>(json, _jsonOptions);
        if (state is null)
        {
            throw new InvalidOperationException(
                $"[SpireSense] Failed to deserialize GameState from snapshot (length={json.Length}). This indicates corrupted internal state.");
        }
        return state;
    }

    /// <summary>
    /// Returns the pre-serialized JSON snapshot of the current state.
    /// Thread-safe — the string is captured under the lock during mutation.
    /// </summary>
    public string GetSerializedState()
    {
        lock (_lock)
        {
            return _serializedState;
        }
    }

    public void UpdateState(Action<GameState> updater)
    {
        string snapshot;
        lock (_lock)
        {
            updater(_currentState);
            _serializedState = JsonSerializer.Serialize(_currentState, _jsonOptions);
            snapshot = _serializedState;
        }
        EmitEvent(new GameEvent { Type = "state_update" }, snapshot);
    }

    public void SetState(GameState state)
    {
        string snapshot;
        lock (_lock)
        {
            _currentState = state;
            _serializedState = JsonSerializer.Serialize(_currentState, _jsonOptions);
            snapshot = _serializedState;
        }
        EmitEvent(new GameEvent { Type = "state_update" }, snapshot);
    }

    /// <summary>
    /// Emit a game event to all <see cref="OnGameEvent"/> subscribers.
    /// Non-<c>state_update</c> events are also added to the ring buffer.
    ///
    /// <para>
    ///   Called by state-mutating methods <b>after</b> releasing <c>_lock</c>, so the
    ///   serialized snapshot is already consistent when subscribers receive it.
    ///   See the class-level reentrancy contract for subscriber obligations.
    /// </para>
    /// </summary>
    public void EmitEvent(GameEvent gameEvent, string? serializedData = null)
    {
        if (serializedData != null)
        {
            gameEvent.SerializedData = serializedData;
        }

        // Automatically buffer all emitted events (except internal state_update noise)
        if (gameEvent.Type != "state_update")
        {
            AddEvent(gameEvent.Type, gameEvent.Data);
        }

        OnGameEvent?.Invoke(gameEvent);
    }

    /// <summary>
    /// Add an event to the ring buffer. Thread-safe via dedicated lock.
    /// Evicts the oldest entry when the buffer exceeds <see cref="MaxEventBufferSize"/>.
    /// </summary>
    public void AddEvent(string type, object? data)
    {
        var entry = new BufferedEvent
        {
            Type = type,
            Data = data,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        lock (_eventLock)
        {
            if (_eventBuffer.Count >= MaxEventBufferSize)
            {
                _eventBuffer.Dequeue();
            }
            _eventBuffer.Enqueue(entry);
        }
    }

    /// <summary>
    /// Return all buffered events with a timestamp strictly greater than <paramref name="sinceTimestamp"/>.
    /// Returns a snapshot list — safe to iterate without holding the lock.
    /// </summary>
    public List<BufferedEvent> GetEventsSince(long sinceTimestamp)
    {
        lock (_eventLock)
        {
            return _eventBuffer.Where(e => e.Timestamp > sinceTimestamp).ToList();
        }
    }

    public void SetScreen(string screen)
    {
        string snapshot;
        lock (_lock)
        {
            _currentState.Screen = screen;
            _serializedState = JsonSerializer.Serialize(_currentState, _jsonOptions);
            snapshot = _serializedState;
        }
        EmitEvent(new GameEvent { Type = "state_update" }, snapshot);
    }

    public void SetCombatState(CombatState? combat)
    {
        string snapshot;
        lock (_lock)
        {
            _currentState.Combat = combat;
            _serializedState = JsonSerializer.Serialize(_currentState, _jsonOptions);
            snapshot = _serializedState;
        }
        EmitEvent(new GameEvent { Type = "state_update" }, snapshot);
    }

    public void SetCardRewards(List<CardInfo>? rewards)
    {
        string snapshot;
        lock (_lock)
        {
            _currentState.CardRewards = rewards;
            _serializedState = JsonSerializer.Serialize(_currentState, _jsonOptions);
            snapshot = _serializedState;
        }
        EmitEvent(new GameEvent { Type = "state_update" }, snapshot);
    }

    // StartCombatPolling/StopCombatPolling removed — Godot objects are not thread-safe.
    // Client-side HTTP polling provides live combat updates instead.
}
