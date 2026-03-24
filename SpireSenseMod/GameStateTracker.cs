using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SpireSenseMod;

/// <summary>
/// Central game state tracker. Updated by Harmony patches, read by HTTP/WebSocket servers.
/// Thread-safe via locking + immutable serialized snapshots.
/// </summary>
public class GameStateTracker
{
    private readonly object _lock = new();
    private GameState _currentState = new();
    private string _serializedState = "{}";

    private readonly object _eventLock = new();
    private readonly Queue<BufferedEvent> _eventBuffer = new();
    private const int MaxEventBufferSize = 100;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public event Action<GameEvent>? OnGameEvent;

    /// <summary>
    /// Returns a deep copy of the current state. Prefer <see cref="GetSerializedState"/> when
    /// you only need the JSON string (avoids deserialization overhead).
    /// </summary>
    public GameState GetCurrentState()
    {
        return JsonSerializer.Deserialize<GameState>(GetSerializedState(), _jsonOptions)!;
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
}
