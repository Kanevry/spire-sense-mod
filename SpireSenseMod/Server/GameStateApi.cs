using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod;

/// <summary>
/// Helper class to extract game state from STS2 game objects.
/// Uses Harmony Traverse to access internal/private fields.
///
/// STS2 field naming conventions (from sts2.dll decompilation):
/// - Private fields: _camelCase (e.g., _name, _description)
/// - Properties: PascalCase (e.g., Name, Description, CardId)
/// - Models: CardModel, RelicModel, PotionModel, MonsterModel
/// - Creatures: Creature base with Name, CurrentHp, MaxHp, Block
/// </summary>
public static class GameStateApi
{
    private static readonly HashSet<string> _dumpedTypes = new();

    /// <summary>
    /// Dump all public properties and fields of an object (once per type) for debugging.
    /// </summary>
    public static void DumpObjectOnce(object obj, string label)
    {
        var typeName = obj.GetType().FullName ?? obj.GetType().Name;
        if (!_dumpedTypes.Add(typeName)) return; // Already dumped this type

        GD.Print($"[SpireSense DEBUG] === {label}: {typeName} ===");
        var type = obj.GetType();

        // Properties
        foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            try
            {
                var val = prop.GetValue(obj);
                var valStr = val?.ToString() ?? "null";
                if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                GD.Print($"[SpireSense DEBUG]   PROP {prop.Name} ({prop.PropertyType.Name}): {valStr}");
            }
            catch (System.Exception ex)
            {
                GD.Print($"[SpireSense DEBUG]   PROP {prop.Name} ({prop.PropertyType.Name}): ERROR {ex.Message}");
            }
        }

