using System.Text.Json;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests.Models;

public class RestOptionSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [Fact]
    public void Serialize_DefaultRestOption_HasCorrectDefaults()
    {
        var option = new RestOption();

        Assert.Equal("", option.Id);
        Assert.Equal("", option.Name);
        Assert.Equal("", option.Description);
        Assert.True(option.Enabled);
    }

    [Fact]
    public void Serialize_FullyPopulatedRestOption_MatchesExpectedFormat()
    {
        var option = new RestOption
        {
            Id = "rest",
            Name = "Rest",
            Description = "Heal 30% of max HP.",
            Enabled = true,
        };

        var json = JsonSerializer.Serialize(option, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("rest", root.GetProperty("id").GetString());
        Assert.Equal("Rest", root.GetProperty("name").GetString());
        Assert.Equal("Heal 30% of max HP.", root.GetProperty("description").GetString());
        Assert.True(root.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Serialize_DisabledRestOption_PreservesEnabledFalse()
    {
        var option = new RestOption
        {
            Id = "smith",
            Name = "Smith",
            Description = "Upgrade a card.",
            Enabled = false,
        };

        var json = JsonSerializer.Serialize(option, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Roundtrip_RestOption_PreservesAllFields()
    {
        var option = new RestOption
        {
            Id = "lift",
            Name = "Lift",
            Description = "Permanently gain 1 Strength.",
            Enabled = true,
        };

        var json = JsonSerializer.Serialize(option, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RestOption>(json, JsonOptions)!;

        Assert.Equal(option.Id, deserialized.Id);
        Assert.Equal(option.Name, deserialized.Name);
        Assert.Equal(option.Description, deserialized.Description);
        Assert.Equal(option.Enabled, deserialized.Enabled);
    }

    [Fact]
    public void JsonPropertyNames_MatchWebAppExpectedFormat()
    {
        var option = new RestOption { Id = "test", Name = "Test" };

        var json = JsonSerializer.Serialize(option, JsonOptions);

        Assert.Contains("\"id\":", json);
        Assert.Contains("\"name\":", json);
        Assert.Contains("\"description\":", json);
        Assert.Contains("\"enabled\":", json);

        // Ensure no PascalCase leaks through
        Assert.DoesNotContain("\"Id\":", json);
        Assert.DoesNotContain("\"Name\":", json);
        Assert.DoesNotContain("\"Description\":", json);
        Assert.DoesNotContain("\"Enabled\":", json);
    }
}
