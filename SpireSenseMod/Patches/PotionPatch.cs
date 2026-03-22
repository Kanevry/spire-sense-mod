using System.Collections.Generic;
using HarmonyLib;
using Godot;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for potion usage and acquisition.
/// Intercepts when potions are used in combat and when new potions are obtained.
///
/// NOTE: The target class and method names are placeholders based on STS2
/// decompiled patterns. These MUST be verified against actual game assemblies
/// and updated as the game evolves during Early Access.
///
/// Known STS2 patterns from sts2-advisor/BetterSpire2:
/// - Potions are managed by a potion manager or player inventory
/// - Usage triggers an effect callback
/// - Obtaining a potion adds it to a fixed-size slot array
/// </summary>
public static class PotionPatch
{
    /// <summary>
    /// Postfix patch: fires when a potion is used.
    /// Tracks which potion was used and on which target.
    ///
    /// TARGET: The method that applies the potion effect.
    /// This needs to be identified via decompilation of the game DLL.
    /// Example: [HarmonyPatch(typeof(PotionManager), "UsePotion")]
    /// </summary>
    // [HarmonyPatch(typeof(PotionManager), "UsePotion")]
    // [HarmonyPostfix]
    public static void OnPotionUsed(object __instance, object potion, object target)
    {
        try
        {
            var potionTraverse = Traverse.Create(potion);
            var potionInfo = new PotionInfo
            {
                Id = potionTraverse.Field("id")?.GetValue<string>()
                    ?? potionTraverse.Field("potionId")?.GetValue<string>()
                    ?? "",
                Name = potionTraverse.Field("name")?.GetValue<string>() ?? "",
                Description = potionTraverse.Field("description")?.GetValue<string>() ?? "",
                CanUse = false, // Already used
            };

            var targetName = "";
            if (target != null)
            {
                targetName = Traverse.Create(target).Field("name")?.GetValue<string>() ?? "";
            }

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "potion_used",
                Data = new { potion = potionInfo, target = targetName },
            });

            // Update potion inventory — remove the used potion
            Plugin.StateTracker?.UpdateState(state =>
            {
                if (state.Combat?.Player.Potions != null)
                {
                    state.Combat.Player.Potions.RemoveAll(p => p.Id == potionInfo.Id);
                }
            });

            GD.Print($"[SpireSense] Potion used: {potionInfo.Name}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] PotionPatch OnPotionUsed error: {ex.Message}");
        }
    }

    /// <summary>
    /// Postfix patch: fires when a potion is obtained.
    /// Updates the potion inventory in the player state.
    ///
    /// TARGET: The method that adds a potion to the player inventory.
    /// Example: [HarmonyPatch(typeof(PotionManager), "ObtainPotion")]
    /// </summary>
    // [HarmonyPatch(typeof(PotionManager), "ObtainPotion")]
    // [HarmonyPostfix]
    public static void OnPotionObtained(object __instance, object potion)
    {
        try
        {
            var potionTraverse = Traverse.Create(potion);
            var potionInfo = new PotionInfo
            {
                Id = potionTraverse.Field("id")?.GetValue<string>()
                    ?? potionTraverse.Field("potionId")?.GetValue<string>()
                    ?? "",
                Name = potionTraverse.Field("name")?.GetValue<string>() ?? "",
                Description = potionTraverse.Field("description")?.GetValue<string>() ?? "",
                CanUse = potionTraverse.Field("canUse")?.GetValue<bool>() ?? true,
            };

            Plugin.StateTracker?.UpdateState(state =>
            {
                if (state.Combat?.Player.Potions != null)
                {
                    state.Combat.Player.Potions.Add(potionInfo);
                }
            });

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "potion_obtained",
                Data = new { potion = potionInfo },
            });

            GD.Print($"[SpireSense] Potion obtained: {potionInfo.Name}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] PotionPatch OnPotionObtained error: {ex.Message}");
        }
    }
}
