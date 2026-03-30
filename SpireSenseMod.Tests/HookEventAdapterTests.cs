using System.Text.Json;
using SpireSenseMod;
using SpireSenseMod.Hooks;
using Xunit;

namespace SpireSenseMod.Tests;

public class HookEventAdapterTests
{
    private GameStateTracker CreateTracker() => new();

    private HookEventAdapter CreateAdapter(GameStateTracker tracker) => new(tracker);

    private PlayerState CreatePlayer(
        int hp = 70, int maxHp = 70, int block = 0,
        int energy = 3, int maxEnergy = 3, int gold = 100)
        => new() { Hp = hp, MaxHp = maxHp, Block = block, Energy = energy, MaxEnergy = maxEnergy, Gold = gold };

    private CardInfo CreateCard(string id = "strike", string name = "Strike")
        => new() { Id = id, Name = name };

    private MonsterInfo CreateMonster(
        string id = "nibbit", string name = "Nibbit",
        int hp = 45, int maxHp = 45)
        => new() { Id = id, Name = name, Hp = hp, MaxHp = maxHp };

    /// <summary>
    /// Sets up a tracker with an active combat state so HandleTurnStart can operate on it.
    /// </summary>
    private (GameStateTracker tracker, HookEventAdapter adapter) CreateWithCombat()
    {
        var tracker = CreateTracker();
        tracker.SetCombatState(new CombatState
        {
            Turn = 1,
            Player = CreatePlayer(),
            Monsters = new List<MonsterInfo> { CreateMonster() },
        });
        var adapter = CreateAdapter(tracker);
        return (tracker, adapter);
    }

    // ─── HandleTurnStart ──────────────────────────────────────────────

    [Fact]
    public void HandleTurnStart_ValidTurn_UpdatesCombatTurnNumber()
    {
        var (tracker, adapter) = CreateWithCombat();

        adapter.HandleTurnStart(
            turn: 3,
            player: CreatePlayer(),
            hand: new List<CardInfo>(),
            drawPile: new List<CardInfo>(),
            discardPile: new List<CardInfo>(),
            exhaustPile: new List<CardInfo>(),
            monsters: new List<MonsterInfo>());

        var state = tracker.GetCurrentState();
        Assert.NotNull(state.Combat);
        Assert.Equal(3, state.Combat!.Turn);
    }

    [Fact]
    public void HandleTurnStart_WithPlayerData_UpdatesPlayerState()
    {
        var (tracker, adapter) = CreateWithCombat();
        var player = CreatePlayer(hp: 55, maxHp: 80, block: 10, energy: 2, maxEnergy: 4, gold: 250);

        adapter.HandleTurnStart(
            turn: 2,
            player: player,
            hand: new List<CardInfo>(),
            drawPile: new List<CardInfo>(),
            discardPile: new List<CardInfo>(),
            exhaustPile: new List<CardInfo>(),
            monsters: new List<MonsterInfo>());

        var state = tracker.GetCurrentState();
        Assert.Equal(55, state.Combat!.Player.Hp);
        Assert.Equal(80, state.Combat.Player.MaxHp);
        Assert.Equal(10, state.Combat.Player.Block);
        Assert.Equal(2, state.Combat.Player.Energy);
        Assert.Equal(4, state.Combat.Player.MaxEnergy);
        Assert.Equal(250, state.Combat.Player.Gold);
    }

    [Fact]
    public void HandleTurnStart_WithCards_UpdatesHand()
    {
        var (tracker, adapter) = CreateWithCombat();
        var hand = new List<CardInfo>
        {
            CreateCard("strike", "Strike"),
            CreateCard("defend", "Defend"),
            CreateCard("bash", "Bash"),
        };

        adapter.HandleTurnStart(
            turn: 1,
            player: CreatePlayer(),
            hand: hand,
            drawPile: new List<CardInfo>(),
            discardPile: new List<CardInfo>(),
            exhaustPile: new List<CardInfo>(),
            monsters: new List<MonsterInfo>());

        var state = tracker.GetCurrentState();
        Assert.Equal(3, state.Combat!.Hand.Count);
        Assert.Equal("strike", state.Combat.Hand[0].Id);
        Assert.Equal("defend", state.Combat.Hand[1].Id);
        Assert.Equal("bash", state.Combat.Hand[2].Id);
    }

    [Fact]
    public void HandleTurnStart_WithPiles_UpdatesDrawPile()
    {
        var (tracker, adapter) = CreateWithCombat();
        var drawPile = new List<CardInfo>
        {
            CreateCard("anger", "Anger"),
            CreateCard("cleave", "Cleave"),
        };

        adapter.HandleTurnStart(
            turn: 1,
            player: CreatePlayer(),
            hand: new List<CardInfo>(),
            drawPile: drawPile,
            discardPile: new List<CardInfo>(),
            exhaustPile: new List<CardInfo>(),
            monsters: new List<MonsterInfo>());

        var state = tracker.GetCurrentState();
        Assert.Equal(2, state.Combat!.DrawPile.Count);
        Assert.Equal("anger", state.Combat.DrawPile[0].Id);
    }

    [Fact]
    public void HandleTurnStart_WithPiles_UpdatesDiscardPile()
    {
        var (tracker, adapter) = CreateWithCombat();
        var discardPile = new List<CardInfo>
        {
            CreateCard("strike", "Strike"),
        };

        adapter.HandleTurnStart(
            turn: 1,
            player: CreatePlayer(),
            hand: new List<CardInfo>(),
            drawPile: new List<CardInfo>(),
            discardPile: discardPile,
            exhaustPile: new List<CardInfo>(),
            monsters: new List<MonsterInfo>());

        var state = tracker.GetCurrentState();
        Assert.Single(state.Combat!.DiscardPile);
        Assert.Equal("strike", state.Combat.DiscardPile[0].Id);
    }

