using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpireSenseMod;

public class CardInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("character")]
    public string Character { get; set; } = "neutral";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "attack";

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "common";

    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("costUpgraded")]
    public int CostUpgraded { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("descriptionUpgraded")]
    public string DescriptionUpgraded { get; set; } = "";

    [JsonPropertyName("upgraded")]
    public bool Upgraded { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}
