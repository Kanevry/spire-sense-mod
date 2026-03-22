using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for potion usage and acquisition.
/// Intercepts when potions are used in combat and when new potions are obtained.
///
/// STS2 classes (from sts2.dll decompilation):
/// - UsePotionAction (MegaCrit.Sts2.Core.GameActions) — extends GameAction
///   - Constructor: UsePotionAction(PotionModel, Creature?, bool)
///   - Properties: Player, PotionIndex, TargetId
/// - PotionCmd (MegaCrit.Sts2.Core.Commands) — static
///   - TryToProcure(PotionModel, Player, int) — obtain potion
///   - Discard(PotionModel) — discard potion
/// - Player (MegaCrit.Sts2.Core.Entities.Players)
///   - Events: PotionProcured, PotionDiscarded, UsedPotionRemoved
/// </summary>
public static class PotionPatch
{
    /// <summary>
    /// Postfix patch: fires when a potion is used.
    /// Tracks which potion was used and on which target.
    ///
    /// TARGET: UsePotionAction.ExecuteAction() (extends GameAction)
    /// __instance is UsePotionAction with potion and target info.
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.GameActions.UsePotionAction", "ExecuteAction")]
    [HarmonyPostfix]
    public static void OnPotionUsed(object __instance)
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

    /// <summary>
    /// Postfix patch: fires when a potion is obtained.
    /// Updates the potion inventory in the player state.
    ///
    /// TARGET: PotionCmd.TryToProcure(PotionModel, Player, int) — static method
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Commands.PotionCmd", "TryToProcure")]
    [HarmonyPostfix]
    public static void OnPotionObtained(object[] __args)
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