    [Fact]
    public void HandleTurnStart_WithPiles_UpdatesExhaustPile()
    {
        var (tracker, adapter) = CreateWithCombat();
        var exhaustPile = new List<CardInfo>
        {
            CreateCard("true_grit", "True Grit"),
            CreateCard("offering", "Offering"),
        };

        adapter.HandleTurnStart(
            turn: 2,
            player: CreatePlayer(),
            hand: new List<CardInfo>(),
            drawPile: new List<CardInfo>(),
            discardPile: new List<CardInfo>(),
            exhaustPile: exhaustPile,
            monsters: new List<MonsterInfo>());

        var state = tracker.GetCurrentState();
        Assert.Equal(2, state.Combat!.ExhaustPile.Count);
        Assert.Equal("true_grit", state.Combat.ExhaustPile[0].Id);
        Assert.Equal("offering", state.Combat.ExhaustPile[1].Id);
    }

    [Fact]
    public void HandleTurnStart_WithMonsters_UpdatesMonstersList()
    {
        var (tracker, adapter) = CreateWithCombat();
        var monsters = new List<MonsterInfo>
        {
            CreateMonster("jaw_worm", "Jaw Worm", 44, 44),
            CreateMonster("cultist", "Cultist", 50, 50),
        };

        adapter.HandleTurnStart(
            turn: 1,
            player: CreatePlayer(),
            hand: new List<CardInfo>(),
            drawPile: new List<CardInfo>(),
            discardPile: new List<CardInfo>(),
            exhaustPile: new List<CardInfo>(),
            monsters: monsters);

        var state = tracker.GetCurrentState();
        Assert.Equal(2, state.Combat!.Monsters.Count);
        Assert.Equal("jaw_worm", state.Combat.Monsters[0].Id);
        Assert.Equal("Jaw Worm", state.Combat.Monsters[0].Name);
        Assert.Equal("cultist", state.Combat.Monsters[1].Id);
    }

    [Fact]
    public void HandleTurnStart_NoCombatState_DoesNotCrash()
    {
        var tracker = CreateTracker();
        // No combat state set — Combat is null
        var adapter = CreateAdapter(tracker);

        // Should not throw
        adapter.HandleTurnStart(
            turn: 1,
            player: CreatePlayer(),
            hand: new List<CardInfo> { CreateCard() },
            drawPile: new List<CardInfo>(),
            discardPile: new List<CardInfo>(),
            exhaustPile: new List<CardInfo>(),
            monsters: new List<MonsterInfo> { CreateMonster() });

        var state = tracker.GetCurrentState();
        Assert.Null(state.Combat);
    }

    // ─── HandleCardPlayed ─────────────────────────────────────────────

    [Fact]
    public void HandleCardPlayed_EmitsCardPlayedEvent_WithCardInfo()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var card = CreateCard("bash", "Bash");

