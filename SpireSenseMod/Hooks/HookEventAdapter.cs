using System.Collections.Generic;

namespace SpireSenseMod.Hooks;

/// <summary>
/// Translates extracted game data from Hook subscriptions into GameStateTracker mutations.
/// Testable: depends only on GameStateTracker (no Godot/Harmony/reflection dependencies).
/// </summary>
public class HookEventAdapter
{
    private readonly GameStateTracker _tracker;

    public HookEventAdapter(GameStateTracker tracker)
    {
        _tracker = tracker;
    }

    /// <summary>
    /// Called by OnAfterPlayerTurnStart hook.
    /// Updates combat state with current turn data: player, hand, piles, monsters.
    /// </summary>
    public void HandleTurnStart(
        int turn,
        PlayerState player,
        List<CardInfo> hand,
        List<CardInfo> drawPile,
        List<CardInfo> discardPile,
        List<CardInfo> exhaustPile,
        List<MonsterInfo> monsters)
    {
        _tracker.UpdateState(state =>
        {
            if (state.Combat == null) return;
            state.Combat.Turn = turn;
            state.Combat.Player = player;
            state.Combat.Hand = hand;
            state.Combat.DrawPile = drawPile;
            state.Combat.DiscardPile = discardPile;
            state.Combat.ExhaustPile = exhaustPile;
            state.Combat.Monsters = monsters;
        });
    }

    /// <summary>
    /// Called by OnAfterCardPlayed hook.
    /// Emits a card_played event with the card info and optional target name.
    /// </summary>
    public void HandleCardPlayed(CardInfo card, string targetName)
    {
        _tracker.EmitEvent(new GameEvent
        {
            Type = "card_played",
            Data = new { card, target = targetName },
        });
    }

    /// <summary>
    /// Called by OnBeforeCombatStart hook (Wave 4).
    /// Initializes combat state with monsters and player, sets screen to Combat.
    /// </summary>
    public void HandleCombatStart(List<MonsterInfo> monsters, PlayerState player)
    {
        var combatState = new CombatState
        {
            Turn = 1,
            Player = player,
            Monsters = monsters,
        };
        _tracker.SetCombatState(combatState);
        _tracker.SetScreen(ScreenType.Combat);
        _tracker.EmitEvent(new GameEvent
        {
            Type = "combat_start",
            Data = new { monsters },
        });
    }

    /// <summary>
    /// Called by OnAfterCombatEnd hook (Wave 4).
    /// Clears combat state and sets the appropriate screen based on outcome.
    /// </summary>
    public void HandleCombatEnd(bool won, bool isBoss, int floor)
    {
        _tracker.SetCombatState(null);
        if (won && isBoss)
            _tracker.SetScreen(ScreenType.BossReward);
        else if (!won)
            _tracker.SetScreen(ScreenType.GameOver);
        _tracker.EmitEvent(new GameEvent
        {
            Type = "combat_end",
            Data = new { won, isBoss, floor },
        });
    }

    /// <summary>
    /// Called when a card is added to the player's deck (e.g., card reward pick, shop purchase, event).
    /// Appends the card to the deck and emits a deck_changed event.
    /// </summary>
    public void HandleCardAddedToDeck(CardInfo card)
    {
        _tracker.UpdateState(state =>
        {
            state.Deck.Add(card);
        });
        _tracker.EmitEvent(new GameEvent
        {
            Type = "deck_changed",
            Data = new { action = "added", card },
        });
    }

    /// <summary>
    /// Called when a card is removed from the player's deck (e.g., shop removal, event).
    /// Removes the first matching card by ID and emits a card_removed event.
    /// </summary>
    public void HandleCardRemovedFromDeck(CardInfo card)
    {
        _tracker.UpdateState(state =>
        {
            var index = state.Deck.FindIndex(c => c.Id == card.Id);
            if (index >= 0)
                state.Deck.RemoveAt(index);
        });
        _tracker.EmitEvent(new GameEvent
        {
            Type = "card_removed",
            Data = new { card },
        });
    }

    /// <summary>
    /// Called when the player enters a shop.
    /// Sets screen to Shop and clears previous shop data. Shop items are populated
    /// separately via state update once the shop inventory is extracted.
    /// </summary>
    public void HandleShopEntered()
    {
        _tracker.SetScreen(ScreenType.Shop);
        _tracker.EmitEvent(new GameEvent
        {
            Type = "floor_changed",
            Data = new { screen = ScreenType.Shop },
        });
    }

    /// <summary>
    /// Called when the player enters a rest site.
    /// Sets screen to Rest, stores the available rest options, and emits a rest_entered event.
    /// </summary>
    public void HandleRestEntered(List<RestOption> options)
    {
        _tracker.UpdateState(state =>
        {
            state.Screen = ScreenType.Rest;
            state.RestOptions = options;
        });
        _tracker.EmitEvent(new GameEvent
        {
            Type = "rest_entered",
            Data = new { options },
        });
    }
}
