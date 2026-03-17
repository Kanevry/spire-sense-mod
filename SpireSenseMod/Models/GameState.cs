using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpireSenseMod;

/// <summary>
/// Complete game state snapshot, serialized to JSON for the HTTP API.
/// Mirrors the TypeScript types in the web app.
/// </summary>
public class GameState
{
    [JsonPropertyName("screen")]
    public string Screen { get; set; } = "main_menu";

    [JsonPropertyName("character")]
    public string Character { get; set; } = "ironclad";

    [JsonPropertyName("act")]
    public int Act { get; set; } = 1;

    [JsonPropertyName("floor")]
    public int Floor { get; set; } = 0;

    [JsonPropertyName("ascension")]
    public int Ascension { get; set; } = 0;

    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("deck")]
    public List<CardInfo> Deck { get; set; } = new();

    [JsonPropertyName("relics")]
    public List<RelicInfo> Relics { get; set; } = new();

    [JsonPropertyName("combat")]
    public CombatState? Combat { get; set; }

    [JsonPropertyName("map")]
    public List<MapNode> Map { get; set; } = new();

    [JsonPropertyName("cardRewards")]
    public List<CardInfo>? CardRewards { get; set; }

    [JsonPropertyName("shopCards")]
    public List<CardInfo>? ShopCards { get; set; }

    [JsonPropertyName("shopRelics")]
    public List<RelicInfo>? ShopRelics { get; set; }

    [JsonPropertyName("eventOptions")]
    public List<EventOption>? EventOptions { get; set; }
}

public class GameEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
