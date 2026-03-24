using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for rest site interactions.
/// Intercepts rest site choice selection (heal, smith, dig, etc.).
///
/// Migrated to hooks (HookEventAdapter):
///   - OnRestEntered → HandleRestEntered (via AfterRoomEntered hook, RestSiteRoom detection)
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
/// </summary>
public static class RestPatch
{
    /// <summary>
    /// Postfix patch: fires when the player clicks a rest site button.
    /// Emits the chosen option for analytics tracking.
    ///
    /// TARGET: NRestSiteButton.SelectOption(RestSiteOption) — concrete method (not abstract)
    /// Note: RestSiteOption.OnSelect() is abstract and cannot be patched by Harmony.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnRestChoice
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.RestSite.NRestSiteButton");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "SelectOption" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance, object[] __args)
        {
            try
            {
                // First arg is the RestSiteOption that was selected
                var option = __args?.Length > 0 ? __args[0] : __instance;
                var optTraverse = Traverse.Create(option);
                var choiceId = (GameStateApi.GetProp(option, "OptionId")
                    ?? GameStateApi.GetField(option, "_optionId"))?.ToString()
                    ?? option.GetType().Name.Replace("RestSiteOption", "").ToLowerInvariant();
                var choiceName = (GameStateApi.GetProp(option, "Title")
                    ?? GameStateApi.GetField(option, "_title"))?.ToString()
                    ?? option.GetType().Name.Replace("RestSiteOption", "");

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "rest_choice",
                    Data = new { choiceId, choiceName },
                });

                // Clear rest state after choice
                Plugin.StateTracker?.UpdateState(state =>
                {
                    state.RestOptions = null;
                });

                GD.Print($"[SpireSense] Rest choice made: {choiceName}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] RestPatch OnRestChoice error: {ex.Message}");
            }
        }
    }
}
