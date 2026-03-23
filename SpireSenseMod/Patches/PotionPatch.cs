using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for potion usage and acquisition.
/// Intercepts when potions are used in combat and when new potions are obtained.
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
/// </summary>
public static class PotionPatch
{
    /// <summary>
    /// Postfix patch: fires when a potion is used.
    /// Tracks which potion was used and on which target.
    ///
    /// TARGET: UsePotionAction.ExecuteAction() (extends GameAction)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnPotionUsed
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.GameActions.UsePotionAction");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "ExecuteAction" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);

                // UsePotionAction stores the PotionModel and target
                var potionObj = traverse.Field("_potion")?.GetValue<object>()
                    ?? traverse.Property("Potion")?.GetValue<object>();
                var targetObj = traverse.Field("_target")?.GetValue<object>()
                    ?? traverse.Property("Target")?.GetValue<object>();

                var potionInfo = new PotionInfo();
                if (potionObj != null)
                {
                    var potionTraverse = Traverse.Create(potionObj);
                    potionInfo = new PotionInfo
                    {
                        Id = potionTraverse.Property("PotionId")?.GetValue<string>()
                            ?? potionTraverse.Field("_potionId")?.GetValue<string>()
                            ?? "",
                        Name = potionTraverse.Property("Name")?.GetValue<string>()
                            ?? potionTraverse.Field("_name")?.GetValue<string>()
                            ?? "",
                        Description = potionTraverse.Property("Description")?.GetValue<string>()
                            ?? potionTraverse.Field("_description")?.GetValue<string>()
                            ?? "",
                        CanUse = false, // Already used
                    };
                }

                var targetName = "";
                if (targetObj != null)
                {
                    var targetTraverse = Traverse.Create(targetObj);
                    targetName = targetTraverse.Property("Name")?.GetValue<string>()
                        ?? targetTraverse.Field("_name")?.GetValue<string>()
                        ?? "";
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
    }

    /// <summary>
    /// Postfix patch: fires when a potion is obtained.
    /// Updates the potion inventory in the player state.
    ///
    /// TARGET: PotionCmd.TryToProcure(PotionModel, Player, int) — static method
    /// Uses manual resolution because PotionCmd.TryToProcure may have generic overloads.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnPotionObtained
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Commands.PotionCmd");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                .Where(m => m.Name == "TryToProcure" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // First arg is PotionModel
                if (__args.Length == 0 || __args[0] == null) return;
                var potionObj = __args[0];

                var potionTraverse = Traverse.Create(potionObj);
                var potionInfo = new PotionInfo
                {
                    Id = potionTraverse.Property("PotionId")?.GetValue<string>()
                        ?? potionTraverse.Field("_potionId")?.GetValue<string>()
                        ?? "",
                    Name = potionTraverse.Property("Name")?.GetValue<string>()
                        ?? potionTraverse.Field("_name")?.GetValue<string>()
                        ?? "",
                    Description = potionTraverse.Property("Description")?.GetValue<string>()
                        ?? potionTraverse.Field("_description")?.GetValue<string>()
                        ?? "",
                    CanUse = true,
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
}
