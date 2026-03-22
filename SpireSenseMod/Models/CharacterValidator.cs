using System.Collections.Generic;

namespace SpireSenseMod;

/// <summary>
/// Validates character identifiers against the known STS2 roster.
/// Returns "unknown" for unrecognized characters.
/// </summary>
public static class CharacterValidator
{
    private static readonly HashSet<string> ValidCharacters = new()
    {
        "ironclad", "silent", "defect", "regent", "necrobinder",
    };

    public static string Validate(string? character)
    {
        var lower = character?.ToLowerInvariant() ?? "unknown";
        return ValidCharacters.Contains(lower) ? lower : "unknown";
    }
}
