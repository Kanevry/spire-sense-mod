using System;
using System.Collections.Generic;

namespace SpireSenseMod;

/// <summary>
/// Central game state tracker. Updated by Harmony patches, read by HTTP/WebSocket servers.
/// Thread-safe via locking.
/// </summary>
public class GameStateTracker
{
    private readonly object _lock = new();
    private GameState _currentState = new();

    public event Action<GameEvent>? OnGameEvent;

    public GameState GetCurrentState()
    {
        lock (_lock)
        {
            return _currentState;
        }
    }

    public void UpdateState(Action<GameState> updater)
    {
        lock (_lock)
        {
            updater(_currentState);
        }
    }

    public void SetState(GameState state)
    {
        lock (_lock)
        {
            _currentState = state;
        }
        EmitEvent(new GameEvent { Type = "state_update", Data = state });
    }

    public void EmitEvent(GameEvent gameEvent)
    {
        OnGameEvent?.Invoke(gameEvent);
    }

    public void SetScreen(string screen)
    {
        lock (_lock)
        {
            _currentState.Screen = screen;
        }
    }

    public void SetCombatState(CombatState? combat)
    {
        lock (_lock)
        {
            _currentState.Combat = combat;
        }
    }

    public void SetCardRewards(List<CardInfo>? rewards)
    {
        lock (_lock)
        {
            _currentState.CardRewards = rewards;
        }
    }
}
