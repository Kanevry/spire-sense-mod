using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpireSenseMod;

public class CombatState
{
    [JsonPropertyName("turn")]
    public int Turn { get; set; } = 1;

    [JsonPropertyName("player")]
    public PlayerState Player { get; set; } = new();

    [JsonPropertyName("monsters")]
    public List<MonsterInfo> Monsters { get; set; } = new();

    [JsonPropertyName("hand")]
    public List<CardInfo> Hand { get; set; } = new();

    [JsonPropertyName("drawPile")]
    public List<CardInfo> DrawPile { get; set; } = new();

    [JsonPropertyName("discardPile")]
    public List<CardInfo> DiscardPile { get; set; } = new();

    [JsonPropertyName("exhaustPile")]
    public List<CardInfo> ExhaustPile { get; set; } = new();
}

public class PlayerState
{
    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("maxHp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("block")]
    public int Block { get; set; }

    [JsonPropertyName("energy")]
    public int Energy { get; set; }

    [JsonPropertyName("maxEnergy")]
    public int MaxEnergy { get; set; } = 3;

    [JsonPropertyName("gold")]
    public int Gold { get; set; }

    [JsonPropertyName("powers")]
    public List<PowerInfo> Powers { get; set; } = new();

    [JsonPropertyName("potions")]
    public List<PotionInfo> Potions { get; set; } = new();
}

public class MonsterInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("maxHp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("block")]
    public int Block { get; set; }

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "unknown";

    [JsonPropertyName("intentDamage")]
    public int IntentDamage { get; set; }

    [JsonPropertyName("powers")]
    public List<PowerInfo> Powers { get; set; } = new();
}

public class PowerInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("amount")]
    public int Amount { get; set; }
}

public class PotionInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("canUse")]
    public bool CanUse { get; set; }
}

public class MapNode
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "monster";

    [JsonPropertyName("connections")]
    public List<int> Connections { get; set; } = new();

    [JsonPropertyName("visited")]
    public bool Visited { get; set; }
}

public class EventOption
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public class RelicInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "common";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("character")]
    public string Character { get; set; } = "neutral";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public class ShopItem
{
    [JsonPropertyName("card")]
    public CardInfo Card { get; set; } = new();

    [JsonPropertyName("price")]
    public int Price { get; set; }
}

public class ShopRelicItem
{
    [JsonPropertyName("relic")]
    public RelicInfo Relic { get; set; } = new();

    [JsonPropertyName("price")]
    public int Price { get; set; }
}

public class RestOption
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// A buffered event entry for the ring buffer.
/// Stores the event type, optional data, and a Unix millisecond timestamp.
/// </summary>
public class BufferedEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}