        // Fields
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
        {
            try
            {
                var val = field.GetValue(obj);
                var valStr = val?.ToString() ?? "null";
                if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                GD.Print($"[SpireSense DEBUG]   FIELD {field.Name} ({field.FieldType.Name}): {valStr}");
            }
            catch (System.Exception ex)
            {
                GD.Print($"[SpireSense DEBUG]   FIELD {field.Name} ({field.FieldType.Name}): ERROR {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Extract card info from a game CardModel object.
    /// CardModel properties: CardId, Name, CardType, Rarity, EnergyCost, Description, IsUpgraded
    /// </summary>
    public static CardInfo ExtractCardInfo(object gameCard)
    {
        try
        {
            DumpObjectOnce(gameCard, "CardModel");
            var traverse = Traverse.Create(gameCard);

            // Card ID from CanonicalInstance (e.g., "CARD.IRON_WAVE (54965706)" → "iron_wave")
            var canonicalStr = traverse.Property("CanonicalInstance")?.GetValue<object>()?.ToString() ?? "";
            var cardId = "";
            if (canonicalStr.StartsWith("CARD."))
            {
                var spaceIdx = canonicalStr.IndexOf(' ');
                cardId = (spaceIdx > 5 ? canonicalStr.Substring(5, spaceIdx - 5) : canonicalStr.Substring(5)).ToLowerInvariant();
            }

            // Title is a plain String, Description is LocString (use ToString)
            var name = traverse.Property("Title")?.GetValue<string>() ?? "";
            var desc = traverse.Property("Description")?.GetValue<object>()?.ToString() ?? "";

            // EnergyCost is CardEnergyCost type — use ToString to get the value
            var energyCostObj = traverse.Property("EnergyCost")?.GetValue<object>();
            var energyCostStr = energyCostObj?.ToString() ?? "0";
            int.TryParse(energyCostStr, out var cost);

            // Character from Pool (e.g., "CARD_POOL.IRONCLAD_CARD_POOL (44127137)" → "ironclad")
            var poolStr = traverse.Property("Pool")?.GetValue<object>()?.ToString() ?? "";
            var character = "neutral";
            if (poolStr.Contains("IRONCLAD")) character = "ironclad";
            else if (poolStr.Contains("SILENT")) character = "silent";
            else if (poolStr.Contains("DEFECT")) character = "defect";
            else if (poolStr.Contains("REGENT")) character = "regent";
            else if (poolStr.Contains("NECROBINDER")) character = "necrobinder";
            else if (poolStr.Contains("DEPRIVED")) character = "deprived";

            return new CardInfo
            {
                Id = cardId,
                Name = name,
                Character = character,
                Type = (traverse.Property("Type")?.GetValue<object>()?.ToString() ?? "Attack").ToLowerInvariant(),
                Rarity = (traverse.Property("Rarity")?.GetValue<object>()?.ToString() ?? "Common").ToLowerInvariant(),
                Cost = cost,
                CostUpgraded = cost, // TODO: extract upgraded cost from CardEnergyCost
                Description = desc,
                Upgraded = traverse.Property("IsUpgraded")?.GetValue<bool>() ?? false,
                Tags = new List<string>(),
            };
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] Failed to extract card info: {ex.Message}");
            return new CardInfo();
        }
    }

    /// <summary>
    /// Extract a list of cards from a game collection (hand, draw pile, discard pile, etc.).
    /// The source should be a Traverse pointing to a CardPile or IEnumerable of CardModel.
    /// </summary>
    public static List<CardInfo> ExtractCards(Traverse? source)
    {
        var cards = new List<CardInfo>();
        if (source == null) return cards;

        try
        {
            var collection = source.GetValue<object>();
            if (collection is System.Collections.IEnumerable enumerable)
            {
                foreach (var card in enumerable)
                {
                    cards.Add(ExtractCardInfo(card));
                }
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] ExtractCards error: {ex.Message}");
        }

        return cards;
    }

    /// <summary>
    /// Extract relic info from a game RelicModel object.
    /// RelicModel properties: RelicId, Name, CharacterId, Rarity, Description
    /// </summary>
    public static RelicInfo ExtractRelicInfo(object gameRelic)
    {
        try
        {
            DumpObjectOnce(gameRelic, "Relic");
            var traverse = Traverse.Create(gameRelic);

            return new RelicInfo
            {
                Id = traverse.Property("RelicId")?.GetValue<string>()
                    ?? traverse.Field("_relicId")?.GetValue<string>()
                    ?? "",
                Name = traverse.Property("Name")?.GetValue<string>()
                    ?? traverse.Field("_name")?.GetValue<string>()
                    ?? "",
                Character = (traverse.Property("CharacterId")?.GetValue<string>()
                    ?? traverse.Field("_characterId")?.GetValue<string>()
                    ?? traverse.Property("Color")?.GetValue<object>()?.ToString()
                    ?? "neutral").ToLowerInvariant(),
                Rarity = (traverse.Property("Rarity")?.GetValue<object>()?.ToString()
                    ?? traverse.Field("_rarity")?.GetValue<object>()?.ToString()
                    ?? "Common").ToLowerInvariant(),
                Description = traverse.Property("Description")?.GetValue<string>()
                    ?? traverse.Field("_description")?.GetValue<string>()
                    ?? "",
                Tags = new List<string>(),
            };
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] Failed to extract relic info: {ex.Message}");
            return new RelicInfo();
        }
    }

    /// <summary>
    /// Extract monster info from a game Creature object.
    /// Creature properties: Name, CurrentHp, MaxHp, Block
    /// MonsterModel extends Creature with intent data.
    /// </summary>
    public static MonsterInfo ExtractMonsterInfo(object gameMonster)
    {
        DumpObjectOnce(gameMonster, "Monster");
        var traverse = Traverse.Create(gameMonster);

        var info = new MonsterInfo
        {
            Id = traverse.Property("MonsterId")?.GetValue<string>()
                ?? traverse.Property("CreatureId")?.GetValue<string>()
                ?? traverse.Field("_monsterId")?.GetValue<string>()
                ?? "",
            Name = traverse.Property("Name")?.GetValue<string>()
                ?? traverse.Field("_name")?.GetValue<string>()
                ?? "",
            Hp = traverse.Property("CurrentHp")?.GetValue<int>()
                ?? traverse.Field("_currentHp")?.GetValue<int>()
                ?? 0,
            MaxHp = traverse.Property("MaxHp")?.GetValue<int>()
                ?? traverse.Field("_maxHp")?.GetValue<int>()
                ?? 0,
            Block = traverse.Property("Block")?.GetValue<int>()
                ?? traverse.Field("_block")?.GetValue<int>()
                ?? 0,
            Intent = (traverse.Property("Intent")?.GetValue<object>()?.ToString()
                ?? traverse.Field("_intent")?.GetValue<object>()?.ToString()
                ?? "unknown").ToLowerInvariant(),
            IntentDamage = traverse.Property("IntentDamage")?.GetValue<int>()
                ?? traverse.Field("_intentDamage")?.GetValue<int>()
                ?? 0,
            Powers = ExtractPowers(traverse.Property("Powers") ?? traverse.Field("_powers")),
        };

        return info;
    }

    /// <summary>
    /// Extract powers/buffs from a creature's powers collection.
    /// Power properties: PowerId, Name, Amount/StackAmount
    /// </summary>
    public static List<PowerInfo> ExtractPowers(Traverse? source)
    {
        var powers = new List<PowerInfo>();
        if (source == null) return powers;

        try
        {
            var collection = source.GetValue<object>();
            if (collection is System.Collections.IEnumerable enumerable)
            {
                foreach (var power in enumerable)
                {
                    var pt = Traverse.Create(power);
                    powers.Add(new PowerInfo
                    {
                        Id = pt.Property("PowerId")?.GetValue<string>()
                            ?? pt.Field("_powerId")?.GetValue<string>()
                            ?? "",
                        Name = pt.Property("Name")?.GetValue<string>()
                            ?? pt.Field("_name")?.GetValue<string>()
                            ?? "",
                        Amount = pt.Property("Amount")?.GetValue<int>()
                            ?? pt.Property("StackAmount")?.GetValue<int>()
                            ?? pt.Field("_amount")?.GetValue<int>()
                            ?? 0,
                    });
                }
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] ExtractPowers error: {ex.Message}");
        }

        return powers;
    }

    /// <summary>
    /// Extract potions from a player's potion slots.
    /// Player.PotionSlots is an array/list of PotionModel.
    /// PotionModel properties: PotionId, Name, Description
    /// </summary>
    public static List<PotionInfo> ExtractPotions(Traverse? source)
    {
        var potions = new List<PotionInfo>();
        if (source == null) return potions;

        try
        {
            var collection = source.GetValue<object>();
            if (collection is System.Collections.IEnumerable enumerable)
            {
                foreach (var potion in enumerable)
                {
                    if (potion == null) continue; // Empty potion slot
                    var pt = Traverse.Create(potion);
                    potions.Add(new PotionInfo
                    {
                        Id = pt.Property("PotionId")?.GetValue<string>()
                            ?? pt.Field("_potionId")?.GetValue<string>()
                            ?? "",
                        Name = pt.Property("Name")?.GetValue<string>()
                            ?? pt.Field("_name")?.GetValue<string>()
                            ?? "",
                        Description = pt.Property("Description")?.GetValue<string>()
                            ?? pt.Field("_description")?.GetValue<string>()
                            ?? "",
                        CanUse = pt.Property("CanUse")?.GetValue<bool>()
                            ?? pt.Field("_canUse")?.GetValue<bool>()
                            ?? true,
                    });
                }
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] ExtractPotions error: {ex.Message}");
        }

        return potions;
    }

    /// <summary>
    /// Extract all map nodes from the game's ActMap data object.
    /// ActMap contains map points with MapPointType and connections.
    /// MapPoint has Coord (MapCoord with X, Y), Type (MapPointType), connections.
    /// </summary>
    public static List<MapNode> ExtractMapNodes(object mapData)
    {
        var nodes = new List<MapNode>();
        if (mapData == null) return nodes;

        try
        {
            var traverse = Traverse.Create(mapData);
            // ActMap has MapPoints or similar collection
            var nodeCollection = traverse.Property("MapPoints")?.GetValue<object>()
                ?? traverse.Field("_mapPoints")?.GetValue<object>()
                ?? traverse.Property("Points")?.GetValue<object>()
                ?? traverse.Field("_points")?.GetValue<object>();
            if (nodeCollection is not System.Collections.IEnumerable enumerable) return nodes;

            foreach (var node in enumerable)
            {
                var nt = Traverse.Create(node);

                // MapPoint has Coord (MapCoord), Type (MapPointType)
                var coord = nt.Property("Coord")?.GetValue<object>()
                    ?? nt.Field("_coord")?.GetValue<object>();
                int x = 0, y = 0;
                if (coord != null)
                {
                    var ct = Traverse.Create(coord);
                    x = ct.Property("X")?.GetValue<int>() ?? ct.Field("X")?.GetValue<int>() ?? 0;
                    y = ct.Property("Y")?.GetValue<int>() ?? ct.Field("Y")?.GetValue<int>() ?? 0;
                }

                // MapPointType enum: Unassigned, Unknown, Shop, Treasure, RestSite, Monster, Elite, Boss, Ancient
                var nodeType = (nt.Property("Type")?.GetValue<object>()?.ToString()
                    ?? nt.Field("_type")?.GetValue<object>()?.ToString()
                    ?? "Monster").ToLowerInvariant();

                // Extract connection indices from the node's children/connections
                var connections = new List<int>();
                var edgeCollection = nt.Property("Children")?.GetValue<object>()
                    ?? nt.Field("_children")?.GetValue<object>()
                    ?? nt.Property("Connections")?.GetValue<object>();
                if (edgeCollection is System.Collections.IEnumerable edgeEnum)
                {
                    foreach (var edge in edgeEnum)
                    {
                        // Connection might be another MapPoint or a MapCoord
                        var et = Traverse.Create(edge);
                        var edgeCoord = et.Property("Coord")?.GetValue<object>();
                        if (edgeCoord != null)
                        {
                            var ect = Traverse.Create(edgeCoord);
                            var edgeY = ect.Property("Y")?.GetValue<int>() ?? ect.Field("Y")?.GetValue<int>() ?? -1;
                            if (edgeY >= 0) connections.Add(edgeY);
                        }
                        else
                        {
                            // Direct index
                            var idx = et.Property("Y")?.GetValue<int>() ?? et.Field("Y")?.GetValue<int>() ?? -1;
                            if (idx >= 0) connections.Add(idx);
                        }
                    }
                }

                var visited = nt.Property("IsVisited")?.GetValue<bool>()
                    ?? nt.Field("_isVisited")?.GetValue<bool>()
                    ?? false;

                nodes.Add(new MapNode
                {
                    X = x,
                    Y = y,
                    Type = nodeType,
                    Connections = connections,
                    Visited = visited,
                });
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] Failed to extract map nodes: {ex.Message}");
        }

        return nodes;
    }

    /// <summary>
    /// Extract player state from the game's Player object.
    /// Player properties: CurrentHp, MaxHp, Block, MaxEnergy, Gold, Deck, Relics, PotionSlots
    /// </summary>
    public static PlayerState ExtractPlayerState(object gamePlayer)
    {
        DumpObjectOnce(gamePlayer, "Player");
        var traverse = Traverse.Create(gamePlayer);

        return new PlayerState
        {
            Hp = traverse.Property("CurrentHp")?.GetValue<int>()
                ?? traverse.Field("_currentHp")?.GetValue<int>()
                ?? 0,
            MaxHp = traverse.Property("MaxHp")?.GetValue<int>()
                ?? traverse.Field("_maxHp")?.GetValue<int>()
                ?? 0,
            Block = traverse.Property("Block")?.GetValue<int>()
                ?? traverse.Field("_block")?.GetValue<int>()
                ?? 0,
            Energy = traverse.Property("Energy")?.GetValue<int>()
                ?? traverse.Field("_energy")?.GetValue<int>()
                ?? 0,
            MaxEnergy = traverse.Property("MaxEnergy")?.GetValue<int>()
                ?? traverse.Field("_maxEnergy")?.GetValue<int>()
                ?? 3,
            Gold = traverse.Property("Gold")?.GetValue<int>()
                ?? traverse.Field("_gold")?.GetValue<int>()
                ?? 0,
            Powers = ExtractPowers(traverse.Property("Powers") ?? traverse.Field("_powers")),
            Potions = ExtractPotions(traverse.Property("PotionSlots") ?? traverse.Field("_potionSlots")),
        };
    }
}
