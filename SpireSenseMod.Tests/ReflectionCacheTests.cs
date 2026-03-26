using System.Collections.Generic;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests;

/// <summary>
/// Tests for the reflection cache (MOD-004) extracted into ReflectionCache.cs.
/// Validates cached GetProp/GetField lookups, null safety, missing member handling,
/// and cache population behavior.
/// </summary>
public class ReflectionCacheTests : IDisposable
{
    public ReflectionCacheTests()
    {
        // Clear caches before each test to ensure isolation
        ReflectionCache.ClearCaches();
    }

    public void Dispose()
    {
        ReflectionCache.ClearCaches();
    }

    // ── Test target class ───────────────────────────────────────────────

    private class TestTarget
    {
        public string Name { get; set; } = "test";
        public int Value { get; set; } = 42;
        private string _secret = "hidden";
        public string Secret => _secret;
    }

    private class DerivedTarget : TestTarget
    {
        public string Extra { get; set; } = "derived";
    }

    // ── GetProp: property lookup ────────────────────────────────────────

    [Fact]
    public void GetProp_KnownProperty_ReturnsCorrectValue()
    {
        var target = new TestTarget();

        var name = ReflectionCache.GetProp(target, "Name");
        var value = ReflectionCache.GetProp(target, "Value");

        Assert.Equal("test", name);
        Assert.Equal(42, value);
    }

    [Fact]
    public void GetProp_NullObject_ReturnsNull()
    {
        var result = ReflectionCache.GetProp(null, "Name");

        Assert.Null(result);
    }

    [Fact]
    public void GetProp_NonExistentProperty_ReturnsNull()
    {
        var target = new TestTarget();

        var result = ReflectionCache.GetProp(target, "DoesNotExist");

        Assert.Null(result);
    }

    [Fact]
    public void GetProp_MutatedValue_ReturnsUpdatedValue()
    {
        var target = new TestTarget { Name = "original" };
        Assert.Equal("original", ReflectionCache.GetProp(target, "Name"));

        target.Name = "updated";
        Assert.Equal("updated", ReflectionCache.GetProp(target, "Name"));
    }

    [Fact]
    public void GetProp_DerivedType_ReturnsBaseProperty()
    {
        var target = new DerivedTarget { Name = "base_val" };

        Assert.Equal("base_val", ReflectionCache.GetProp(target, "Name"));
        Assert.Equal("derived", ReflectionCache.GetProp(target, "Extra"));
    }

    [Fact]
    public void GetProp_ReadOnlyProperty_ReturnsValue()
    {
        // Secret is a get-only property backed by private field _secret
        var target = new TestTarget();

        var result = ReflectionCache.GetProp(target, "Secret");

        Assert.Equal("hidden", result);
    }

    // ── GetField: field lookup ──────────────────────────────────────────

    [Fact]
    public void GetField_KnownPrivateField_ReturnsCorrectValue()
    {
        var target = new TestTarget();

        var result = ReflectionCache.GetField(target, "_secret");

        Assert.Equal("hidden", result);
    }

    [Fact]
    public void GetField_NullObject_ReturnsNull()
    {
        var result = ReflectionCache.GetField(null, "_secret");

        Assert.Null(result);
    }

    [Fact]
    public void GetField_NonExistentField_ReturnsNull()
    {
        var target = new TestTarget();

        var result = ReflectionCache.GetField(target, "_doesNotExist");

        Assert.Null(result);
    }

    [Fact]
    public void GetField_PublicBackingField_AccessedViaBindingFlags()
    {
        // GetField uses BindingFlags.Public | NonPublic | Instance,
        // so it should find auto-property backing fields too
        var target = new TestTarget();

        // Auto-property backing field for Name (compiler-generated)
        var backingField = ReflectionCache.GetField(target, "<Name>k__BackingField");

        Assert.Equal("test", backingField);
    }

    // ── GetCollection ───────────────────────────────────────────────────

    private class CollectionTarget
    {
        public List<string> Items { get; set; } = new() { "a", "b", "c" };
        private List<int> _numbers = new() { 1, 2, 3 };
    }

    [Fact]
    public void GetCollection_PropertyExists_ReturnsEnumerable()
    {
        var target = new CollectionTarget();

        var result = ReflectionCache.GetCollection(target, "Items");

        Assert.NotNull(result);
        var items = new List<string>();
        foreach (var item in result!)
        {
            items.Add((string)item);
        }
        Assert.Equal(new[] { "a", "b", "c" }, items);
    }

    [Fact]
    public void GetCollection_PropertyMissing_FallsBackToField()
    {
        var target = new CollectionTarget();

        var result = ReflectionCache.GetCollection(target, "NonExistent", "_numbers");

        Assert.NotNull(result);
        var numbers = new List<int>();
        foreach (var item in result!)
        {
            numbers.Add((int)item);
        }
        Assert.Equal(new[] { 1, 2, 3 }, numbers);
    }

    [Fact]
    public void GetCollection_NullObject_ReturnsNull()
    {
        var result = ReflectionCache.GetCollection(null, "Items");

        Assert.Null(result);
    }

