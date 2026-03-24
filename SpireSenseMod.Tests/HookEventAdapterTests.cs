using System.Text.Json;
using SpireSenseMod;
using SpireSenseMod.Hooks;
using Xunit;

namespace SpireSenseMod.Tests;

public class HookEventAdapterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

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
        var json = JsonSerializer.Serialize(events[0].Data, JsonOptions);
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

        var json = JsonSerializer.Serialize(events[0].Data, JsonOptions);
        Assert.Contains("\"won\":true", json);
        Assert.Contains("\"isBoss\":true", json);
        Assert.Contains("\"floor\":17", json);
    }
}
