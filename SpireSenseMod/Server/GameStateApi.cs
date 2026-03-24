using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    /// <summary>Thread-safe set of already-dumped type names (debug logging dedup).</summary>
    private static readonly ConcurrentDictionary<string, byte> _dumpedTypes = new();

    /// <summary>Cached reference to the current CombatState for RefreshCombatState.</summary>
    /// <remarks>
    /// Written by patch threads (HookSubscriptions), read by RefreshCombatState.
    /// Volatile ensures cross-thread visibility without a full lock.
    /// </remarks>
    private static volatile object? _currentCombatState;

    /// <summary>
    /// Get or set the cached CombatState reference. Thread-safe via volatile.
    /// </summary>
    internal static object? CurrentCombatState
    {
        get => _currentCombatState;
        set => _currentCombatState = value;
    }

    // ── Reflection Helpers ──────────────────────────────────────────────
    // Harmony Traverse.Property().GetValue<object>() returns null for IReadOnlyList<T>
    // and IEnumerable<T> types. Direct reflection works. Use these helpers everywhere.

    /// <summary>Get a public property value via direct reflection (works for all types including collections).</summary>
    public static object? GetProp(object? obj, string name)
        => obj?.GetType().GetProperty(name)?.GetValue(obj);

    /// <summary>Get a field value via direct reflection (public + private).</summary>
    public static object? GetField(object? obj, string name)
        => obj?.GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(obj);

    /// <summary>Get a collection (IEnumerable) from a property, with optional field fallback.</summary>
    public static System.Collections.IEnumerable? GetCollection(object? obj, string propName, string? fieldFallback = null)
        => GetProp(obj, propName) as System.Collections.IEnumerable
            ?? (fieldFallback != null ? GetField(obj, fieldFallback) as System.Collections.IEnumerable : null);

    /// <summary>
    /// Resolve a LocString object to its localized text via Godot's TranslationServer.
    /// LocString has LocTable (e.g., "cards") and LocEntryKey (e.g., "RAGE.description").
    /// </summary>
    public static string ResolveLocString(object? locStringObj)
    {
        if (locStringObj == null) return "";

        // Use direct reflection — Traverse can fail for some property types
        // Try direct text/value properties first
        var text = GetProp(locStringObj, "Text")?.ToString();
        if (!string.IsNullOrEmpty(text)) return text;

        text = GetProp(locStringObj, "Value")?.ToString();
        if (!string.IsNullOrEmpty(text)) return text;

        // Try ToString() — some LocString types auto-resolve
        var toStr = locStringObj.ToString();
        if (!string.IsNullOrEmpty(toStr) && !toStr.Contains("LocString") && !toStr.Contains("MegaCrit"))
            return toStr;

        // Get LocTable and LocEntryKey for TranslationServer
        var locTable = GetProp(locStringObj, "LocTable")?.ToString()
            ?? GetField(locStringObj, "locTable")?.ToString();
        var entryKey = GetProp(locStringObj, "LocEntryKey")?.ToString()
            ?? GetField(locStringObj, "locEntryKey")?.ToString();

        if (!string.IsNullOrEmpty(entryKey))
        {
            // Strategy 1: Combined table.key (e.g., "cards/RAGE.description")
            if (!string.IsNullOrEmpty(locTable))
            {
                var combined = $"{locTable}/{entryKey}";
                var translated = TranslationServer.Translate(combined);
                if (translated != null && translated != combined) return translated;

                // Also try with dot separator
                combined = $"{locTable}.{entryKey}";
                translated = TranslationServer.Translate(combined);
                if (translated != null && translated != combined) return translated;
            }

            // Strategy 2: Just the entry key
            var translated2 = TranslationServer.Translate(entryKey);
            if (translated2 != null && translated2 != entryKey) return translated2;

            // Fallback: return the raw key (frontend has card data from spire-codex)
            return entryKey;
        }

        return "";
    }

    /// <summary>
    /// Dump all public properties and fields of an object (once per type) for debugging.
    /// </summary>
    public static void DumpObjectOnce(object obj, string label)
    {
        var typeName = obj.GetType().FullName ?? obj.GetType().Name;
        if (!_dumpedTypes.TryAdd(typeName, 0)) return; // Already dumped this type

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
    /// CardModel properties: CardId, Name, CardType, Rarity, EnergyCost, Description, IsUpgraded, GetDescriptionForUpgradePreview()
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

            // Title is a plain String
            var name = traverse.Property("Title")?.GetValue<string>() ?? "";

            // EnergyCost is CardEnergyCost type — Canonical is the original cost, _base is the current (possibly upgraded) cost
            var energyCostObj = traverse.Property("EnergyCost")?.GetValue<object>();
            if (energyCostObj != null) DumpObjectOnce(energyCostObj, "CardEnergyCost");
            var cost = 0;
            var costUpgraded = 0;
            if (energyCostObj != null)
            {
                var ecTraverse = Traverse.Create(energyCostObj);
                cost = ecTraverse.Property("Canonical")?.GetValue<int>() ?? 0;
                // _base holds the current cost (modified by UpgradeBy), falls back to Canonical
                costUpgraded = ecTraverse.Field("_base")?.GetValue<int>() ?? cost;
            }

            // Description is LocString — has LocTable + LocEntryKey, resolve via Godot TranslationServer
            var desc = ResolveLocString(traverse.Property("Description")?.GetValue<object>());

            // Upgraded description via GetDescriptionForUpgradePreview() — returns formatted text with BBCode
            var descUpgraded = "";
            try
            {
                var upgradePreviewMethod = gameCard.GetType().GetMethod("GetDescriptionForUpgradePreview");
                if (upgradePreviewMethod != null)
                {
                    var rawUpgraded = upgradePreviewMethod.Invoke(gameCard, null)?.ToString() ?? "";
                    // Strip BBCode tags (e.g., [green]8[/green]) for clean text
                    descUpgraded = System.Text.RegularExpressions.Regex.Replace(rawUpgraded, @"\[/?[^\]]+\]", "");
                    // Strip image tags (e.g., [img]res://...png[/img]) that remain after BBCode strip
                    descUpgraded = System.Text.RegularExpressions.Regex.Replace(descUpgraded, @"\[img\][^\[]*\[/img\]", "").Trim();
                }
            }
            catch { /* Upgraded description not available — keep empty */ }

            // Character from Pool (e.g., "CARD_POOL.IRONCLAD_CARD_POOL (44127137)" → "ironclad")
            var poolStr = traverse.Property("Pool")?.GetValue<object>()?.ToString() ?? "";
            var character = "neutral";
            if (poolStr.Contains("IRONCLAD")) character = "ironclad";
            else if (poolStr.Contains("SILENT")) character = "silent";
            else if (poolStr.Contains("DEFECT")) character = "defect";
            else if (poolStr.Contains("REGENT")) character = "regent";
            else if (poolStr.Contains("NECROBINDER")) character = "necrobinder";
            else if (poolStr.Contains("DEPRIVED")) character = "deprived";

            // Tags: CardModel.Tags returns IEnumerable<CardTag>, Keywords returns IReadOnlySet<CardKeyword>
            var tags = new List<string>();
            try
            {
                var tagsEnum = traverse.Property("Tags")?.GetValue<System.Collections.IEnumerable>();
                if (tagsEnum != null)
                {
                    foreach (var tag in tagsEnum)
                    {
                        var tagStr = tag?.ToString();
                        if (!string.IsNullOrEmpty(tagStr) && tagStr != "None")
                            tags.Add(tagStr.ToLowerInvariant());
                    }
                }
            }
            catch { /* Tags not available on this card */ }

            try
            {
                var keywordsEnum = traverse.Property("Keywords")?.GetValue<System.Collections.IEnumerable>();
                if (keywordsEnum != null)
                {
                    foreach (var kw in keywordsEnum)
                    {
                        var kwStr = kw?.ToString();
                        if (!string.IsNullOrEmpty(kwStr) && kwStr != "None")
                            tags.Add(kwStr.ToLowerInvariant());
                    }
                }
            }
            catch { /* Keywords not available on this card */ }

            return new CardInfo
            {
                Id = cardId,
                Name = name,
                Character = character,
                Type = (traverse.Property("Type")?.GetValue<object>()?.ToString() ?? "Attack").ToLowerInvariant(),
                Rarity = (traverse.Property("Rarity")?.GetValue<object>()?.ToString() ?? "Common").ToLowerInvariant(),
                Cost = cost,
                CostUpgraded = costUpgraded,
                Description = desc,
                DescriptionUpgraded = descUpgraded,
                Upgraded = traverse.Property("IsUpgraded")?.GetValue<bool>() ?? false,
                Tags = tags,
            };
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] Failed to extract card info: {ex.Message}");
            return new CardInfo();
        }
    }

    /// <summary>
    /// Extract a list of cards from an IEnumerable (obtained via GetCollection).
    /// </summary>
    public static List<CardInfo> ExtractCardsFromEnum(System.Collections.IEnumerable? source)
    {
        var cards = new List<CardInfo>();
        if (source == null) return cards;
        foreach (var card in source)
            cards.Add(ExtractCardInfo(card));
        return cards;
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

            // ID from CanonicalInstance (e.g., "RELIC.BURNING_BLOOD (33410170)" → "burning_blood")
            // or from Id (ModelId, e.g., "RELIC.BURNING_BLOOD")
            var canonicalStr = GetProp(gameRelic, "CanonicalInstance")?.ToString() ?? "";
            var relicId = "";
            if (canonicalStr.StartsWith("RELIC."))
            {
                var spaceIdx = canonicalStr.IndexOf(' ');
                relicId = (spaceIdx > 6 ? canonicalStr.Substring(6, spaceIdx - 6) : canonicalStr.Substring(6)).ToLowerInvariant();
            }
            if (string.IsNullOrEmpty(relicId))
            {
                var modelId = GetProp(gameRelic, "Id")?.ToString() ?? "";
                if (modelId.StartsWith("RELIC."))
                    relicId = modelId.Substring(6).ToLowerInvariant();
            }

            // Name from HoverTip.Title (e.g., "Burning Blood")
            var name = "";
            var hoverTip = GetProp(gameRelic, "HoverTip");
            if (hoverTip != null)
            {
                name = GetProp(hoverTip, "Title")?.ToString() ?? "";
            }

            // Description from HoverTip.Description (contains BBCode like [green]6[/green])
            var description = "";
            if (hoverTip != null)
            {
                var rawDesc = GetProp(hoverTip, "Description")?.ToString() ?? "";
                // Strip BBCode tags for clean text
                description = System.Text.RegularExpressions.Regex.Replace(rawDesc, @"\[/?[^\]]+\]", "");
            }

            // Rarity from direct property
            var rarity = (GetProp(gameRelic, "Rarity")?.ToString() ?? "common").ToLowerInvariant();

            // Character from Pool (e.g., "RELIC_POOL.IRONCLAD_RELIC_POOL (22415451)")
            var poolStr = GetProp(gameRelic, "Pool")?.ToString() ?? "";
            var character = "neutral";
            if (poolStr.Contains("IRONCLAD")) character = "ironclad";
            else if (poolStr.Contains("SILENT")) character = "silent";
            else if (poolStr.Contains("DEFECT")) character = "defect";
            else if (poolStr.Contains("REGENT")) character = "regent";
            else if (poolStr.Contains("NECROBINDER")) character = "necrobinder";
            else if (poolStr.Contains("DEPRIVED")) character = "deprived";

            return new RelicInfo
            {
                Id = relicId,
                Name = name,
                Character = character,
                Rarity = rarity,
                Description = description,
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

        // Use direct reflection for all fields — Traverse fails for enum types and collections
        // Creature has: Block, CurrentHp, MaxHp, ModelId, Name, Monster (MonsterModel), Powers
        // Intent system (from decompiled sts2.dll):
        //   Creature.Monster → MonsterModel
        //   MonsterModel.NextMove → MoveState
        //   MoveState.Intents → IReadOnlyList<AbstractIntent>
        //   AbstractIntent.IntentType → IntentType enum (Attack, Buff, Debuff, Defend, etc.)
        //   AttackIntent.DamageCalc → Func<decimal> (base damage)

        var monsterModel = GetProp(gameMonster, "Monster");

        // Extract intent from MonsterModel.NextMove.Intents
        var intentStr = "unknown";
        var intentDmg = 0;
        if (monsterModel != null)
        {
            var nextMove = GetProp(monsterModel, "NextMove"); // MoveState
            if (nextMove != null)
            {
                var intents = GetCollection(nextMove, "Intents"); // IReadOnlyList<AbstractIntent>
                if (intents != null)
                {
                    var intentTypes = new List<string>();
                    foreach (var intent in intents)
                    {
                        if (intent == null) continue;
                        // AbstractIntent.IntentType is an enum
                        var intentType = GetProp(intent, "IntentType")?.ToString()?.ToLowerInvariant() ?? "";
                        intentTypes.Add(intentType);

                        // If this is an AttackIntent, get damage from DamageCalc
                        if ((intentType == "attack" || intentType == "deathblow") && intentDmg == 0)
                        {
                            try
                            {
                                // AttackIntent.DamageCalc is Func<decimal> — invoke it
                                var damageCalc = GetProp(intent, "DamageCalc") as System.Delegate;
                                if (damageCalc != null)
                                {
                                    var dmgResult = damageCalc.DynamicInvoke();
                                    if (dmgResult is decimal decDmg)
                                        intentDmg = (int)decDmg;
                                    else if (dmgResult != null)
                                        intentDmg = System.Convert.ToInt32(dmgResult);
                                }
                            }
                            catch { /* DamageCalc may throw outside combat context */ }
                        }
                    }
                    intentStr = CombineIntentTypes(intentTypes);
                }
            }
        }

        var info = new MonsterInfo
        {
            Id = GetProp(gameMonster, "ModelId")?.ToString() ?? "",
            Name = GetProp(gameMonster, "Name")?.ToString() ?? "",
            Hp = (int?)GetProp(gameMonster, "CurrentHp")
                ?? traverse.Field("_currentHp")?.GetValue<int>()
                ?? 0,
            MaxHp = (int?)GetProp(gameMonster, "MaxHp")
                ?? traverse.Field("_maxHp")?.GetValue<int>()
                ?? 0,
            Block = (int?)GetProp(gameMonster, "Block")
                ?? traverse.Field("_block")?.GetValue<int>()
                ?? 0,
            Intent = intentStr,
            IntentDamage = intentDmg,
            Powers = ExtractPowersFromEnum(GetCollection(gameMonster, "Powers", "_powers")),
        };

        return info;
    }

    /// <summary>
    /// Combine multiple STS2 AbstractIntent.IntentType values into a single frontend intent string.
    /// STS2 IntentType enum (from sts2.dll decompile):
    ///   Attack, Buff, Debuff, DebuffStrong, Defend, Escape, Heal, Hidden,
    ///   Summon, Sleep, Stun, StatusCard, CardDebuff, DeathBlow, Unknown
    /// Frontend expects: attack, defend, buff, debuff, attack_defend, attack_buff,
    ///                   attack_debuff, unknown, sleeping, stunned
    /// </summary>
    public static string CombineIntentTypes(List<string> intentTypes)
    {
        if (intentTypes.Count == 0) return "unknown";

        var hasAttack = intentTypes.Any(t => t == "attack" || t == "deathblow");
        var hasDefend = intentTypes.Any(t => t == "defend");
        var hasBuff = intentTypes.Any(t => t == "buff" || t == "heal" || t == "summon");
        var hasDebuff = intentTypes.Any(t => t == "debuff" || t == "debuffstrong" || t == "statuscard" || t == "carddebuff");
        var hasSleep = intentTypes.Any(t => t == "sleep");
        var hasStun = intentTypes.Any(t => t == "stun");

        if (hasSleep) return "sleeping";
        if (hasStun) return "stunned";

        if (hasAttack && hasDefend) return "attack_defend";
        if (hasAttack && hasBuff) return "attack_buff";
        if (hasAttack && hasDebuff) return "attack_debuff";
        if (hasAttack) return "attack";
        if (hasDefend) return "defend";
        if (hasBuff) return "buff";
        if (hasDebuff) return "debuff";

        // Hidden, Escape, Unknown → unknown
        return "unknown";
    }

    /// <summary>Extract powers from an IEnumerable (obtained via GetCollection).</summary>
    public static List<PowerInfo> ExtractPowersFromEnum(System.Collections.IEnumerable? source)
    {
        var powers = new List<PowerInfo>();
        if (source == null) return powers;
        foreach (var power in source)
        {
            var pt = Traverse.Create(power);
            powers.Add(new PowerInfo
            {
                Id = (GetProp(power, "PowerId") ?? GetProp(power, "Id"))?.ToString() ?? "",
                Name = (GetProp(power, "Name"))?.ToString() ?? "",
                Amount = pt.Property("Amount")?.GetValue<int>()
                    ?? pt.Property("StackAmount")?.GetValue<int>()
                    ?? 0,
            });
        }
        return powers;
    }

    /// <summary>Extract potions from an IEnumerable (obtained via GetCollection).</summary>
    public static List<PotionInfo> ExtractPotionsFromEnum(System.Collections.IEnumerable? source)
    {
        var potions = new List<PotionInfo>();
        if (source == null) return potions;
        foreach (var potion in source)
        {
            if (potion == null) continue;
            potions.Add(new PotionInfo
            {
                Id = (GetProp(potion, "PotionId") ?? GetProp(potion, "Id"))?.ToString() ?? "",
                Name = (GetProp(potion, "Name"))?.ToString() ?? "",
                Description = ResolveLocString(GetProp(potion, "Description")),
                CanUse = Traverse.Create(potion).Property("CanUse")?.GetValue<bool>() ?? true,
            });
        }
        return potions;
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
                        Id = (pt.Property("PowerId")?.GetValue<object>()
                            ?? pt.Field("_powerId")?.GetValue<object>())?.ToString() ?? "",
                        Name = (pt.Property("Name")?.GetValue<object>()
                            ?? pt.Field("_name")?.GetValue<object>())?.ToString() ?? "",
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
                        Id = (pt.Property("PotionId")?.GetValue<object>()
                            ?? pt.Field("_potionId")?.GetValue<object>())?.ToString() ?? "",
                        Name = (pt.Property("Name")?.GetValue<object>()
                            ?? pt.Field("_name")?.GetValue<object>())?.ToString() ?? "",
                        Description = (pt.Property("Description")?.GetValue<object>()
                            ?? pt.Field("_description")?.GetValue<object>())?.ToString() ?? "",
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
    /// <summary>
    /// Map STS2's MapPointType enum to frontend node types.
    /// Frontend expects: monster, elite, boss, rest, shop, event, chest
    /// </summary>
    public static string MapNodeType(string rawType)
    {
        return rawType switch
        {
            "monster" => "monster",
            "elite" => "elite",
            "boss" => "boss",
            "restsite" or "rest" => "rest",
            "shop" => "shop",
            "treasure" or "chest" => "chest",
            "event" => "event",
            "ancient" => "elite",
            "unknown" or "unassigned" => "monster",
            _ => "monster",
        };
    }

    public static List<MapNode> ExtractMapNodes(object mapData)
    {
        var nodes = new List<MapNode>();
        if (mapData == null) return nodes;

        try
        {
            DumpObjectOnce(mapData, "ActMap");

            // From sts2.dll decompile:
            //   StandardActMap has Grid (MapPoint[,]) — 2D array, NOT an IEnumerable
            //   MapPoint has: coord (public field MapCoord), PointType (MapPointType enum),
            //                 Children (HashSet<MapPoint>)
            //   MapCoord has: col (int), row (int) — public fields

            // Try Grid first (MapPoint[,] 2D array)
            var grid = GetProp(mapData, "Grid") ?? GetField(mapData, "<Grid>k__BackingField");
            if (grid is System.Array gridArray && gridArray.Rank == 2)
            {
                var cols = gridArray.GetLength(0);
                var rows = gridArray.GetLength(1);
                for (int c = 0; c < cols; c++)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        var point = gridArray.GetValue(c, r);
                        if (point == null) continue;

                        // MapPoint.coord is a public field (MapCoord struct)
                        var coordObj = GetField(point, "coord") ?? GetProp(point, "coord");
                        int x = 0, y = 0;
                        if (coordObj != null)
                        {
                            // MapCoord.col and .row are public fields
                            x = (int?)GetField(coordObj, "col") ?? 0;
                            y = (int?)GetField(coordObj, "row") ?? 0;
                        }

                        // MapPoint.PointType (not "Type") — MapPointType enum
                        var rawType = (GetProp(point, "PointType"))?.ToString()?.ToLowerInvariant() ?? "monster";
                        var nodeType = MapNodeType(rawType);

                        // MapPoint.Children is HashSet<MapPoint> — extract child coords
                        var connections = new List<int>();
                        var children = GetCollection(point, "Children");
                        if (children != null)
                        {
                            foreach (var child in children)
                            {
                                if (child == null) continue;
                                var childCoord = GetField(child, "coord");
                                if (childCoord != null)
                                {
                                    var childCol = (int?)GetField(childCoord, "col") ?? -1;
                                    if (childCol >= 0) connections.Add(childCol);
                                }
                            }
                        }

                        nodes.Add(new MapNode
                        {
                            X = x,
                            Y = y,
                            Type = nodeType,
                            Connections = connections,
                            Visited = false, // Track via VisitedMapCoords on RunState instead
                        });
                    }
                }
            }
            else
            {
                // Fallback: try IEnumerable-based access
                var enumerable = GetCollection(mapData, "MapPoints", "_mapPoints")
                    ?? GetCollection(mapData, "Points", "_points");
                if (enumerable != null)
                {
                    foreach (var point in enumerable)
                    {
                        if (point == null) continue;
                        var coordObj = GetField(point, "coord");
                        int x = (int?)GetField(coordObj, "col") ?? 0;
                        int y = (int?)GetField(coordObj, "row") ?? 0;
                        var rawType = GetProp(point, "PointType")?.ToString()?.ToLowerInvariant() ?? "monster";

                        nodes.Add(new MapNode
                        {
                            X = x,
                            Y = y,
                            Type = MapNodeType(rawType),
                            Connections = new List<int>(),
                            Visited = false,
                        });
                    }
                }
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

        // Also dump base type (Creature) properties for HP/Block discovery
        var baseType = gamePlayer.GetType().BaseType;
        if (baseType != null && _dumpedTypes.TryAdd("BASE:" + baseType.FullName, 0))
        {
            GD.Print($"[SpireSense DEBUG] === Player BaseType: {baseType.FullName} ===");
            foreach (var prop in baseType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                try
                {
                    var val = prop.GetValue(gamePlayer);
                    var valStr = val?.ToString() ?? "null";
                    if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                    GD.Print($"[SpireSense DEBUG]   BASE PROP {prop.Name} ({prop.PropertyType.Name}): {valStr}");
                }
                catch (System.Exception ex)
                {
                    GD.Print($"[SpireSense DEBUG]   BASE PROP {prop.Name} ({prop.PropertyType.Name}): ERROR {ex.Message}");
                }
            }
        }

        var traverse = Traverse.Create(gamePlayer);

        // Gold and MaxEnergy are directly on Player (verified from dump)
        var gold = traverse.Property("Gold")?.GetValue<int>()
            ?? traverse.Field("_gold")?.GetValue<int>()
            ?? 0;
        var maxEnergy = traverse.Property("MaxEnergy")?.GetValue<int>()
            ?? traverse.Field("_maxEnergy")?.GetValue<int>()
            ?? 3;

        // HP/Block are on the Creature sub-object
        // Player.Creature is a Creature with CurrentHp, MaxHp, Block (same type as monsters)
        var creatureObj = traverse.Property("Creature")?.GetValue<object>()
            ?? traverse.Field("<Creature>k__BackingField")?.GetValue<object>();

        int hp = 0, maxHp = 0, block = 0;
        if (creatureObj != null)
        {
            DumpObjectOnce(creatureObj, "PlayerCreature");
            var ct = Traverse.Create(creatureObj);
            hp = ct.Property("CurrentHp")?.GetValue<int>() ?? 0;
            maxHp = ct.Property("MaxHp")?.GetValue<int>() ?? 0;
            block = ct.Property("Block")?.GetValue<int>() ?? 0;
        }
        else
        {
            GD.Print($"[SpireSense] WARNING: Player.Creature is null, trying PlayerCombatState");
            // Fallback: try PlayerCombatState which may have HP data
            var pcs = traverse.Property("PlayerCombatState")?.GetValue<object>();
            if (pcs != null)
            {
                DumpObjectOnce(pcs, "PlayerCombatState");
                var pcsTraverse = Traverse.Create(pcs);
                hp = pcsTraverse.Property("CurrentHp")?.GetValue<int>() ?? 0;
                maxHp = pcsTraverse.Property("MaxHp")?.GetValue<int>() ?? 0;
                block = pcsTraverse.Property("Block")?.GetValue<int>() ?? 0;
            }
        }

        return new PlayerState
        {
            Hp = hp,
            MaxHp = maxHp,
            Block = block,
            Energy = traverse.Property("Energy")?.GetValue<int>()
                ?? traverse.Field("_energy")?.GetValue<int>()
                ?? 0,
            MaxEnergy = maxEnergy,
            Gold = gold,
            Powers = ExtractPowersFromEnum(GetCollection(creatureObj, "Powers", "_powers")),
            Potions = ExtractPotionsFromEnum(GetCollection(gamePlayer, "PotionSlots", "_potionSlots")),
        };
    }

    /// <summary>
    /// Extract card piles from Player.Piles (array of CardPile, each with Type + Cards).
    /// </summary>
    /// <summary>
    /// Extract card piles from CombatState._allCards by grouping cards by their Pile.Type.
    /// Each CardModel has a Pile property pointing to its current CardPile (Hand/DrawPile/etc).
    /// </summary>
    public static void ExtractCardPilesFromCombat(object combatState, CombatState combat)
    {
        var allCards = GetCollection(combatState, "AllCards")
            ?? GetField(combatState, "_allCards") as System.Collections.IEnumerable;
        if (allCards == null) return;

        var hand = new List<CardInfo>();
        var draw = new List<CardInfo>();
        var discard = new List<CardInfo>();
        var exhaust = new List<CardInfo>();

        foreach (var card in allCards)
        {
            if (card == null) continue;
            var pile = GetProp(card, "Pile");
            var pileType = pile != null ? GetProp(pile, "Type")?.ToString() ?? "" : "";
            var cardInfo = ExtractCardInfo(card);

            // PileType enum: None, Draw, Hand, Discard, Exhaust, Play, Deck
            switch (pileType)
            {
                case "Hand": hand.Add(cardInfo); break;
                case "Draw": draw.Add(cardInfo); break;
                case "Discard": discard.Add(cardInfo); break;
                case "Exhaust": exhaust.Add(cardInfo); break;
            }
        }

        combat.Hand = hand;
        combat.DrawPile = draw;
        combat.DiscardPile = discard;
        combat.ExhaustPile = exhaust;
        GD.Print($"[SpireSense] Piles: hand={hand.Count}, draw={draw.Count}, discard={discard.Count}, exhaust={exhaust.Count}");
    }

    /// <summary>
    /// Refresh the current combat state — updates HP, Block, monsters, card piles.
    /// Called after card_played and other combat events.
    /// </summary>
    public static void RefreshCombatState()
    {
        Plugin.StateTracker?.UpdateState(state =>
        {
            if (state.Combat == null) return;

            // Find the CombatState from the current game state
            // We stored it when combat started — now refresh from the live objects
            // Get RunState from the last known path
            var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
            if (rmType == null) return;

            // Try to get RunManager singleton
            object? runManager = null;
            try
            {
                var instanceProp = rmType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                runManager = instanceProp?.GetValue(null);
            }
            catch { /* Not a singleton */ }

            if (runManager == null) return;
            var runState = GetProp(runManager, "State");
            if (runState == null) return;

            // Get players for Gold, Energy, Card Piles
            var players = GetCollection(runState, "Players") ?? GetField(runState, "_players") as System.Collections.IEnumerable;
            if (players != null)
            {
                foreach (var player in players)
                {
                    var pt = Traverse.Create(player);
                    state.Combat.Player.Gold = pt.Property("Gold")?.GetValue<int>() ?? 0;
                    state.Combat.Player.MaxEnergy = pt.Property("MaxEnergy")?.GetValue<int>() ?? 3;

                    // Card piles — extracted separately from CombatState below

                    // Get Creature for HP/Block
                    var creature = GetProp(player, "Creature");
                    if (creature != null)
                    {
                        var ct = Traverse.Create(creature);
                        state.Combat.Player.Hp = ct.Property("CurrentHp")?.GetValue<int>() ?? state.Combat.Player.Hp;
                        state.Combat.Player.MaxHp = ct.Property("MaxHp")?.GetValue<int>() ?? state.Combat.Player.MaxHp;
                        state.Combat.Player.Block = ct.Property("Block")?.GetValue<int>() ?? state.Combat.Player.Block;
                    }
                    break;
                }
            }

            // Refresh card piles from cached CombatState
            if (CurrentCombatState != null)
                ExtractCardPilesFromCombat(CurrentCombatState, state.Combat);
        });
    }
}