    [Fact]
    public void GetCollection_BothMissing_NoFallback_ReturnsNull()
    {
        var target = new CollectionTarget();

        var result = ReflectionCache.GetCollection(target, "NonExistent");

        Assert.Null(result);
    }

    // ── Caching behavior ────────────────────────────────────────────────

    [Fact]
    public void Cache_GetProp_PopulatesCacheEntry()
    {
        var target = new TestTarget();

        // Cache should be empty initially
        Assert.Equal(0, ReflectionCache.PropCacheCount);

        ReflectionCache.GetProp(target, "Name");

        // Cache should now have an entry for (TestTarget, "Name")
        Assert.True(ReflectionCache.PropCacheContains(typeof(TestTarget), "Name"));
        Assert.Equal(1, ReflectionCache.PropCacheCount);
    }

    [Fact]
    public void Cache_GetField_PopulatesCacheEntry()
    {
        var target = new TestTarget();

        Assert.Equal(0, ReflectionCache.FieldCacheCount);

        ReflectionCache.GetField(target, "_secret");

        Assert.True(ReflectionCache.FieldCacheContains(typeof(TestTarget), "_secret"));
        Assert.Equal(1, ReflectionCache.FieldCacheCount);
    }

    [Fact]
    public void Cache_RepeatedGetProp_DoesNotGrowCache()
    {
        var target = new TestTarget();

        ReflectionCache.GetProp(target, "Name");
        Assert.Equal(1, ReflectionCache.PropCacheCount);

        // Second call with same type + name should reuse cached entry
        ReflectionCache.GetProp(target, "Name");
        Assert.Equal(1, ReflectionCache.PropCacheCount);

        // Third call with a different instance of the same type
        var target2 = new TestTarget { Name = "other" };
        ReflectionCache.GetProp(target2, "Name");
        Assert.Equal(1, ReflectionCache.PropCacheCount);
    }

    [Fact]
    public void Cache_DifferentProperties_CreateSeparateEntries()
    {
        var target = new TestTarget();

        ReflectionCache.GetProp(target, "Name");
        ReflectionCache.GetProp(target, "Value");

        Assert.Equal(2, ReflectionCache.PropCacheCount);
        Assert.True(ReflectionCache.PropCacheContains(typeof(TestTarget), "Name"));
        Assert.True(ReflectionCache.PropCacheContains(typeof(TestTarget), "Value"));
    }

    [Fact]
    public void Cache_NonExistentProperty_StillCached()
    {
        // Even a miss should be cached (null PropertyInfo) to avoid repeated lookups
        var target = new TestTarget();

        ReflectionCache.GetProp(target, "NonExistent");

        Assert.Equal(1, ReflectionCache.PropCacheCount);
        Assert.True(ReflectionCache.PropCacheContains(typeof(TestTarget), "NonExistent"));
    }

    [Fact]
    public void Cache_DerivedType_CachedSeparatelyFromBase()
    {
        var baseTarget = new TestTarget();
        var derivedTarget = new DerivedTarget();

        ReflectionCache.GetProp(baseTarget, "Name");
        ReflectionCache.GetProp(derivedTarget, "Name");

        // Should have 2 entries: (TestTarget, "Name") and (DerivedTarget, "Name")
        Assert.Equal(2, ReflectionCache.PropCacheCount);
        Assert.True(ReflectionCache.PropCacheContains(typeof(TestTarget), "Name"));
        Assert.True(ReflectionCache.PropCacheContains(typeof(DerivedTarget), "Name"));
    }

    [Fact]
    public void Cache_NullObject_DoesNotPopulateCache()
    {
        ReflectionCache.GetProp(null, "Name");
        ReflectionCache.GetField(null, "_secret");

        Assert.Equal(0, ReflectionCache.PropCacheCount);
        Assert.Equal(0, ReflectionCache.FieldCacheCount);
    }

    // ── Thread safety ───────────────────────────────────────────────────

    [Fact]
    public async Task Cache_ConcurrentGetProp_ThreadSafe()
    {
        var targets = new List<TestTarget>();
        for (int i = 0; i < 100; i++)
        {
            targets.Add(new TestTarget { Name = $"target_{i}" });
        }

        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        for (int i = 0; i < 100; i++)
        {
            var target = targets[i];
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var result = ReflectionCache.GetProp(target, "Name");
                    Assert.Equal(target.Name, result);
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
        // All calls used the same type + name, so only 1 cache entry
        Assert.Equal(1, ReflectionCache.PropCacheCount);
    }

    [Fact]
    public async Task Cache_ConcurrentGetField_ThreadSafe()
    {
        var targets = new List<TestTarget>();
        for (int i = 0; i < 50; i++)
        {
            targets.Add(new TestTarget());
        }

        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        for (int i = 0; i < 50; i++)
        {
            var target = targets[i];
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var result = ReflectionCache.GetField(target, "_secret");
                    Assert.Equal("hidden", result);
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
        Assert.Equal(1, ReflectionCache.FieldCacheCount);
    }
}
