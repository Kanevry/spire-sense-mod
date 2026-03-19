using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod;

/// <summary>
/// Helper class to extract game state from STS2 game objects.
/// Uses Harmony Traverse to access internal/private fields.
///
/// NOTE: These methods reference STS2 game classes which are only available
/// when compiled against the game's assemblies. The class names and method
/// signatures may need adjustment as the game updates during Early Access.
/// </summary>
public static class GameStateApi
{
    /// <summary>
    /// Extract card info from a game card object.
    /// Uses reflection via Traverse since card internals are not public.
    /// </summary>
    public static CardInfo ExtractCardInfo(object gameCard)
    {
        var traverse = Traverse.Create(gameCard);

        return new CardInfo
        {
            Id = traverse.Field("id")?.GetValue<string>() ?? "",
            Name = traverse.Field("name")?.GetValue<string>() ?? traverse.Property("Name")?.GetValue<string>() ?? "",
            Character = traverse.Field("character")?.GetValue<string>()?.ToLowerInvariant()
                ?? traverse.Field("color")?.GetValue<string>()?.ToLowerInvariant()
                ?? "neutral",
            Type = traverse.Field("type")?.GetValue<string>()?.ToLowerInvariant() ?? "attack",
            Rarity = traverse.Field("rarity")?.GetValue<string>()?.ToLowerInvariant() ?? "common",
            Cost = traverse.Field("cost")?.GetValue<int>() ?? 0,
            CostUpgraded = traverse.Field("costUpgraded")?.GetValue<int>() ?? 0,
            Description = traverse.Field("description")?.GetValue<string>() ?? "",
            Upgraded = traverse.Field("upgraded")?.GetValue<bool>() ?? false,
            Tags = new List<string>(),
        };
    }

    /// <summary>
    /// Extract a list of cards from a game collection (hand, draw pile, discard pile, etc.).
    /// The source should be a Traverse pointing to a collection of card objects.
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
    /// Extract relic info from a game relic object.
    /// </summary>
    public static RelicInfo ExtractRelicInfo(object gameRelic)
    {
        var traverse = Traverse.Create(gameRelic);

        return new RelicInfo
        {
            Id = traverse.Field("id")?.GetValue<string>() ?? "",
            Name = traverse.Field("name")?.GetValue<string>() ?? "",
            Character = traverse.Field("character")?.GetValue<string>()?.ToLowerInvariant()
                ?? traverse.Field("color")?.GetValue<string>()?.ToLowerInvariant()
                ?? "neutral",
            Rarity = traverse.Field("rarity")?.GetValue<string>()?.ToLowerInvariant() ?? "common",
            Description = traverse.Field("description")?.GetValue<string>() ?? "",
            Tags = new List<string>(),
        };
    }

    /// <summary>
    /// Extract monster info from a game creature/monster object.
    /// Includes power traversal and intent damage value.
    /// </summary>
    public static MonsterInfo ExtractMonsterInfo(object gameMonster)
    {
        var traverse = Traverse.Create(gameMonster);

        var info = new MonsterInfo
        {
            Id = traverse.Field("id")?.GetValue<string>() ?? "",
            Name = traverse.Field("name")?.GetValue<string>() ?? "",
            Hp = traverse.Field("currentHp")?.GetValue<int>() ?? traverse.Field("hp")?.GetValue<int>() ?? 0,
            MaxHp = traverse.Field("maxHp")?.GetValue<int>() ?? 0,
            Block = traverse.Field("block")?.GetValue<int>() ?? 0,
            Intent = traverse.Field("intent")?.GetValue<string>()?.ToLowerInvariant() ?? "unknown",
            IntentDamage = traverse.Field("intentDmg")?.GetValue<int>()
                ?? traverse.Field("intentDamage")?.GetValue<int>()
                ?? 0,
            Powers = ExtractPowers(traverse.Field("powers")),
        };

        return info;
    }

    /// <summary>
    /// Extract powers/buffs from a game object's powers collection.
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
                        Id = pt.Field("id")?.GetValue<string>() ?? pt.Field("powerId")?.GetValue<string>() ?? "",
                        Name = pt.Field("name")?.GetValue<string>() ?? "",
                        Amount = pt.Field("amount")?.GetValue<int>() ?? pt.Field("stackAmount")?.GetValue<int>() ?? 0,
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
    /// Extract potions from a game object's potions collection.
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
                    var pt = Traverse.Create(potion);
                    potions.Add(new PotionInfo
                    {
                        Id = pt.Field("id")?.GetValue<string>() ?? pt.Field("potionId")?.GetValue<string>() ?? "",
                        Name = pt.Field("name")?.GetValue<string>() ?? "",
                        Description = pt.Field("description")?.GetValue<string>() ?? "",
                        CanUse = pt.Field("canUse")?.GetValue<bool>() ?? true,
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
    /// Extract player state from the game's player/character object.
    /// </summary>
    public static PlayerState ExtractPlayerState(object gamePlayer)
    {
        var traverse = Traverse.Create(gamePlayer);

        return new PlayerState
        {
            Hp = traverse.Field("currentHp")?.GetValue<int>() ?? traverse.Field("hp")?.GetValue<int>() ?? 0,
            MaxHp = traverse.Field("maxHp")?.GetValue<int>() ?? 0,
            Block = traverse.Field("block")?.GetValue<int>() ?? 0,
            Energy = traverse.Field("energy")?.GetValue<int>() ?? 0,
            MaxEnergy = traverse.Field("maxEnergy")?.GetValue<int>() ?? 3,
            Gold = traverse.Field("gold")?.GetValue<int>() ?? 0,
            Powers = ExtractPowers(traverse.Field("powers")),
            Potions = ExtractPotions(traverse.Field("potions")),
        };
    }
}
