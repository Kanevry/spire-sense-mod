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

    /// <summary>
    /// Called when a relic is obtained.
    /// Appends the relic to the relic list and emits a relic_obtained event.
    ///
    /// Note: This is called from DeckPatch (Harmony), not a Hook subscription,
    /// because the STS2 Hook system does not expose an AfterRelicObtained hook.
    /// RelicCmd.Obtain is a static command with no Hook equivalent.
    /// See DeckPatch class documentation for full rationale (GitLab #126).
    /// </summary>
    public void HandleRelicObtained(RelicInfo relic)
    {
        _tracker.UpdateState(state =>
        {
            state.Relics.Add(relic);
        });
        _tracker.EmitEvent(new GameEvent
        {
            Type = "relic_obtained",
            Data = new { relic },
        });
    }

    /// <summary>
    /// Called when a new run starts.
    /// Replaces the entire game state with the initial run state and emits a run_start event.
    ///
    /// Note: This is called from DeckPatch (Harmony), not a Hook subscription,
    /// because the STS2 Hook system does not expose an AfterRunStart or BeforeRunStart hook.
    /// RunManager.Launch() is a lifecycle method with no Hook equivalent.
    /// See DeckPatch class documentation for full rationale (GitLab #126).
    /// </summary>
    public void HandleRunStart(GameState initialState)
    {
        _tracker.SetState(initialState);
        _tracker.EmitEvent(new GameEvent
        {
            Type = "run_start",
            Data = new { character = initialState.Character, ascension = initialState.Ascension, seed = initialState.Seed },
        });
    }

    /// <summary>
    /// Called when the current run ends (victory or defeat).
    /// Sets the screen to Victory or GameOver and emits a run_end event.
    ///
    /// Note: This is called from DeckPatch (Harmony), not a Hook subscription,
    /// because the STS2 Hook system does not expose an AfterRunEnd or BeforeRunEnd hook.
    /// RunManager.OnEnded(bool) is a lifecycle method with no Hook equivalent.
    /// See DeckPatch class documentation for full rationale (GitLab #126).
    /// </summary>
    public void HandleRunEnd(bool won, int floor, int score)
    {
        _tracker.SetScreen(won ? ScreenType.Victory : ScreenType.GameOver);
        _tracker.EmitEvent(new GameEvent
        {
            Type = "run_end",
            Data = new { won, floor, score },
        });
    }

    /// <summary>
    /// Called when the player selects a rest site option (heal, smith, dig, etc.).
    /// Emits a rest_choice event and clears the stored rest options.
    ///
    /// Note: This is called from RestPatch (Harmony), not a Hook subscription,
    /// because the STS2 Hook system does not expose an AfterRestChoiceSelected hook.
    /// NRestSiteButton.SelectOption is a UI button handler with no Hook equivalent.
    /// AfterRoomEntered detects the rest site and extracts options, but the choice
    /// selection is a separate user action that only Harmony can intercept.
    /// See RestPatch class documentation for full rationale (GitLab #126).
    /// </summary>
    public void HandleRestChoice(string choiceId, string choiceName)
    {
        _tracker.EmitEvent(new GameEvent
        {
            Type = "rest_choice",
            Data = new { choiceId, choiceName },
        });
        _tracker.UpdateState(state =>
        {
            state.RestOptions = null;
        });
    }

    /// <summary>
    /// Called when the player exits the shop (MerchantRoom).
    /// Clears shop data and resets the screen to Map.
    ///
    /// Note: This is called from ShopPatch (Harmony), not a Hook subscription,
    /// because the STS2 Hook system does not expose an AfterShopExited hook.
    /// MerchantRoom.Exit is a room lifecycle method with no Hook equivalent.
    /// AfterRoomEntered detects shop entry, but shop exit is a separate game action.
    /// See ShopPatch class documentation for full rationale (GitLab #126).
    /// </summary>
    public void HandleShopExited()
    {
        _tracker.UpdateState(state =>
        {
            state.Screen = ScreenType.Map;
            state.ShopCards = null;
            state.ShopRelics = null;
        });
    }

    /// <summary>
    /// Called when the card reward selection screen is shown (3 cards offered).
    /// Stores the offered cards, sets screen to CardReward, emits card_rewards_shown event.
    ///
    /// Note: This is called from CardRewardPatch (Harmony), not a Hook subscription,
    /// because no STS2 Hook equivalent exists for NCardRewardSelectionScreen.ShowScreen.
    /// See CardRewardPatch class documentation for full rationale (GitLab #126).
    /// </summary>
    public void HandleCardRewardsShown(List<CardInfo> cards)
    {
        _tracker.SetCardRewards(cards);
        _tracker.SetScreen(ScreenType.CardReward);
        _tracker.EmitEvent(new GameEvent
        {
            Type = "card_rewards_shown",
            Data = new { cards },
        });
    }

    /// <summary>
    /// Called when the player picks a card from the reward selection screen.
    /// Emits a card_picked event with the chosen card and the alternatives that were offered,
    /// then clears the stored card rewards.
    ///
    /// Note: This is called from CardRewardPatch (Harmony), not a Hook subscription,
    /// because no STS2 Hook equivalent exists for NCardRewardSelectionScreen.SelectCard.
    /// The deck addition is handled separately by AfterCardChangedPiles (deck_changed event).
    /// See CardRewardPatch class documentation for full rationale (GitLab #126).
    /// </summary>
    public void HandleCardPicked(CardInfo card, List<CardInfo> alternatives)
    {
        _tracker.EmitEvent(new GameEvent
        {
            Type = "card_picked",
            Data = new { card, alternatives },
        });
        _tracker.SetCardRewards(null);
    }

    /// <summary>
    /// Called when an in-game narrative event encounter starts.
    /// Sets screen to Event, stores event options, and emits an event_started event.
    ///
    /// Note: This is called from EventPatch (Harmony), not a Hook subscription,
    /// because the STS2 Hook system does not expose an AfterEventStarted or BeforeEventStarted hook.
    /// AfterRoomEntered fires before EventModel.BeginEvent() populates the event options,
    /// so the Harmony patch on BeginEvent is required to capture the full event data.
    /// See EventPatch class documentation for full rationale (GitLab #126).
    /// </summary>
    public void HandleEventStarted(string name, string description, List<EventOption> options)
    {
        _tracker.UpdateState(state =>
        {
            state.Screen = ScreenType.Event;
            state.EventOptions = options;
        });
        _tracker.EmitEvent(new GameEvent
        {
            Type = "event_started",
            Data = new { name, description, options },
        });
    }

    /// <summary>
    /// Called when the player selects an event option.
    /// Emits an event_choice event and clears the stored event options.
    ///
    /// Note: This is called from EventPatch (Harmony), not a Hook subscription,
    /// because the STS2 Hook system does not expose an AfterEventOptionSelected hook.
    /// NEventOptionButton.OnRelease() is a UI button press handler with no Hook equivalent.
    /// See EventPatch class documentation for full rationale (GitLab #126).
    /// </summary>
    public void HandleEventChoice(string choiceId, string choiceName)
    {
        _tracker.EmitEvent(new GameEvent
        {
            Type = "event_choice",
            Data = new { choiceId, choiceName },
        });
        _tracker.UpdateState(state =>
        {
            state.EventOptions = null;
        });
    }
}
