using System.Text.Json.Serialization;

namespace SpireSenseMod;

/// <summary>
/// Represents a single orb in the Defect's orb slots.
/// Mirrors the web app's OrbInfo type for JSON serialization.
/// </summary>
public class OrbInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";  // "lightning", "frost", "dark", "plasma"

    [JsonPropertyName("passiveAmount")]
    public int PassiveAmount { get; set; }

    [JsonPropertyName("evokeAmount")]
    public int EvokeAmount { get; set; }
}
