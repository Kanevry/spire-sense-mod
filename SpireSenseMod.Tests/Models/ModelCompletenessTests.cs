using System.Text.Json;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests.Models;

/// <summary>
/// Tests that verify all models serialize with correct JSON property names
/// and have sensible defaults. These catch accidental renames or missing attributes.
/// </summary>
public class ModelCompletenessTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    // ─── GameState Completeness ──────────────────────────────────────

    [Fact]
    public void GameState_IncludesRestOptionsField()
    {
        var state = new GameState
        {
            RestOptions = new()
            {
                new RestOption { Id = "rest", Name = "Rest", Description = "Heal 30% HP.", Enabled = true },
                new RestOption { Id = "smith", Name = "Smith", Description = "Upgrade a card.", Enabled = true },
            },
        };

        var json = JsonSerializer.Serialize(state, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("restOptions", out var restOptions));
        Assert.Equal(JsonValueKind.Array, restOptions.ValueKind);
        Assert.Equal(2, restOptions.GetArrayLength());
        Assert.Equal("rest", restOptions[0].GetProperty("id").GetString());
        Assert.Equal("smith", restOptions[1].GetProperty("id").GetString());
    }

    [Fact]
    public void GameState_RestOptionsNull_SerializesAsNull()
    {
        var state = new GameState();

        var json = JsonSerializer.Serialize(state, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("restOptions", out var restOptions));
        Assert.Equal(JsonValueKind.Null, restOptions.ValueKind);
    }

    [Fact]
    public void GameState_AllFieldsHaveJsonPropertyNames()
    {
        var state = new GameState
        {
            Screen = "test",
            Character = "ironclad",
            Act = 1,
            Floor = 5,
            Ascension = 10,
            Seed = "ABC",
            Deck = new() { new CardInfo { Id = "strike" } },
            Relics = new() { new RelicInfo { Id = "relic" } },
            Combat = new CombatState(),
            Map = new() { new MapNode() },
            CardRewards = new() { new CardInfo { Id = "reward" } },
            ShopCards = new() { new CardInfo { Id = "shop_card" } },
            ShopRelics = new() { new RelicInfo { Id = "shop_relic" } },
            EventOptions = new() { new EventOption { Id = "evt" } },
            RestOptions = new() { new RestOption { Id = "rest" } },
        };

        var json = JsonSerializer.Serialize(state, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify all 15 fields are present with camelCase names
        var expectedFields = new[]
        {
            "screen", "character", "act", "floor", "ascension", "seed",
            "deck", "relics", "combat", "map", "cardRewards",
            "shopCards", "shopRelics", "eventOptions", "restOptions",
        };

        foreach (var field in expectedFields)
        {
            Assert.True(root.TryGetProperty(field, out _), $"Missing field: {field}");
        }
    }

    // ─── RelicInfo Serialization ─────────────────────────────────────

    [Fact]
    public void RelicInfo_SerializesWithCorrectPropertyNames()
    {
        var relic = new RelicInfo
        {
            Id = "burning_blood",
            Name = "Burning Blood",
            Rarity = "starter",
            Description = "At the end of combat, heal 6 HP.",
            Character = "ironclad",
            Tags = new() { "healing" },
        };

        var json = JsonSerializer.Serialize(relic, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("burning_blood", root.GetProperty("id").GetString());
        Assert.Equal("Burning Blood", root.GetProperty("name").GetString());
        Assert.Equal("starter", root.GetProperty("rarity").GetString());
        Assert.Equal("At the end of combat, heal 6 HP.", root.GetProperty("description").GetString());
        Assert.Equal("ironclad", root.GetProperty("character").GetString());
        Assert.Single(JsonSerializer.Deserialize<List<string>>(root.GetProperty("tags").GetRawText())!);
    }

    [Fact]
    public void RelicInfo_DefaultValues_AreCorrect()
    {
        var relic = new RelicInfo();

        Assert.Equal("", relic.Id);
        Assert.Equal("", relic.Name);
        Assert.Equal("common", relic.Rarity);
        Assert.Equal("", relic.Description);
        Assert.Equal("neutral", relic.Character);
        Assert.Empty(relic.Tags);
    }

    [Fact]
    public void RelicInfo_Roundtrip_PreservesAll()
    {
        var relic = new RelicInfo
        {
            Id = "snecko_eye",
            Name = "Snecko Eye",
            Rarity = "boss",
            Description = "Draw 2 additional cards each turn. Cards cost random amounts.",
            Character = "neutral",
            Tags = new() { "draw", "randomize" },
        };

        var json = JsonSerializer.Serialize(relic, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RelicInfo>(json, JsonOptions)!;

        Assert.Equal(relic.Id, deserialized.Id);
        Assert.Equal(relic.Name, deserialized.Name);
        Assert.Equal(relic.Rarity, deserialized.Rarity);
        Assert.Equal(relic.Description, deserialized.Description);
        Assert.Equal(relic.Character, deserialized.Character);
        Assert.Equal(relic.Tags, deserialized.Tags);
    }

    // ─── EventOption Serialization ───────────────────────────────────

    [Fact]
    public void EventOption_SerializesWithCorrectPropertyNames()
    {
        var option = new EventOption
        {
            Id = "option_1",
            Text = "[Fight] Engage the enemy.",
            Enabled = true,
        };

        var json = JsonSerializer.Serialize(option, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("option_1", root.GetProperty("id").GetString());
        Assert.Equal("[Fight] Engage the enemy.", root.GetProperty("text").GetString());
        Assert.True(root.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void EventOption_DefaultValues_AreCorrect()
    {
        var option = new EventOption();

        Assert.Equal("", option.Id);
        Assert.Equal("", option.Text);
        Assert.True(option.Enabled);
    }

    [Fact]
    public void EventOption_DisabledOption_SerializesCorrectly()
    {
        var option = new EventOption { Id = "locked", Text = "Locked", Enabled = false };

        var json = JsonSerializer.Serialize(option, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.GetProperty("enabled").GetBoolean());
    }

    // ─── MapNode Serialization ───────────────────────────────────────

    [Fact]
    public void MapNode_SerializesWithCorrectPropertyNames()
    {
        var node = new MapNode
        {
            X = 3,
            Y = 7,
            Type = "elite",
            Connections = new() { 8, 9 },
            Visited = true,
        };

        var json = JsonSerializer.Serialize(node, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(3, root.GetProperty("x").GetInt32());
        Assert.Equal(7, root.GetProperty("y").GetInt32());
        Assert.Equal("elite", root.GetProperty("type").GetString());
        Assert.Equal(2, root.GetProperty("connections").GetArrayLength());
        Assert.True(root.GetProperty("visited").GetBoolean());
    }

    [Fact]
    public void MapNode_DefaultValues_AreCorrect()
    {
        var node = new MapNode();

        Assert.Equal(0, node.X);
        Assert.Equal(0, node.Y);
        Assert.Equal("monster", node.Type);
        Assert.Empty(node.Connections);
        Assert.False(node.Visited);
    }

    // ─── ShopItem Serialization ──────────────────────────────────────

    [Fact]
    public void ShopItem_SerializesWithCorrectPropertyNames()
    {
        var item = new ShopItem
        {
            Card = new CardInfo { Id = "pommel_strike", Name = "Pommel Strike", Cost = 1 },
            Price = 75,
        };

        var json = JsonSerializer.Serialize(item, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(75, root.GetProperty("price").GetInt32());
        Assert.Equal("pommel_strike", root.GetProperty("card").GetProperty("id").GetString());
    }

    [Fact]
    public void ShopRelicItem_SerializesWithCorrectPropertyNames()
    {
        var item = new ShopRelicItem
        {
            Relic = new RelicInfo { Id = "vajra", Name = "Vajra" },
            Price = 250,
        };

        var json = JsonSerializer.Serialize(item, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(250, root.GetProperty("price").GetInt32());
        Assert.Equal("vajra", root.GetProperty("relic").GetProperty("id").GetString());
    }

    // ─── PotionInfo Serialization ────────────────────────────────────

    [Fact]
    public void PotionInfo_DefaultValues_AreCorrect()
    {
        var potion = new PotionInfo();

        Assert.Equal("", potion.Id);
        Assert.Equal("", potion.Name);
        Assert.Equal("", potion.Description);
        Assert.False(potion.CanUse);
    }

    [Fact]
    public void PotionInfo_SerializesWithCorrectPropertyNames()
    {
        var potion = new PotionInfo
        {
            Id = "fire_potion",
            Name = "Fire Potion",
            Description = "Deal 20 damage.",
            CanUse = true,
        };

        var json = JsonSerializer.Serialize(potion, JsonOptions);

        Assert.Contains("\"id\":", json);
        Assert.Contains("\"name\":", json);
        Assert.Contains("\"description\":", json);
        Assert.Contains("\"canUse\":", json);

        Assert.DoesNotContain("\"CanUse\":", json);
    }

    // ─── PowerInfo Serialization ─────────────────────────────────────

    [Fact]
    public void PowerInfo_DefaultValues_AreCorrect()
    {
        var power = new PowerInfo();

        Assert.Equal("", power.Id);
        Assert.Equal("", power.Name);
        Assert.Equal(0, power.Amount);
    }

    [Fact]
    public void PowerInfo_NegativeAmount_SerializesCorrectly()
    {
        var power = new PowerInfo
        {
            Id = "dexterity",
            Name = "Dexterity",
            Amount = -2,
        };

        var json = JsonSerializer.Serialize(power, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(-2, doc.RootElement.GetProperty("amount").GetInt32());
    }

    // ─── GameEvent Serialization ─────────────────────────────────────

    [Fact]
    public void GameEvent_SerializedData_HasJsonIgnore()
    {
        var gameEvent = new GameEvent
        {
            Type = "card_played",
            Data = new { card = "bash" },
            SerializedData = "{\"huge\":\"payload\"}",
        };

        var json = JsonSerializer.Serialize(gameEvent, JsonOptions);

        // SerializedData has [JsonIgnore], must not appear
        Assert.DoesNotContain("serializedData", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"type\":", json);
        Assert.Contains("\"data\":", json);
    }

    [Fact]
    public void GameEvent_DefaultValues_AreCorrect()
    {
        var gameEvent = new GameEvent();

        Assert.Equal("", gameEvent.Type);
        Assert.Null(gameEvent.Data);
        Assert.Null(gameEvent.SerializedData);
    }

    // ─── ScreenType Constants ────────────────────────────────────────

    [Fact]
    public void ScreenType_AllConstantsHaveExpectedValues()
    {
        Assert.Equal("main_menu", ScreenType.MainMenu);
        Assert.Equal("map", ScreenType.Map);
        Assert.Equal("combat", ScreenType.Combat);
        Assert.Equal("card_reward", ScreenType.CardReward);
        Assert.Equal("shop", ScreenType.Shop);
        Assert.Equal("rest", ScreenType.Rest);
        Assert.Equal("event", ScreenType.Event);
        Assert.Equal("boss_reward", ScreenType.BossReward);
        Assert.Equal("game_over", ScreenType.GameOver);
        Assert.Equal("victory", ScreenType.Victory);
        Assert.Equal("chest", ScreenType.Chest);
        Assert.Equal("grid_select", ScreenType.GridSelect);
        Assert.Equal("hand_select", ScreenType.HandSelect);
    }

    [Fact]
    public void ScreenType_AllConstantsUseSnakeCase()
    {
        var constants = new[]
        {
            ScreenType.MainMenu, ScreenType.Map, ScreenType.Combat,
            ScreenType.CardReward, ScreenType.Shop, ScreenType.Rest,
            ScreenType.Event, ScreenType.BossReward, ScreenType.GameOver,
            ScreenType.Victory, ScreenType.Chest, ScreenType.GridSelect,
            ScreenType.HandSelect,
        };

        foreach (var constant in constants)
        {
            // snake_case: only lowercase letters and underscores
            Assert.Matches("^[a-z_]+$", constant);
        }
    }

    [Fact]
    public void GameState_DefaultScreen_IsMainMenu()
    {
        var state = new GameState();
        Assert.Equal(ScreenType.MainMenu, state.Screen);
    }
}
