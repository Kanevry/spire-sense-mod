using System.Text.Json;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests.Models;

public class BufferedEventSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [Fact]
    public void Serialize_DefaultBufferedEvent_HasCorrectDefaults()
    {
        var entry = new BufferedEvent();

        Assert.Equal("", entry.Type);
        Assert.Null(entry.Data);
        Assert.Equal(0, entry.Timestamp);
    }

    [Fact]
    public void Serialize_FullyPopulated_MatchesExpectedFormat()
    {
        var entry = new BufferedEvent
        {
            Type = "card_played",
            Data = new { card = "strike", cost = 1 },
            Timestamp = 1711000000000,
        };

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("card_played", root.GetProperty("type").GetString());
        Assert.Equal(1711000000000, root.GetProperty("timestamp").GetInt64());
        Assert.NotEqual(JsonValueKind.Null, root.GetProperty("data").ValueKind);
    }

    [Fact]
    public void Serialize_NullData_SerializesAsNull()
    {
        var entry = new BufferedEvent
        {
            Type = "turn_end",
            Data = null,
            Timestamp = 1711000000000,
        };

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("data").ValueKind);
    }

    [Fact]
    public void JsonPropertyNames_UseCamelCase()
    {
        var entry = new BufferedEvent
        {
            Type = "test",
            Timestamp = 123456789,
        };

        var json = JsonSerializer.Serialize(entry, JsonOptions);

        Assert.Contains("\"type\":", json);
        Assert.Contains("\"data\":", json);
        Assert.Contains("\"timestamp\":", json);

        // No PascalCase leaks
        Assert.DoesNotContain("\"Type\":", json);
        Assert.DoesNotContain("\"Data\":", json);
        Assert.DoesNotContain("\"Timestamp\":", json);
    }

    [Fact]
    public void Roundtrip_WithStringData_PreservesAll()
    {
        var entry = new BufferedEvent
        {
            Type = "combat_start",
            Data = "boss_fight",
            Timestamp = 1711000000000,
        };

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<BufferedEvent>(json, JsonOptions)!;

        Assert.Equal(entry.Type, deserialized.Type);
        Assert.Equal(entry.Timestamp, deserialized.Timestamp);
        // Data is deserialized as JsonElement for anonymous/object types
        Assert.NotNull(deserialized.Data);
    }

    [Fact]
    public void Serialize_LargeTimestamp_HandledCorrectly()
    {
        var entry = new BufferedEvent
        {
            Type = "test",
            Timestamp = long.MaxValue,
        };

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(long.MaxValue, doc.RootElement.GetProperty("timestamp").GetInt64());
    }
}
