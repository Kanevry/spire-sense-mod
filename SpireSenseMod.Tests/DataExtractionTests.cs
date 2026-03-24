using System.Collections.Concurrent;
using System.Text.Json;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests;

/// <summary>
/// Tests for mod data extraction fixes:
/// - CardInfo: costUpgraded, descriptionUpgraded, tags fields
/// - ConcurrentDictionary thread safety (_dumpedTypes pattern)
/// - Volatile field pattern (CurrentCombatState)
/// - PileType enum string comparison (DeckPatch pattern)
///
/// Note: GameStateApi.ExtractCardInfo, DeckPatch, and CardRewardPatch cannot be
/// unit tested directly because they depend on Harmony Traverse + Godot runtime.
/// These tests validate the data contracts and concurrency patterns those classes rely on.
/// </summary>
public class DataExtractionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    // ── CardInfo: costUpgraded extraction ─────────────────────────────

    [Fact]
    public void CardInfo_CostUpgraded_DefaultsToZero()
    {
        var card = new CardInfo();

        Assert.Equal(0, card.CostUpgraded);
    }

    [Fact]
    public void CardInfo_CostUpgraded_DifferentFromBaseCost()
    {
        // Simulates a card where upgrade reduces cost (e.g., Armaments: 1 -> 1, but
        // some cards like Blur: 1 -> 0)
        var card = new CardInfo
        {
            Id = "blur",
            Name = "Blur",
            Cost = 1,
            CostUpgraded = 0,
        };

        Assert.Equal(1, card.Cost);
        Assert.Equal(0, card.CostUpgraded);
    }

    [Fact]
    public void CardInfo_CostUpgraded_SerializesCorrectly()
    {
        var card = new CardInfo
        {
            Id = "blur",
            Name = "Blur",
            Cost = 1,
            CostUpgraded = 0,
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("cost").GetInt32());
        Assert.Equal(0, root.GetProperty("costUpgraded").GetInt32());
    }

    [Fact]
    public void CardInfo_CostUpgraded_SameAsBaseCost_WhenNoUpgradeCostChange()
    {
        // Most cards don't change cost on upgrade (e.g., Strike: 1 -> 1)
        var card = new CardInfo
        {
            Id = "strike",
            Name = "Strike",
            Cost = 1,
            CostUpgraded = 1,
        };

        Assert.Equal(card.Cost, card.CostUpgraded);
    }

    [Fact]
    public void CardInfo_CostUpgraded_NegativeValues_HandledGracefully()
    {
        // X-cost cards may have special values
        var card = new CardInfo
        {
            Id = "whirlwind",
            Name = "Whirlwind",
            Cost = -1,
            CostUpgraded = -1,
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CardInfo>(json, JsonOptions)!;

        Assert.Equal(-1, deserialized.Cost);
        Assert.Equal(-1, deserialized.CostUpgraded);
    }

    // ── CardInfo: descriptionUpgraded extraction ──────────────────────

    [Fact]
    public void CardInfo_DescriptionUpgraded_DefaultsToEmpty()
    {
        var card = new CardInfo();

        Assert.Equal("", card.DescriptionUpgraded);
    }

    [Fact]
    public void CardInfo_DescriptionUpgraded_ContainsUpgradedText()
    {
        var card = new CardInfo
        {
            Id = "strike",
            Name = "Strike",
            Description = "Deal 6 damage.",
            DescriptionUpgraded = "Deal 9 damage.",
        };

        Assert.NotEqual(card.Description, card.DescriptionUpgraded);
        Assert.Equal("Deal 9 damage.", card.DescriptionUpgraded);
    }

    [Fact]
    public void CardInfo_DescriptionUpgraded_RoundtripPreserved()
    {
        var card = new CardInfo
        {
            Id = "bash",
            Name = "Bash",
            Description = "Deal 8 damage. Apply 2 Vulnerable.",
            DescriptionUpgraded = "Deal 10 damage. Apply 3 Vulnerable.",
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CardInfo>(json, JsonOptions)!;

        Assert.Equal(card.DescriptionUpgraded, deserialized.DescriptionUpgraded);
    }

    [Fact]
    public void CardInfo_DescriptionUpgraded_EmptyWhenMethodNotAvailable()
    {
        // GameStateApi.ExtractCardInfo leaves descriptionUpgraded empty when
        // GetDescriptionForUpgradePreview() is not available on the CardModel.
        // Verify the default stays empty and serializes as empty string.
        var card = new CardInfo
        {
            Id = "curse_of_the_bell",
            Name = "Curse of the Bell",
            Description = "Unplayable. Ethereal.",
            DescriptionUpgraded = "",
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("", doc.RootElement.GetProperty("descriptionUpgraded").GetString());
    }

    // ── CardInfo: tags extraction ─────────────────────────────────────

    [Fact]
    public void CardInfo_Tags_DefaultsToEmptyList()
    {
        var card = new CardInfo();

        Assert.NotNull(card.Tags);
        Assert.Empty(card.Tags);
    }

    [Fact]
    public void CardInfo_Tags_CanContainMultipleTags()
    {
        var card = new CardInfo
        {
            Id = "pommel_strike",
            Name = "Pommel Strike",
            Tags = new() { "draw", "damage" },
        };

        Assert.Equal(2, card.Tags.Count);
        Assert.Contains("draw", card.Tags);
        Assert.Contains("damage", card.Tags);
    }

    [Fact]
    public void CardInfo_Tags_LowercaseConvention()
    {
        // GameStateApi.ExtractCardInfo converts tags to lowercase via .ToLowerInvariant()
        // Verify the model preserves lowercase values
        var card = new CardInfo
        {
            Id = "test_card",
            Name = "Test",
            Tags = new() { "innate", "ethereal", "exhaust", "retain" },
        };

        foreach (var tag in card.Tags)
        {
            Assert.Equal(tag, tag.ToLowerInvariant());
        }
    }

    [Fact]
    public void CardInfo_Tags_SerializeAsJsonArray()
    {
        var card = new CardInfo
        {
            Id = "inflame",
            Name = "Inflame",
            Tags = new() { "strength", "power" },
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var tagsElement = doc.RootElement.GetProperty("tags");

        Assert.Equal(JsonValueKind.Array, tagsElement.ValueKind);
        Assert.Equal(2, tagsElement.GetArrayLength());
        Assert.Equal("strength", tagsElement[0].GetString());
        Assert.Equal("power", tagsElement[1].GetString());
    }

    [Fact]
    public void CardInfo_Tags_EmptyList_SerializesAsEmptyArray()
    {
        var card = new CardInfo
        {
            Id = "slimed",
            Name = "Slimed",
            Tags = new(),
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(0, doc.RootElement.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public void CardInfo_Tags_FilterNone_Convention()
    {
        // GameStateApi.ExtractCardInfo filters out "None" tags.
        // Verify the model correctly stores only non-None tags.
        var card = new CardInfo
        {
            Id = "test",
            Name = "Test",
            Tags = new() { "innate" }, // "None" should have been filtered by extraction
        };

        Assert.DoesNotContain("None", card.Tags);
        Assert.DoesNotContain("none", card.Tags);
        Assert.Single(card.Tags);
    }

    [Fact]
    public void CardInfo_Tags_CombineTagsAndKeywords()
    {
        // GameStateApi.ExtractCardInfo merges both Tags and Keywords into a single list.
        // Verify a combined list serializes correctly.
        var card = new CardInfo
        {
            Id = "burning_pact",
            Name = "Burning Pact",
            Tags = new() { "exhaust", "draw", "innate" }, // tags + keywords merged
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CardInfo>(json, JsonOptions)!;

        Assert.Equal(3, deserialized.Tags.Count);
        Assert.Equal(card.Tags, deserialized.Tags);
    }

    // ── Full CardInfo contract: all new fields together ───────────────

    [Fact]
    public void CardInfo_FullExtraction_AllNewFieldsPresent()
    {
        // Simulates the full output of GameStateApi.ExtractCardInfo
        // with all the fixed/new fields populated
        var card = new CardInfo
        {
            Id = "heavy_blade",
            Name = "Heavy Blade",
            Character = "ironclad",
            Type = "attack",
            Rarity = "common",
            Cost = 2,
            CostUpgraded = 2,
            Description = "Deal 14 damage. Strength affects this card 3 times.",
            DescriptionUpgraded = "Deal 14 damage. Strength affects this card 5 times.",
            Upgraded = false,
            Tags = new() { "strength", "scaling" },
        };

        var json = JsonSerializer.Serialize(card, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify every field is present and correctly named
        Assert.Equal("heavy_blade", root.GetProperty("id").GetString());
        Assert.Equal("Heavy Blade", root.GetProperty("name").GetString());
        Assert.Equal("ironclad", root.GetProperty("character").GetString());
        Assert.Equal("attack", root.GetProperty("type").GetString());
        Assert.Equal("common", root.GetProperty("rarity").GetString());
        Assert.Equal(2, root.GetProperty("cost").GetInt32());
        Assert.Equal(2, root.GetProperty("costUpgraded").GetInt32());
        Assert.Equal("Deal 14 damage. Strength affects this card 3 times.", root.GetProperty("description").GetString());
        Assert.Equal("Deal 14 damage. Strength affects this card 5 times.", root.GetProperty("descriptionUpgraded").GetString());
        Assert.False(root.GetProperty("upgraded").GetBoolean());
        Assert.Equal(2, root.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public void CardInfo_UpgradedCard_CostUpgradedDiffersFromCost()
    {
        // Simulates an upgraded card where the cost actually changed
        var card = new CardInfo
        {
            Id = "blind",
            Name = "Blind+",
            Cost = 2,
            CostUpgraded = 1,
            Upgraded = true,
            Description = "Apply 2 Weak.",
            DescriptionUpgraded = "Apply 2 Weak.",
        };

        Assert.True(card.Upgraded);
        Assert.NotEqual(card.Cost, card.CostUpgraded);
        Assert.True(card.CostUpgraded < card.Cost);
    }

    // ── ConcurrentDictionary thread safety (DumpObjectOnce pattern) ───

    [Fact]
    public void ConcurrentDictionary_TryAdd_OnlySucceedsOnce()
    {
        // Validates the pattern used by GameStateApi._dumpedTypes
        var dict = new ConcurrentDictionary<string, byte>();

        var firstAdd = dict.TryAdd("TestType", 0);
        var secondAdd = dict.TryAdd("TestType", 0);

        Assert.True(firstAdd);
        Assert.False(secondAdd);
    }

    [Fact]
    public void ConcurrentDictionary_TryAdd_DifferentKeys_AllSucceed()
    {
        var dict = new ConcurrentDictionary<string, byte>();

        Assert.True(dict.TryAdd("CardModel", 0));
        Assert.True(dict.TryAdd("RelicModel", 0));
        Assert.True(dict.TryAdd("MonsterModel", 0));
    }

    [Fact]
    public async Task ConcurrentDictionary_ConcurrentAdds_ThreadSafe()
    {
        // Simulates the concurrent access pattern in GameStateApi._dumpedTypes
        var dict = new ConcurrentDictionary<string, byte>();
        var successCount = 0;
        var tasks = new List<Task>();

        // 10 threads all try to add the same key
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (dict.TryAdd("SharedType", 0))
                {
                    Interlocked.Increment(ref successCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Exactly one should succeed
        Assert.Equal(1, successCount);
    }

    [Fact]
    public async Task ConcurrentDictionary_ManyDifferentKeys_AllRecorded()
    {
        // Simulates many different types being dumped concurrently
        var dict = new ConcurrentDictionary<string, byte>();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var typeName = $"Type_{i}";
            tasks.Add(Task.Run(() => dict.TryAdd(typeName, 0)));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(100, dict.Count);
    }

    // ── Volatile field pattern (CurrentCombatState) ───────────────────

    [Fact]
    public async Task VolatileField_CrossThreadVisibility()
    {
        // Tests the pattern used by GameStateApi.CurrentCombatState:
        // volatile ensures writes are visible to other threads without a lock.
        // We simulate this with a simple volatile wrapper.
        var holder = new VolatileHolder();

        Assert.Null(holder.Value);

        // Write on one thread, read on another
        var sentinel = new object();
        var writeTask = Task.Run(() => holder.Value = sentinel);
        await writeTask;

        object? readValue = null;
        var readTask = Task.Run(() => readValue = holder.Value);
        await readTask;

        Assert.Same(sentinel, readValue);
    }

    [Fact]
    public async Task VolatileField_SetAndClear_Pattern()
    {
        // Simulates the pattern: set CurrentCombatState when combat starts,
        // clear it (set null) when combat ends
        var holder = new VolatileHolder();

        var combatState = new object();
        holder.Value = combatState;
        Assert.NotNull(holder.Value);

        // Clear on combat end
        holder.Value = null;

        object? readValue = null;
        await Task.Run(() => readValue = holder.Value);
        Assert.Null(readValue);
    }

    [Fact]
    public async Task VolatileField_ConcurrentReadsAndWrites_NoExceptions()
    {
        var holder = new VolatileHolder();
        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        // Writers
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        holder.Value = new object();
                        holder.Value = null;
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }));
        }

        // Readers
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var _ = holder.Value;
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }

    // ── PileType enum string comparison (DeckPatch pattern) ───────────

    [Fact]
    public void PileType_EnumToString_DeckComparison()
    {
        // DeckPatch.OnCardAdded uses arg.ToString() != "Deck" for PileType enum comparison.
        // This validates the pattern works correctly for all STS2 PileType members.
        // PileType enum: None, Draw, Hand, Discard, Exhaust, Play, Deck
        var pileTypes = new Dictionary<string, bool>
        {
            { "None", false },
            { "Draw", false },
            { "Hand", false },
            { "Discard", false },
            { "Exhaust", false },
            { "Play", false },
            { "Deck", true },
        };

        foreach (var (pileTypeStr, shouldTrack) in pileTypes)
        {
            // Mirrors the DeckPatch comparison logic
            var isDeck = pileTypeStr == "Deck";
            Assert.Equal(shouldTrack, isDeck);
        }
    }

    [Fact]
    public void PileType_QualifiedEnumName_EndsWith_Fallback()
    {
        // DeckPatch.OnRunStart uses pileTypeStr.EndsWith(".Deck") as fallback
        // for when ToString() returns the fully-qualified enum name
        var qualifiedPileTypes = new[]
        {
            ("MegaCrit.Sts2.Core.Entities.Cards.PileType.Deck", true),
            ("PileType.Deck", true),
            ("Deck", false), // EndsWith(".Deck") won't match "Deck" — but == "Deck" catches it
            ("PileType.Draw", false),
            ("PileType.Discard", false),
        };

        foreach (var (pileTypeStr, shouldMatchEndsWith) in qualifiedPileTypes)
        {
            var matchesEndsWith = pileTypeStr.EndsWith(".Deck");
            Assert.Equal(shouldMatchEndsWith, matchesEndsWith);
        }
    }

    [Fact]
    public void PileType_CombinedCheck_MatchesDeckInAllForms()
    {
        // The actual pattern from DeckPatch.OnRunStart:
        // if (pileTypeStr == "Deck" || pileTypeStr.EndsWith(".Deck"))
        var testCases = new[]
        {
            ("Deck", true),
            ("MegaCrit.Sts2.Core.Entities.Cards.PileType.Deck", true),
            ("PileType.Deck", true),
            ("Draw", false),
            ("Hand", false),
            ("Discard", false),
            ("Exhaust", false),
            ("PileType.Draw", false),
            ("", false),
        };

        foreach (var (pileTypeStr, expectedIsDeck) in testCases)
        {
            var isDeck = pileTypeStr == "Deck" || pileTypeStr.EndsWith(".Deck");
            Assert.Equal(expectedIsDeck, isDeck);
        }
    }

    // ── CardRewardPatch: static method parameter extraction pattern ───

    [Fact]
    public void CardRewardPatch_NullParameterFallback_Pattern()
    {
        // CardRewardPatch.OnCardRewardsShown uses: object? cards = __0;
        // If __0 is null, it falls back to __result._options field.
        // This tests the null-coalescing pattern.
        object? primary = null;
        object? fallback = new List<CardInfo> { new() { Id = "strike", Name = "Strike" } };

        // Primary: read from __0 parameter
        object? cards = primary;

        // Fallback: read from result field
        if (cards == null)
        {
            cards = fallback;
        }

        Assert.NotNull(cards);
        Assert.IsType<List<CardInfo>>(cards);
        Assert.Single((List<CardInfo>)cards);
    }

    [Fact]
    public void CardRewardPatch_IEnumerable_Iteration_Pattern()
    {
        // CardRewardPatch iterates over cards as System.Collections.IEnumerable
        // and builds a List<CardInfo> from each item.
        var sourceCards = new List<object>
        {
            new CardInfo { Id = "strike", Name = "Strike" },
            new CardInfo { Id = "defend", Name = "Defend" },
            new CardInfo { Id = "bash", Name = "Bash" },
        };

        var result = new List<CardInfo>();
        if (sourceCards is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is CardInfo cardInfo)
                {
                    result.Add(cardInfo);
                }
            }
        }

        Assert.Equal(3, result.Count);
        Assert.Equal("strike", result[0].Id);
        Assert.Equal("defend", result[1].Id);
        Assert.Equal("bash", result[2].Id);
    }

    [Fact]
    public void CardRewardPatch_NullItems_SkippedGracefully()
    {
        // CardRewardPatch skips null items: if (item == null) continue;
        var sourceCards = new List<CardInfo?>
        {
            new() { Id = "strike", Name = "Strike" },
            null,
            new() { Id = "bash", Name = "Bash" },
        };

        var result = new List<CardInfo>();
        foreach (var item in sourceCards)
        {
            if (item == null) continue;
            result.Add(item);
        }

        Assert.Equal(2, result.Count);
    }

    // ── Helper class for volatile pattern testing ─────────────────────

    private class VolatileHolder
    {
        private volatile object? _value;

        public object? Value
        {
            get => _value;
            set => _value = value;
        }
    }
}
