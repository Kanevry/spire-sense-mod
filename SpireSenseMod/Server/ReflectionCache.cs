using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace SpireSenseMod;

/// <summary>
/// Thread-safe reflection cache for property and field lookups (MOD-004).
/// Extracted from GameStateApi to allow standalone unit testing without Godot/Harmony.
///
/// Caches PropertyInfo/FieldInfo per (Type, name) to avoid repeated GetProperty/GetField
/// calls on the same type — critical for hot paths like per-frame game state extraction.
/// </summary>
public static class ReflectionCache
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _propCache = new();
    private static readonly ConcurrentDictionary<(Type, string), FieldInfo?> _fieldCache = new();

    /// <summary>Cached property lookup. Returns null if obj is null or property doesn't exist.</summary>
    public static object? GetProp(object? obj, string name)
    {
        if (obj == null) return null;
        var type = obj.GetType();
        var prop = _propCache.GetOrAdd((type, name), k => k.Item1.GetProperty(k.Item2));
        return prop?.GetValue(obj);
    }

    /// <summary>Cached field lookup (public + private + instance). Returns null if obj is null or field doesn't exist.</summary>
    public static object? GetField(object? obj, string name)
    {
        if (obj == null) return null;
        var type = obj.GetType();
        var field = _fieldCache.GetOrAdd((type, name), k => k.Item1.GetField(k.Item2, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        return field?.GetValue(obj);
    }

    /// <summary>Get a collection (IEnumerable) from a property, with optional field fallback.</summary>
    public static IEnumerable? GetCollection(object? obj, string propName, string? fieldFallback = null)
        => GetProp(obj, propName) as IEnumerable
            ?? (fieldFallback != null ? GetField(obj, fieldFallback) as IEnumerable : null);

    /// <summary>Number of cached property lookups. Exposed for testing.</summary>
    public static int PropCacheCount => _propCache.Count;

    /// <summary>Number of cached field lookups. Exposed for testing.</summary>
    public static int FieldCacheCount => _fieldCache.Count;

    /// <summary>Check if a (Type, name) pair is in the property cache. Exposed for testing.</summary>
    public static bool PropCacheContains(Type type, string name) => _propCache.ContainsKey((type, name));

    /// <summary>Check if a (Type, name) pair is in the field cache. Exposed for testing.</summary>
    public static bool FieldCacheContains(Type type, string name) => _fieldCache.ContainsKey((type, name));

    /// <summary>Clear both caches. For testing only.</summary>
    internal static void ClearCaches()
    {
        _propCache.Clear();
        _fieldCache.Clear();
    }
}
