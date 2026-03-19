using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Godot;

namespace SpireSenseMod.Data;

/// <summary>
/// Utility to discover game types at runtime for decompilation reference.
/// Iterates loaded assemblies and finds classes matching STS2 patterns.
/// Only runs when Plugin.DebugMode is true.
/// </summary>
public static class TypeDiscovery
{
    private static readonly string[] TargetPatterns =
    {
        "Card", "Combat", "Monster", "Creature", "Relic", "Deck",
        "Map", "Potion", "Power", "Buff", "Intent", "Turn",
        "Player", "Character", "Run", "Floor", "Shop", "Event",
        "Reward", "Energy", "Health", "Block",
    };

    private static readonly string[] ExcludeNamespaces =
    {
        "System", "Microsoft", "Godot", "HarmonyLib", "Mono",
        "SpireSenseMod", "Newtonsoft",
    };

    public static void DiscoverAndLog()
    {
        GD.Print("[SpireSense TypeDiscovery] Scanning loaded assemblies...");

        var results = new List<DiscoveredType>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var assemblyName = assembly.GetName().Name ?? "";

            if (ExcludeNamespaces.Any(ns => assemblyName.StartsWith(ns, StringComparison.OrdinalIgnoreCase)))
                continue;

            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Namespace != null &&
                        ExcludeNamespaces.Any(ns => type.Namespace.StartsWith(ns, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var typeName = type.Name;
                    var matchedPatterns = TargetPatterns
                        .Where(p => typeName.Contains(p, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (matchedPatterns.Count == 0)
                        continue;

                    var discovered = new DiscoveredType
                    {
                        Assembly = assemblyName,
                        Namespace = type.Namespace ?? "(global)",
                        Name = typeName,
                        FullName = type.FullName ?? typeName,
                        IsClass = type.IsClass,
                        IsEnum = type.IsEnum,
                        IsInterface = type.IsInterface,
                        MatchedPatterns = matchedPatterns,
                        Fields = GetFieldNames(type),
                        Properties = GetPropertyNames(type),
                        Methods = GetMethodNames(type),
                    };

                    results.Add(discovered);

                    GD.Print($"[SpireSense TypeDiscovery] Found: {discovered.FullName} ({string.Join(", ", matchedPatterns)})");
                }
            }
            catch (ReflectionTypeLoadException)
            {
                GD.Print($"[SpireSense TypeDiscovery] Skipped assembly (load error): {assemblyName}");
            }
        }

        GD.Print($"[SpireSense TypeDiscovery] Total types found: {results.Count}");

        // Write to JSON file for reference
        try
        {
            var outputPath = Path.Combine(
                OS.GetUserDataDir(),
                "spiresense_type_discovery.json"
            );
            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            File.WriteAllText(outputPath, json);
            GD.Print($"[SpireSense TypeDiscovery] Results saved to: {outputPath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireSense TypeDiscovery] Failed to save results: {ex.Message}");
        }
    }

    private static List<string> GetFieldNames(Type type)
    {
        try
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Select(f => $"{(f.IsPublic ? "public" : "private")} {f.FieldType.Name} {f.Name}")
                .Take(50)
                .ToList();
        }
        catch { return new List<string>(); }
    }

    private static List<string> GetPropertyNames(Type type)
    {
        try
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Select(p => $"{p.PropertyType.Name} {p.Name}")
                .Take(50)
                .ToList();
        }
        catch { return new List<string>(); }
    }

    private static List<string> GetMethodNames(Type type)
    {
        try
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => $"{(m.IsPublic ? "public" : "private")} {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                .Take(30)
                .ToList();
        }
        catch { return new List<string>(); }
    }

    private class DiscoveredType
    {
        public string Assembly { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public bool IsClass { get; set; }
        public bool IsEnum { get; set; }
        public bool IsInterface { get; set; }
        public List<string> MatchedPatterns { get; set; } = new();
        public List<string> Fields { get; set; } = new();
        public List<string> Properties { get; set; } = new();
        public List<string> Methods { get; set; } = new();
    }
}