        adapter.HandleCardPlayed(card, "Jaw Worm");

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("card_played", events[0].Type);
        Assert.NotNull(events[0].Data);
    }

    [Fact]
    public void HandleCardPlayed_EventData_ContainsTargetName()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var card = CreateCard("bash", "Bash");

        adapter.HandleCardPlayed(card, "Cultist");

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        // Serialize and inspect the data to verify target is present
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"target\":\"Cultist\"", json);
    }

    [Fact]
    public void HandleCardPlayed_EventType_IsCardPlayed()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type != "state_update") receivedEvent = e;
        };

        adapter.HandleCardPlayed(CreateCard(), "");

        Assert.NotNull(receivedEvent);
        Assert.Equal("card_played", receivedEvent!.Type);
    }

    // ─── HandleCombatStart ────────────────────────────────────────────

    [Fact]
    public void HandleCombatStart_CreatesCombatState_WithMonsters()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var monsters = new List<MonsterInfo>
        {
            CreateMonster("jaw_worm", "Jaw Worm", 44, 44),
            CreateMonster("louse_l", "Red Louse", 12, 12),
        };
        var player = CreatePlayer(hp: 80, maxHp: 80);

        adapter.HandleCombatStart(monsters, player);

        var state = tracker.GetCurrentState();
        Assert.NotNull(state.Combat);
        Assert.Equal(1, state.Combat!.Turn);
        Assert.Equal(2, state.Combat.Monsters.Count);
        Assert.Equal("jaw_worm", state.Combat.Monsters[0].Id);
        Assert.Equal(80, state.Combat.Player.Hp);
    }

    [Fact]
    public void HandleCombatStart_SetsScreenToCombat()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleCombatStart(
            new List<MonsterInfo> { CreateMonster() },
            CreatePlayer());

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Combat, state.Screen);
    }

    [Fact]
    public void HandleCombatStart_EmitsCombatStartEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleCombatStart(
            new List<MonsterInfo> { CreateMonster() },
            CreatePlayer());

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("combat_start", events[0].Type);
    }

    // ─── HandleCombatEnd ──────────────────────────────────────────────

    [Fact]
    public void HandleCombatEnd_ClearsCombatState()
    {
        var tracker = CreateTracker();
        tracker.SetCombatState(new CombatState { Turn = 3 });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCombatEnd(won: true, isBoss: false, floor: 5);

        var state = tracker.GetCurrentState();
        Assert.Null(state.Combat);
    }

    [Fact]
    public void HandleCombatEnd_WonBoss_SetsScreenToBossReward()
    {
        var tracker = CreateTracker();
        tracker.SetCombatState(new CombatState { Turn = 5 });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCombatEnd(won: true, isBoss: true, floor: 17);

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.BossReward, state.Screen);
    }

    [Fact]
    public void HandleCombatEnd_Lost_SetsScreenToGameOver()
    {
        var tracker = CreateTracker();
        tracker.SetCombatState(new CombatState { Turn = 2 });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCombatEnd(won: false, isBoss: false, floor: 8);

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.GameOver, state.Screen);
    }

    [Fact]
    public void HandleCombatEnd_EmitsCombatEndEvent_WithCorrectData()
    {
        var tracker = CreateTracker();
        tracker.SetCombatState(new CombatState { Turn = 1 });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCombatEnd(won: true, isBoss: true, floor: 17);

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("combat_end", events[0].Type);

        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"won\":true", json);
        Assert.Contains("\"isBoss\":true", json);
        Assert.Contains("\"floor\":17", json);
    }

    // ─── HandleCardAddedToDeck ─────────────────────────────────────

    [Fact]
    public void HandleCardAddedToDeck_AddsCardToDeck()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var card = CreateCard("bash", "Bash");

        adapter.HandleCardAddedToDeck(card);

        var state = tracker.GetCurrentState();
        Assert.Single(state.Deck);
        Assert.Equal("bash", state.Deck[0].Id);
        Assert.Equal("Bash", state.Deck[0].Name);
    }

    [Fact]
    public void HandleCardAddedToDeck_AppendsToExistingDeck()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Deck.Add(CreateCard("strike", "Strike"));
            state.Deck.Add(CreateCard("defend", "Defend"));
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCardAddedToDeck(CreateCard("bash", "Bash"));

        var state = tracker.GetCurrentState();
        Assert.Equal(3, state.Deck.Count);
        Assert.Equal("strike", state.Deck[0].Id);
        Assert.Equal("defend", state.Deck[1].Id);
        Assert.Equal("bash", state.Deck[2].Id);
    }

    [Fact]
    public void HandleCardAddedToDeck_EmitsDeckChangedEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var card = CreateCard("offering", "Offering");

        adapter.HandleCardAddedToDeck(card);

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("deck_changed", events[0].Type);
    }

    [Fact]
    public void HandleCardAddedToDeck_EventData_ContainsActionAndCard()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var card = CreateCard("bash", "Bash");

        adapter.HandleCardAddedToDeck(card);

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"action\":\"added\"", json);
        Assert.Contains("\"id\":\"bash\"", json);
    }

    [Fact]
    public void HandleCardAddedToDeck_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "deck_changed") receivedEvent = e;
        };

        adapter.HandleCardAddedToDeck(CreateCard());

        Assert.NotNull(receivedEvent);
        Assert.Equal("deck_changed", receivedEvent!.Type);
    }

    // ─── HandleCardRemovedFromDeck ─────────────────────────────────

    [Fact]
    public void HandleCardRemovedFromDeck_RemovesMatchingCard()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Deck.Add(CreateCard("strike", "Strike"));
            state.Deck.Add(CreateCard("bash", "Bash"));
            state.Deck.Add(CreateCard("defend", "Defend"));
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCardRemovedFromDeck(CreateCard("bash", "Bash"));

        var state = tracker.GetCurrentState();
        Assert.Equal(2, state.Deck.Count);
        Assert.Equal("strike", state.Deck[0].Id);
        Assert.Equal("defend", state.Deck[1].Id);
    }

    [Fact]
    public void HandleCardRemovedFromDeck_RemovesOnlyFirstMatch()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Deck.Add(CreateCard("strike", "Strike"));
            state.Deck.Add(CreateCard("strike", "Strike"));
            state.Deck.Add(CreateCard("strike", "Strike"));
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCardRemovedFromDeck(CreateCard("strike", "Strike"));

        var state = tracker.GetCurrentState();
        Assert.Equal(2, state.Deck.Count);
        Assert.All(state.Deck, c => Assert.Equal("strike", c.Id));
    }

    [Fact]
    public void HandleCardRemovedFromDeck_NoMatch_DeckUnchanged()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Deck.Add(CreateCard("strike", "Strike"));
            state.Deck.Add(CreateCard("defend", "Defend"));
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCardRemovedFromDeck(CreateCard("bash", "Bash"));

        var state = tracker.GetCurrentState();
        Assert.Equal(2, state.Deck.Count);
    }

    [Fact]
    public void HandleCardRemovedFromDeck_EmptyDeck_DoesNotCrash()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        // Should not throw on empty deck
        adapter.HandleCardRemovedFromDeck(CreateCard("strike", "Strike"));

        var state = tracker.GetCurrentState();
        Assert.Empty(state.Deck);
    }

    [Fact]
    public void HandleCardRemovedFromDeck_EmitsCardRemovedEvent()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Deck.Add(CreateCard("bash", "Bash"));
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCardRemovedFromDeck(CreateCard("bash", "Bash"));

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("card_removed", events[0].Type);
    }

    [Fact]
    public void HandleCardRemovedFromDeck_EventData_ContainsCardInfo()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Deck.Add(CreateCard("bash", "Bash"));
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleCardRemovedFromDeck(CreateCard("bash", "Bash"));

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"id\":\"bash\"", json);
        Assert.Contains("\"name\":\"Bash\"", json);
    }

    // ─── HandleShopEntered ─────────────────────────────────────────

    [Fact]
    public void HandleShopEntered_SetsScreenToShop()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleShopEntered();

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Shop, state.Screen);
    }

    [Fact]
    public void HandleShopEntered_EmitsFloorChangedEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleShopEntered();

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("floor_changed", events[0].Type);
    }

    [Fact]
    public void HandleShopEntered_EventData_ContainsShopScreen()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleShopEntered();

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"screen\":\"shop\"", json);
    }

    [Fact]
    public void HandleShopEntered_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "floor_changed") receivedEvent = e;
        };

        adapter.HandleShopEntered();

        Assert.NotNull(receivedEvent);
        Assert.Equal("floor_changed", receivedEvent!.Type);
    }

    // ─── HandleRestEntered ─────────────────────────────────────────

    [Fact]
    public void HandleRestEntered_SetsScreenToRest()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleRestEntered(new List<RestOption>());

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Rest, state.Screen);
    }

    [Fact]
    public void HandleRestEntered_StoresRestOptions()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var options = new List<RestOption>
        {
            new() { Id = "rest", Name = "Rest", Description = "Heal 30% HP", Enabled = true },
            new() { Id = "smith", Name = "Smith", Description = "Upgrade a card", Enabled = true },
        };

        adapter.HandleRestEntered(options);

        var state = tracker.GetCurrentState();
        Assert.NotNull(state.RestOptions);
        Assert.Equal(2, state.RestOptions!.Count);
        Assert.Equal("rest", state.RestOptions[0].Id);
        Assert.Equal("Rest", state.RestOptions[0].Name);
        Assert.Equal("smith", state.RestOptions[1].Id);
    }

    [Fact]
    public void HandleRestEntered_EmptyOptions_StoresEmptyList()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleRestEntered(new List<RestOption>());

        var state = tracker.GetCurrentState();
        Assert.NotNull(state.RestOptions);
        Assert.Empty(state.RestOptions!);
    }

    [Fact]
    public void HandleRestEntered_EmitsRestEnteredEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleRestEntered(new List<RestOption>
        {
            new() { Id = "rest", Name = "Rest" },
        });

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("rest_entered", events[0].Type);
    }

    [Fact]
    public void HandleRestEntered_EventData_ContainsOptions()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var options = new List<RestOption>
        {
            new() { Id = "dig", Name = "Dig", Description = "Find a relic", Enabled = false },
        };

        adapter.HandleRestEntered(options);

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"id\":\"dig\"", json);
        Assert.Contains("\"name\":\"Dig\"", json);
        Assert.Contains("\"enabled\":false", json);
    }

    [Fact]
    public void HandleRestEntered_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "rest_entered") receivedEvent = e;
        };

        adapter.HandleRestEntered(new List<RestOption>());

        Assert.NotNull(receivedEvent);
        Assert.Equal("rest_entered", receivedEvent!.Type);
    }

    // ─── HandleCardRewardsShown ───────────────────────────────────────

    [Fact]
    public void HandleCardRewardsShown_StoresCardRewards()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var cards = new List<CardInfo>
        {
            CreateCard("bash", "Bash"),
            CreateCard("cleave", "Cleave"),
            CreateCard("iron_wave", "Iron Wave"),
        };

        adapter.HandleCardRewardsShown(cards);

        var state = tracker.GetCurrentState();
        Assert.NotNull(state.CardRewards);
        Assert.Equal(3, state.CardRewards!.Count);
        Assert.Equal("bash", state.CardRewards[0].Id);
        Assert.Equal("cleave", state.CardRewards[1].Id);
        Assert.Equal("iron_wave", state.CardRewards[2].Id);
    }

    [Fact]
    public void HandleCardRewardsShown_SetsScreenToCardReward()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleCardRewardsShown(new List<CardInfo> { CreateCard() });

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.CardReward, state.Screen);
    }

    [Fact]
    public void HandleCardRewardsShown_EmitsCardRewardsShownEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var cards = new List<CardInfo> { CreateCard("bash", "Bash") };

        adapter.HandleCardRewardsShown(cards);

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("card_rewards_shown", events[0].Type);
    }

    [Fact]
    public void HandleCardRewardsShown_EventData_ContainsCards()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var cards = new List<CardInfo>
        {
            CreateCard("bash", "Bash"),
            CreateCard("cleave", "Cleave"),
        };

        adapter.HandleCardRewardsShown(cards);

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"id\":\"bash\"", json);
        Assert.Contains("\"id\":\"cleave\"", json);
    }

    [Fact]
    public void HandleCardRewardsShown_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "card_rewards_shown") receivedEvent = e;
        };

        adapter.HandleCardRewardsShown(new List<CardInfo> { CreateCard() });

        Assert.NotNull(receivedEvent);
        Assert.Equal("card_rewards_shown", receivedEvent!.Type);
    }

    [Fact]
    public void HandleCardRewardsShown_EmptyList_StoresEmptyRewards()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleCardRewardsShown(new List<CardInfo>());

        var state = tracker.GetCurrentState();
        Assert.NotNull(state.CardRewards);
        Assert.Empty(state.CardRewards!);
    }

    // ─── HandleCardPicked ─────────────────────────────────────────────

    [Fact]
    public void HandleCardPicked_EmitsCardPickedEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var card = CreateCard("bash", "Bash");
        var alternatives = new List<CardInfo>
        {
            CreateCard("bash", "Bash"),
            CreateCard("cleave", "Cleave"),
            CreateCard("iron_wave", "Iron Wave"),
        };

        adapter.HandleCardPicked(card, alternatives);

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("card_picked", events[0].Type);
    }

    [Fact]
    public void HandleCardPicked_EventData_ContainsCardAndAlternatives()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var card = CreateCard("bash", "Bash");
        var alternatives = new List<CardInfo>
        {
            CreateCard("bash", "Bash"),
            CreateCard("cleave", "Cleave"),
        };

        adapter.HandleCardPicked(card, alternatives);

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"card\":", json);
        Assert.Contains("\"alternatives\":", json);
        Assert.Contains("\"id\":\"bash\"", json);
        Assert.Contains("\"id\":\"cleave\"", json);
    }

    [Fact]
    public void HandleCardPicked_ClearsCardRewards()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        // First show some card rewards
        adapter.HandleCardRewardsShown(new List<CardInfo>
        {
            CreateCard("bash", "Bash"),
            CreateCard("cleave", "Cleave"),
        });
        Assert.NotNull(tracker.GetCurrentState().CardRewards);
        Assert.Equal(2, tracker.GetCurrentState().CardRewards!.Count);

        // Now pick a card — should clear rewards
        adapter.HandleCardPicked(
            CreateCard("bash", "Bash"),
            new List<CardInfo> { CreateCard("bash", "Bash"), CreateCard("cleave", "Cleave") });

        var state = tracker.GetCurrentState();
        Assert.Null(state.CardRewards);
    }

    [Fact]
    public void HandleCardPicked_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "card_picked") receivedEvent = e;
        };

        adapter.HandleCardPicked(CreateCard(), new List<CardInfo>());

        Assert.NotNull(receivedEvent);
        Assert.Equal("card_picked", receivedEvent!.Type);
    }

    [Fact]
    public void HandleCardPicked_EmptyAlternatives_StillEmitsEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleCardPicked(CreateCard("bash", "Bash"), new List<CardInfo>());

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("card_picked", events[0].Type);
    }

    [Fact]
    public void HandleCardRewardsShown_ThenPicked_FullLifecycle()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var offered = new List<CardInfo>
        {
            CreateCard("bash", "Bash"),
            CreateCard("cleave", "Cleave"),
            CreateCard("iron_wave", "Iron Wave"),
        };

        // Step 1: Show card rewards
        adapter.HandleCardRewardsShown(offered);
        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.CardReward, state.Screen);
        Assert.Equal(3, state.CardRewards!.Count);

        // Step 2: Pick a card
        adapter.HandleCardPicked(CreateCard("cleave", "Cleave"), offered);
        state = tracker.GetCurrentState();
        Assert.Null(state.CardRewards);

        // Verify events: card_rewards_shown, then card_picked
        var events = tracker.GetEventsSince(0);
        Assert.Equal(2, events.Count);
        Assert.Equal("card_rewards_shown", events[0].Type);
        Assert.Equal("card_picked", events[1].Type);
    }

    // ─── HandleEventStarted ──────────────────────────────────────────

    [Fact]
    public void HandleEventStarted_SetsScreenToEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleEventStarted("Big Fish", "A feast!", new List<EventOption>());

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Event, state.Screen);
    }

    [Fact]
    public void HandleEventStarted_StoresEventOptions()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var options = new List<EventOption>
        {
            new() { Id = "eat", Text = "Eat (Heal 5 HP)", Enabled = true },
            new() { Id = "steal", Text = "Steal (Gain 50 Gold)", Enabled = true },
            new() { Id = "leave", Text = "Leave", Enabled = true },
        };

        adapter.HandleEventStarted("Big Fish", "A feast!", options);

        var state = tracker.GetCurrentState();
        Assert.NotNull(state.EventOptions);
        Assert.Equal(3, state.EventOptions!.Count);
        Assert.Equal("eat", state.EventOptions[0].Id);
        Assert.Equal("Eat (Heal 5 HP)", state.EventOptions[0].Text);
        Assert.True(state.EventOptions[0].Enabled);
        Assert.Equal("steal", state.EventOptions[1].Id);
        Assert.Equal("leave", state.EventOptions[2].Id);
    }

    [Fact]
    public void HandleEventStarted_EmptyOptions_StoresEmptyList()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleEventStarted("Mysterious Event", "...", new List<EventOption>());

        var state = tracker.GetCurrentState();
        Assert.NotNull(state.EventOptions);
        Assert.Empty(state.EventOptions!);
    }

    [Fact]
    public void HandleEventStarted_EmitsEventStartedEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleEventStarted("Big Fish", "A feast!", new List<EventOption>
        {
            new() { Id = "eat", Text = "Eat" },
        });

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("event_started", events[0].Type);
    }

    [Fact]
    public void HandleEventStarted_EventData_ContainsNameAndDescription()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleEventStarted("Big Fish", "A feast awaits!", new List<EventOption>());

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"name\":\"Big Fish\"", json);
        Assert.Contains("\"description\":\"A feast awaits!\"", json);
    }

    [Fact]
    public void HandleEventStarted_EventData_ContainsOptions()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var options = new List<EventOption>
        {
            new() { Id = "eat", Text = "Eat (Heal 5 HP)", Enabled = true },
            new() { Id = "leave", Text = "Leave", Enabled = false },
        };

        adapter.HandleEventStarted("Big Fish", "A feast!", options);

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"id\":\"eat\"", json);
        Assert.Contains("\"id\":\"leave\"", json);
        Assert.Contains("\"enabled\":false", json);
    }

    [Fact]
    public void HandleEventStarted_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "event_started") receivedEvent = e;
        };

        adapter.HandleEventStarted("Big Fish", "A feast!", new List<EventOption>());

        Assert.NotNull(receivedEvent);
        Assert.Equal("event_started", receivedEvent!.Type);
    }

    [Fact]
    public void HandleEventStarted_WithDisabledOption_PreservesEnabledFlag()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var options = new List<EventOption>
        {
            new() { Id = "upgrade", Text = "Upgrade a card", Enabled = true },
            new() { Id = "remove", Text = "Remove a card (Requires 75 Gold)", Enabled = false },
        };

        adapter.HandleEventStarted("Bonfire Spirits", "Strange spirits...", options);

        var state = tracker.GetCurrentState();
        Assert.True(state.EventOptions![0].Enabled);
        Assert.False(state.EventOptions[1].Enabled);
    }

    [Fact]
    public void HandleEventStarted_OverwritesPreviousEventOptions()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        // First event
        adapter.HandleEventStarted("Event A", "First", new List<EventOption>
        {
            new() { Id = "a1", Text = "Option A1" },
        });

        // Second event overwrites
        adapter.HandleEventStarted("Event B", "Second", new List<EventOption>
        {
            new() { Id = "b1", Text = "Option B1" },
            new() { Id = "b2", Text = "Option B2" },
        });

        var state = tracker.GetCurrentState();
        Assert.Equal(2, state.EventOptions!.Count);
        Assert.Equal("b1", state.EventOptions[0].Id);
        Assert.Equal("b2", state.EventOptions[1].Id);
    }

    // ─── HandleEventChoice ───────────────────────────────────────────

    [Fact]
    public void HandleEventChoice_EmitsEventChoiceEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleEventChoice("eat", "Eat (Heal 5 HP)");

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("event_choice", events[0].Type);
    }

    [Fact]
    public void HandleEventChoice_EventData_ContainsChoiceIdAndName()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleEventChoice("steal", "Steal (Gain 50 Gold)");

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"choiceId\":\"steal\"", json);
        Assert.Contains("\"choiceName\":\"Steal (Gain 50 Gold)\"", json);
    }

    [Fact]
    public void HandleEventChoice_ClearsEventOptions()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        // First set up event options
        adapter.HandleEventStarted("Big Fish", "A feast!", new List<EventOption>
        {
            new() { Id = "eat", Text = "Eat" },
            new() { Id = "leave", Text = "Leave" },
        });
        Assert.NotNull(tracker.GetCurrentState().EventOptions);
        Assert.Equal(2, tracker.GetCurrentState().EventOptions!.Count);

        // Choice should clear event options
        adapter.HandleEventChoice("eat", "Eat");

        var state = tracker.GetCurrentState();
        Assert.Null(state.EventOptions);
    }

    [Fact]
    public void HandleEventChoice_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "event_choice") receivedEvent = e;
        };

        adapter.HandleEventChoice("eat", "Eat");

        Assert.NotNull(receivedEvent);
        Assert.Equal("event_choice", receivedEvent!.Type);
    }

    [Fact]
    public void HandleEventChoice_WithUnknownChoice_StillEmitsEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleEventChoice("unknown", "unknown");

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("event_choice", events[0].Type);
    }

    [Fact]
    public void HandleEventChoice_WithoutPriorEvent_DoesNotCrash()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        // No prior HandleEventStarted — should not throw
        adapter.HandleEventChoice("eat", "Eat");

        var state = tracker.GetCurrentState();
        Assert.Null(state.EventOptions);
    }

    // ─── Event Full Lifecycle ────────────────────────────────────────

    [Fact]
    public void HandleEventStarted_ThenChoice_FullLifecycle()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var options = new List<EventOption>
        {
            new() { Id = "eat", Text = "Eat (Heal 5 HP)", Enabled = true },
            new() { Id = "steal", Text = "Steal (Gain 50 Gold)", Enabled = true },
            new() { Id = "leave", Text = "Leave", Enabled = true },
        };

        // Step 1: Event starts
        adapter.HandleEventStarted("Big Fish", "A feast!", options);
        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Event, state.Screen);
        Assert.Equal(3, state.EventOptions!.Count);

        // Step 2: Player makes a choice
        adapter.HandleEventChoice("eat", "Eat (Heal 5 HP)");
        state = tracker.GetCurrentState();
        Assert.Null(state.EventOptions);

        // Verify events: event_started, then event_choice
        var events = tracker.GetEventsSince(0);
        Assert.Equal(2, events.Count);
        Assert.Equal("event_started", events[0].Type);
        Assert.Equal("event_choice", events[1].Type);
    }

    // ─── HandleRelicObtained ─────────────────────────────────────────

    [Fact]
    public void HandleRelicObtained_AddsRelicToState()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var relic = new RelicInfo { Id = "burning_blood", Name = "Burning Blood", Rarity = "starter" };

        adapter.HandleRelicObtained(relic);

        var state = tracker.GetCurrentState();
        Assert.Single(state.Relics);
        Assert.Equal("burning_blood", state.Relics[0].Id);
        Assert.Equal("Burning Blood", state.Relics[0].Name);
    }

    [Fact]
    public void HandleRelicObtained_AppendsToExistingRelics()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Relics.Add(new RelicInfo { Id = "burning_blood", Name = "Burning Blood" });
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleRelicObtained(new RelicInfo { Id = "vajra", Name = "Vajra" });

        var state = tracker.GetCurrentState();
        Assert.Equal(2, state.Relics.Count);
        Assert.Equal("burning_blood", state.Relics[0].Id);
        Assert.Equal("vajra", state.Relics[1].Id);
    }

    [Fact]
    public void HandleRelicObtained_EmitsRelicObtainedEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleRelicObtained(new RelicInfo { Id = "vajra", Name = "Vajra" });

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("relic_obtained", events[0].Type);
    }

    [Fact]
    public void HandleRelicObtained_EventData_ContainsRelicInfo()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleRelicObtained(new RelicInfo { Id = "vajra", Name = "Vajra", Rarity = "common" });

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"id\":\"vajra\"", json);
        Assert.Contains("\"name\":\"Vajra\"", json);
    }

    [Fact]
    public void HandleRelicObtained_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "relic_obtained") receivedEvent = e;
        };

        adapter.HandleRelicObtained(new RelicInfo { Id = "vajra", Name = "Vajra" });

        Assert.NotNull(receivedEvent);
        Assert.Equal("relic_obtained", receivedEvent!.Type);
    }

    // ─── HandleRunStart ──────────────────────────────────────────────

    [Fact]
    public void HandleRunStart_SetsFullGameState()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var initialState = new GameState
        {
            Screen = ScreenType.Map,
            Character = "silent",
            Act = 1,
            Floor = 0,
            Ascension = 15,
            Seed = "ABC123",
            Deck = new List<CardInfo>
            {
                CreateCard("strike", "Strike"),
                CreateCard("defend", "Defend"),
                CreateCard("neutralize", "Neutralize"),
            },
            Relics = new List<RelicInfo>
            {
                new() { Id = "ring_of_the_snake", Name = "Ring of the Snake" },
            },
        };

        adapter.HandleRunStart(initialState);

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Map, state.Screen);
        Assert.Equal("silent", state.Character);
        Assert.Equal(15, state.Ascension);
        Assert.Equal("ABC123", state.Seed);
        Assert.Equal(3, state.Deck.Count);
        Assert.Single(state.Relics);
    }

    [Fact]
    public void HandleRunStart_EmitsRunStartEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var initialState = new GameState
        {
            Character = "ironclad",
            Ascension = 5,
            Seed = "XYZ789",
        };

        adapter.HandleRunStart(initialState);

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("run_start", events[0].Type);
    }

    [Fact]
    public void HandleRunStart_EventData_ContainsCharacterAscensionSeed()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var initialState = new GameState
        {
            Character = "defect",
            Ascension = 20,
            Seed = "SEED42",
        };

        adapter.HandleRunStart(initialState);

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"character\":\"defect\"", json);
        Assert.Contains("\"ascension\":20", json);
        Assert.Contains("\"seed\":\"SEED42\"", json);
    }

    [Fact]
    public void HandleRunStart_ReplacesExistingState()
    {
        var tracker = CreateTracker();
        // Set up old state
        tracker.SetState(new GameState
        {
            Screen = ScreenType.Victory,
            Character = "ironclad",
            Floor = 57,
            Ascension = 10,
        });
        var adapter = CreateAdapter(tracker);

        // Start new run — should fully replace state
        adapter.HandleRunStart(new GameState
        {
            Screen = ScreenType.Map,
            Character = "silent",
            Floor = 0,
            Ascension = 5,
            Seed = "NEW",
        });

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Map, state.Screen);
        Assert.Equal("silent", state.Character);
        Assert.Equal(0, state.Floor);
        Assert.Equal(5, state.Ascension);
        Assert.Equal("NEW", state.Seed);
    }

    [Fact]
    public void HandleRunStart_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "run_start") receivedEvent = e;
        };

        adapter.HandleRunStart(new GameState { Character = "ironclad" });

        Assert.NotNull(receivedEvent);
        Assert.Equal("run_start", receivedEvent!.Type);
    }

    // ─── HandleRunEnd ────────────────────────────────────────────────

    [Fact]
    public void HandleRunEnd_Victory_SetsScreenToVictory()
    {
        var tracker = CreateTracker();
        tracker.SetState(new GameState { Screen = ScreenType.Combat, Floor = 57 });
        var adapter = CreateAdapter(tracker);

        adapter.HandleRunEnd(won: true, floor: 57, score: 1200);

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Victory, state.Screen);
    }

    [Fact]
    public void HandleRunEnd_Defeat_SetsScreenToGameOver()
    {
        var tracker = CreateTracker();
        tracker.SetState(new GameState { Screen = ScreenType.Combat, Floor = 23 });
        var adapter = CreateAdapter(tracker);

        adapter.HandleRunEnd(won: false, floor: 23, score: 450);

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.GameOver, state.Screen);
    }

    [Fact]
    public void HandleRunEnd_EmitsRunEndEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleRunEnd(won: true, floor: 50, score: 1000);

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("run_end", events[0].Type);
    }

    [Fact]
    public void HandleRunEnd_EventData_ContainsWonFloorScore()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleRunEnd(won: false, floor: 33, score: 600);

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"won\":false", json);
        Assert.Contains("\"floor\":33", json);
        Assert.Contains("\"score\":600", json);
    }

    [Fact]
    public void HandleRunEnd_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "run_end") receivedEvent = e;
        };

        adapter.HandleRunEnd(won: true, floor: 50, score: 1000);

        Assert.NotNull(receivedEvent);
        Assert.Equal("run_end", receivedEvent!.Type);
    }

    // ─── HandleRestChoice ────────────────────────────────────────────

    [Fact]
    public void HandleRestChoice_EmitsRestChoiceEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleRestChoice("rest", "Rest");

        var events = tracker.GetEventsSince(0);
        Assert.Single(events);
        Assert.Equal("rest_choice", events[0].Type);
    }

    [Fact]
    public void HandleRestChoice_EventData_ContainsChoiceIdAndName()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        adapter.HandleRestChoice("smith", "Smith");

        var events = tracker.GetEventsSince(0);
        var json = JsonSerializer.Serialize(events[0].Data, TestHelpers.JsonOptions);
        Assert.Contains("\"choiceId\":\"smith\"", json);
        Assert.Contains("\"choiceName\":\"Smith\"", json);
    }

    [Fact]
    public void HandleRestChoice_ClearsRestOptions()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        // Set up rest options first
        adapter.HandleRestEntered(new List<RestOption>
        {
            new() { Id = "rest", Name = "Rest" },
            new() { Id = "smith", Name = "Smith" },
        });
        Assert.NotNull(tracker.GetCurrentState().RestOptions);
        Assert.Equal(2, tracker.GetCurrentState().RestOptions!.Count);

        // Choice should clear rest options
        adapter.HandleRestChoice("rest", "Rest");

        var state = tracker.GetCurrentState();
        Assert.Null(state.RestOptions);
    }

    [Fact]
    public void HandleRestChoice_WithoutPriorRestEntered_DoesNotCrash()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        // No prior HandleRestEntered — should not throw
        adapter.HandleRestChoice("rest", "Rest");

        var state = tracker.GetCurrentState();
        Assert.Null(state.RestOptions);
    }

    [Fact]
    public void HandleRestChoice_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "rest_choice") receivedEvent = e;
        };

        adapter.HandleRestChoice("dig", "Dig");

        Assert.NotNull(receivedEvent);
        Assert.Equal("rest_choice", receivedEvent!.Type);
    }

    [Fact]
    public void HandleRestEntered_ThenChoice_FullLifecycle()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);
        var options = new List<RestOption>
        {
            new() { Id = "rest", Name = "Rest", Description = "Heal 30% HP", Enabled = true },
            new() { Id = "smith", Name = "Smith", Description = "Upgrade a card", Enabled = true },
            new() { Id = "dig", Name = "Dig", Description = "Find a relic", Enabled = false },
        };

        // Step 1: Enter rest site
        adapter.HandleRestEntered(options);
        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Rest, state.Screen);
        Assert.Equal(3, state.RestOptions!.Count);

        // Step 2: Make a choice
        adapter.HandleRestChoice("smith", "Smith");
        state = tracker.GetCurrentState();
        Assert.Null(state.RestOptions);

        // Verify events: rest_entered, then rest_choice
        var events = tracker.GetEventsSince(0);
        Assert.Equal(2, events.Count);
        Assert.Equal("rest_entered", events[0].Type);
        Assert.Equal("rest_choice", events[1].Type);
    }

    // ─── HandleShopExited ────────────────────────────────────────────

    [Fact]
    public void HandleShopExited_SetsScreenToMap()
    {
        var tracker = CreateTracker();
        tracker.SetScreen(ScreenType.Shop);
        var adapter = CreateAdapter(tracker);

        adapter.HandleShopExited();

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Map, state.Screen);
    }

    [Fact]
    public void HandleShopExited_ClearsShopCards()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Screen = ScreenType.Shop;
            state.ShopCards = new List<CardInfo>
            {
                CreateCard("bash", "Bash"),
                CreateCard("cleave", "Cleave"),
            };
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleShopExited();

        var state = tracker.GetCurrentState();
        Assert.Null(state.ShopCards);
    }

    [Fact]
    public void HandleShopExited_ClearsShopRelics()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Screen = ScreenType.Shop;
            state.ShopRelics = new List<RelicInfo>
            {
                new() { Id = "vajra", Name = "Vajra" },
            };
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleShopExited();

        var state = tracker.GetCurrentState();
        Assert.Null(state.ShopRelics);
    }

    [Fact]
    public void HandleShopExited_ClearsAllShopState()
    {
        var tracker = CreateTracker();
        tracker.UpdateState(state =>
        {
            state.Screen = ScreenType.Shop;
            state.ShopCards = new List<CardInfo> { CreateCard("bash", "Bash") };
            state.ShopRelics = new List<RelicInfo> { new() { Id = "vajra", Name = "Vajra" } };
        });
        var adapter = CreateAdapter(tracker);

        adapter.HandleShopExited();

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Map, state.Screen);
        Assert.Null(state.ShopCards);
        Assert.Null(state.ShopRelics);
    }

    [Fact]
    public void HandleShopExited_WithNoShopState_DoesNotCrash()
    {
        var tracker = CreateTracker();
        var adapter = CreateAdapter(tracker);

        // No prior shop state set — should not throw
        adapter.HandleShopExited();

        var state = tracker.GetCurrentState();
        Assert.Equal(ScreenType.Map, state.Screen);
    }

    [Fact]
    public void HandleShopExited_EmitsViaOnGameEvent()
    {
        var tracker = CreateTracker();
        tracker.SetScreen(ScreenType.Shop);
        var adapter = CreateAdapter(tracker);
        GameEvent? receivedEvent = null;
        tracker.OnGameEvent += e =>
        {
            if (e.Type == "state_update") receivedEvent = e;
        };

        adapter.HandleShopExited();

        // HandleShopExited uses UpdateState which emits state_update
        Assert.NotNull(receivedEvent);
        Assert.Equal("state_update", receivedEvent!.Type);
    }
}
