using System.Text.Json;

namespace SpireSenseMod.Tests;

internal static class TestHelpers
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
