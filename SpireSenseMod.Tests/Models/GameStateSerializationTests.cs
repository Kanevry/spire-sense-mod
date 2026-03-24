using System.Text.Json;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests.Models;

public class GameStateSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [Fact]
    public void Serialize_DefaultState_UsesCamelCasePropertyNames()
    {
        var state = new GameState();

        var json = JsonSerializer.Serialize(state, JsonOptions);

        Assert.Contains("\"screen\":", json);
        Assert.Contains("\"character\":", json);
        Assert.Contains("\"floor\":", json);
        Assert.Contains("\"act\":", json);
        Assert.Contains("\"ascension\":", json);
        Assert.Contains("\"seed\":", json);
        Assert.Contains("\"gold\":", json);
        Assert.Contains("\"deck\":", json);
        Assert.Contains("\"relics\":", json);
        // Nullable fields should be absent when null
        Assert.DoesNotContain("\"Screen\":", json);
        Assert.DoesNotContain("\"Character\":", json);
    }

    [Fact]
    public void Serialize_DefaultState_IncludesAllExpectedFields()
    {
        var state = new GameState();

        var json = JsonSerializer.Serialize(state, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("main_menu", root.GetProperty("screen").GetString());
        Assert.Equal("ironclad", root.GetProperty("character").GetString());
        Assert.Equal(1, root.GetProperty("act").GetInt32());
        Assert.Equal(0, root.GetProperty("floor").GetInt32());
        Assert.Equal(0, root.GetProperty("ascension").GetInt32());
        Assert.Equal("", root.GetProperty("seed").GetString());
        Assert.Equal(0, root.GetProperty("gold").GetInt32());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("deck").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("relics").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("map").ValueKind);
    }

    [Fact]
    public void Serialize_DefaultState_HasCorrectDefaultValues()
    {
        var state = new GameState();

        Assert.Equal("main_menu", state.Screen);
        Assert.Equal("ironclad", state.Character);
        Assert.Equal(1, state.Act);
        Assert.Equal(0, state.Floor);
        Assert.Equal(0, state.Ascension);
        Assert.Equal("", state.Seed);
        Assert.Equal(0, state.Gold);
        Assert.Empty(state.Deck);
        Assert.Empty(state.Relics);
        Assert.Empty(state.Map);
        Assert.Null(state.Combat);
        Assert.Null(state.CardRewards);
        Assert.Null(state.ShopCards);
        Assert.Null(state.ShopRelics);
        Assert.Null(state.EventOptions);
        Assert.Null(state.RestOptions);
    }

    [Fact]
    public void Roundtrip_PopulatedState_PreservesAllFields()
    {
        var state = new GameState
        {
            Screen = "combat",
            Character = "silent",
            Act = 2,
            Floor = 15,
            Ascension = 5,
            Seed = "ABC123",
            Gold = 120,
            Deck = new()
            {
                new CardInfo { Id = "strike", Name = "Strike", Type = "attack", Cost = 1 },
                new CardInfo { Id = "defend", Name = "Defend", Type = "skill", Cost = 1 },
            },
            Relics = new()
            {
                new RelicInfo { Id = "burning_blood", Name = "Burning Blood", Rarity = "starter" },
            },
            Combat = new CombatState
            {
                Turn = 3,
                Player = new PlayerState { Hp = 50, MaxHp = 80, Block = 10, Energy = 2, Gold = 120 },
                Monsters = new()
                {
                    new MonsterInfo { Id = "jaw_worm", Name = "Jaw Worm", Hp = 30, MaxHp = 44, Intent = "attack", IntentDamage = 11 },
                },
                Hand = new()
                {
                    new CardInfo { Id = "strike", Name = "Strike" },
                },
            },
            Map = new()
            {
                new MapNode { X = 0, Y = 0, Type = "monster", Visited = true },
                new MapNode { X = 1, Y = 1, Type = "elite" },
            },
            CardRewards = new()
            {
                new CardInfo { Id = "pommel_strike", Name = "Pommel Strike" },
            },
            EventOptions = new()
            {
                new EventOption { Id = "opt1", Text = "Leave", Enabled = true },
            },
            RestOptions = new()
            {
                new RestOption { Id = "rest", Name = "Rest", Description = "Heal 30% HP.", Enabled = true },
                new RestOption { Id = "smith", Name = "Smith", Description = "Upgrade a card.", Enabled = false },
            },
        };

        var json = JsonSerializer.Serialize(state, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<GameState>(json, JsonOptions)!;

        Assert.Equal(state.Screen, deserialized.Screen);
        Assert.Equal(state.Character, deserialized.Character);
        Assert.Equal(state.Act, deserialized.Act);
        Assert.Equal(state.Floor, deserialized.Floor);
        Assert.Equal(state.Ascension, deserialized.Ascension);
        Assert.Equal(state.Seed, deserialized.Seed);
        Assert.Equal(state.Gold, deserialized.Gold);

        // Deck
        Assert.Equal(state.Deck.Count, deserialized.Deck.Count);
        Assert.Equal("strike", deserialized.Deck[0].Id);
        Assert.Equal("defend", deserialized.Deck[1].Id);

        // Relics
        Assert.Single(deserialized.Relics);
        Assert.Equal("burning_blood", deserialized.Relics[0].Id);

        // Combat
        Assert.NotNull(deserialized.Combat);
        Assert.Equal(3, deserialized.Combat!.Turn);
        Assert.Equal(50, deserialized.Combat.Player.Hp);
        Assert.Equal(80, deserialized.Combat.Player.MaxHp);
        Assert.Single(deserialized.Combat.Monsters);
        Assert.Equal("jaw_worm", deserialized.Combat.Monsters[0].Id);
        Assert.Single(deserialized.Combat.Hand);

        // Map
        Assert.Equal(2, deserialized.Map.Count);
        Assert.True(deserialized.Map[0].Visited);
        Assert.False(deserialized.Map[1].Visited);

        // CardRewards
        Assert.NotNull(deserialized.CardRewards);
        Assert.Single(deserialized.CardRewards!);

        // EventOptions
        Assert.NotNull(deserialized.EventOptions);
        Assert.Single(deserialized.EventOptions!);
        Assert.Equal("Leave", deserialized.EventOptions[0].Text);

        // RestOptions
        Assert.NotNull(deserialized.RestOptions);
        Assert.Equal(2, deserialized.RestOptions!.Count);
        Assert.Equal("rest", deserialized.RestOptions[0].Id);
        Assert.True(deserialized.RestOptions[0].Enabled);
        Assert.Equal("smith", deserialized.RestOptions[1].Id);
        Assert.False(deserialized.RestOptions[1].Enabled);
    }

    [Fact]
    public void Serialize_NullableFields_OmittedWhenNull()
    {
        var state = new GameState();

        var json = JsonSerializer.Serialize(state, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Null fields should serialize as null in System.Text.Json by default
        Assert.Equal(JsonValueKind.Null, root.GetProperty("combat").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("cardRewards").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("shopCards").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("shopRelics").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("eventOptions").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("restOptions").ValueKind);
    }
}
