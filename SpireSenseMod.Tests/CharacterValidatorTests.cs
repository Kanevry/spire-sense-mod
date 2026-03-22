using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests;

public class CharacterValidatorTests
{
    // ─── Valid Characters ────────────────────────────────────────────

    [Theory]
    [InlineData("ironclad")]
    [InlineData("silent")]
    [InlineData("defect")]
    [InlineData("regent")]
    [InlineData("necrobinder")]
    [InlineData("deprived")]
    public void Validate_ValidCharacter_ReturnsSameCharacter(string character)
    {
        var result = CharacterValidator.Validate(character);

        Assert.Equal(character, result);
    }

    // ─── Case Insensitivity ──────────────────────────────────────────

    [Theory]
    [InlineData("Ironclad", "ironclad")]
    [InlineData("SILENT", "silent")]
    [InlineData("Defect", "defect")]
    [InlineData("REGENT", "regent")]
    [InlineData("NecRoBiNdEr", "necrobinder")]
    [InlineData("DEPRIVED", "deprived")]
    public void Validate_MixedCase_ReturnsLowerCase(string input, string expected)
    {
        var result = CharacterValidator.Validate(input);

        Assert.Equal(expected, result);
    }

    // ─── Invalid Characters ──────────────────────────────────────────

    [Theory]
    [InlineData("warrior")]
    [InlineData("mage")]
    [InlineData("rogue")]
    [InlineData("watcher")]
    [InlineData("unknown_char")]
    [InlineData("  ironclad  ")]
    public void Validate_InvalidCharacter_ReturnsUnknown(string character)
    {
        var result = CharacterValidator.Validate(character);

        Assert.Equal("unknown", result);
    }

    [Fact]
    public void Validate_EmptyString_ReturnsUnknown()
    {
        var result = CharacterValidator.Validate("");

        Assert.Equal("unknown", result);
    }

    [Fact]
    public void Validate_Null_ReturnsUnknown()
    {
        var result = CharacterValidator.Validate(null);

        Assert.Equal("unknown", result);
    }

    [Fact]
    public void Validate_WhitespaceOnly_ReturnsUnknown()
    {
        var result = CharacterValidator.Validate("   ");

        Assert.Equal("unknown", result);
    }

    // ─── Completeness ────────────────────────────────────────────────

    [Fact]
    public void Validate_AllSixCharacters_AreValid()
    {
        var characters = new[] { "ironclad", "silent", "defect", "regent", "necrobinder", "deprived" };

        foreach (var character in characters)
        {
            var result = CharacterValidator.Validate(character);
            Assert.Equal(character, result);
        }
    }

    [Fact]
    public void Validate_ExactlySixValidCharacters()
    {
        // Ensure no extra characters have been added without test coverage
        var allValid = new[] { "ironclad", "silent", "defect", "regent", "necrobinder", "deprived" };
        var count = 0;
        // Test a broad set — only the six should pass
        var candidates = new[]
        {
            "ironclad", "silent", "defect", "regent", "necrobinder", "deprived",
            "warrior", "mage", "rogue", "watcher", "monk", "paladin",
        };

        foreach (var c in candidates)
        {
            if (CharacterValidator.Validate(c) != "unknown")
                count++;
        }

        Assert.Equal(6, count);
    }
}
