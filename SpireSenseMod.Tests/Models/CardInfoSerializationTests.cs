using System.Text.Json;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests.Models;

public class CardInfoSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [Fact]
    public void Serialize_FullyPopulatedCard_MatchesExpectedFormat()
    {
        var card = new CardInfo
        {
            Id = "pommel_strike",
            Name = "Pommel Strike",
            Character = "ironclad",
            Type = "attack",
            Rarity = "common",
            Cost = 1,
            CostUpgraded = 1,
            Description = "Deal 9 damage. Draw 1 card.",
            DescriptionUpgraded = "Deal 10 damage. Draw 2 cards.",
            Upgraded = false,
            Tags = new() { "draw", "damage" },
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("pommel_strike", root.GetProperty("id").GetString());
        Assert.Equal("Pommel Strike", root.GetProperty("name").GetString());
        Assert.Equal("ironclad", root.GetProperty("character").GetString());
        Assert.Equal("attack", root.GetProperty("type").GetString());
        Assert.Equal("common", root.GetProperty("rarity").GetString());
        Assert.Equal(1, root.GetProperty("cost").GetInt32());
        Assert.Equal(1, root.GetProperty("costUpgraded").GetInt32());
        Assert.Equal("Deal 9 damage. Draw 1 card.", root.GetProperty("description").GetString());
        Assert.Equal("Deal 10 damage. Draw 2 cards.", root.GetProperty("descriptionUpgraded").GetString());
        Assert.False(root.GetProperty("upgraded").GetBoolean());
        Assert.Equal(2, root.GetProperty("tags").GetArrayLength());
        Assert.Equal("draw", root.GetProperty("tags")[0].GetString());
        Assert.Equal("damage", root.GetProperty("tags")[1].GetString());
    }

    [Fact]
    public void Serialize_DefaultCard_HasCorrectDefaults()
    {
        var card = new CardInfo();

        var json = JsonSerializer.Serialize(card, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("", root.GetProperty("id").GetString());
        Assert.Equal("", root.GetProperty("name").GetString());
        Assert.Equal("neutral", root.GetProperty("character").GetString());
        Assert.Equal("attack", root.GetProperty("type").GetString());
        Assert.Equal("common", root.GetProperty("rarity").GetString());
        Assert.Equal(0, root.GetProperty("cost").GetInt32());
        Assert.Equal(0, root.GetProperty("costUpgraded").GetInt32());
        Assert.Equal("", root.GetProperty("description").GetString());
        Assert.Equal("", root.GetProperty("descriptionUpgraded").GetString());
        Assert.False(root.GetProperty("upgraded").GetBoolean());
        Assert.Equal(0, root.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public void Roundtrip_UpgradedCard_PreservesAllFields()
    {
        var card = new CardInfo
        {
            Id = "neutralize",
            Name = "Neutralize+",
            Character = "silent",
            Type = "attack",
            Rarity = "basic",
            Cost = 0,
            CostUpgraded = 0,
            Description = "Deal 3 damage. Apply 1 Weak.",
            DescriptionUpgraded = "Deal 4 damage. Apply 2 Weak.",
            Upgraded = true,
            Tags = new() { "weak", "starter" },
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CardInfo>(json, JsonOptions)!;

        Assert.Equal(card.Id, deserialized.Id);
        Assert.Equal(card.Name, deserialized.Name);
        Assert.Equal(card.Character, deserialized.Character);
        Assert.Equal(card.Type, deserialized.Type);
        Assert.Equal(card.Rarity, deserialized.Rarity);
        Assert.Equal(card.Cost, deserialized.Cost);
        Assert.Equal(card.CostUpgraded, deserialized.CostUpgraded);
        Assert.Equal(card.Description, deserialized.Description);
        Assert.Equal(card.DescriptionUpgraded, deserialized.DescriptionUpgraded);
        Assert.True(deserialized.Upgraded);
        Assert.Equal(card.Tags, deserialized.Tags);
    }

    [Fact]
    public void Serialize_MinimalCard_OnlyIdAndName()
    {
        var card = new CardInfo
        {
            Id = "bash",
            Name = "Bash",
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("bash", root.GetProperty("id").GetString());
        Assert.Equal("Bash", root.GetProperty("name").GetString());
        // Defaults should still be present
        Assert.Equal("neutral", root.GetProperty("character").GetString());
        Assert.Equal("attack", root.GetProperty("type").GetString());
    }

    [Fact]
    public void JsonPropertyNames_MatchWebAppExpectedFormat()
    {
        var card = new CardInfo
        {
            Id = "test",
            Name = "Test",
            CostUpgraded = 2,
            DescriptionUpgraded = "upgraded desc",
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);

        // Verify the exact JSON key names the web app expects
        Assert.Contains("\"id\":", json);
        Assert.Contains("\"name\":", json);
        Assert.Contains("\"character\":", json);
        Assert.Contains("\"type\":", json);
        Assert.Contains("\"rarity\":", json);
        Assert.Contains("\"cost\":", json);
        Assert.Contains("\"costUpgraded\":", json);
        Assert.Contains("\"description\":", json);
        Assert.Contains("\"descriptionUpgraded\":", json);
        Assert.Contains("\"upgraded\":", json);
        Assert.Contains("\"tags\":", json);

        // Ensure no PascalCase leaks through
        Assert.DoesNotContain("\"Id\":", json);
        Assert.DoesNotContain("\"Name\":", json);
        Assert.DoesNotContain("\"CostUpgraded\":", json);
        Assert.DoesNotContain("\"DescriptionUpgraded\":", json);
    }
}
