using System;
using System.Collections.Generic;
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
        OnGameEvent?.Invoke(gameEvent);
    }

    public void SetScreen(string screen)
    {
        lock (_lock)
        {
            _currentState.Screen = screen;
            _serializedState = JsonSerializer.Serialize(_currentState, _jsonOptions);
        }
    }

    public void SetCombatState(CombatState? combat)
    {
        lock (_lock)
        {
            _currentState.Combat = combat;
            _serializedState = JsonSerializer.Serialize(_currentState, _jsonOptions);
        }
    }

    public void SetCardRewards(List<CardInfo>? rewards)
    {
        lock (_lock)
        {
            _currentState.CardRewards = rewards;
            _serializedState = JsonSerializer.Serialize(_currentState, _jsonOptions);
        }
    }
}
