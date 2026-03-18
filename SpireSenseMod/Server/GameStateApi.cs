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
    /// </summary>
    public static MonsterInfo ExtractMonsterInfo(object gameMonster)
    {
        var traverse = Traverse.Create(gameMonster);

        return new MonsterInfo
        {
            Id = traverse.Field("id")?.GetValue<string>() ?? "",
            Name = traverse.Field("name")?.GetValue<string>() ?? "",
            Hp = traverse.Field("currentHp")?.GetValue<int>() ?? traverse.Field("hp")?.GetValue<int>() ?? 0,
            MaxHp = traverse.Field("maxHp")?.GetValue<int>() ?? 0,
            Block = traverse.Field("block")?.GetValue<int>() ?? 0,
            Intent = traverse.Field("intent")?.GetValue<string>()?.ToLowerInvariant() ?? "unknown",
            Powers = new List<PowerInfo>(),
        };
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
            Powers = new List<PowerInfo>(),
            Potions = new List<PotionInfo>(),
        };
    }
}
