using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for rest site interactions.
/// Intercepts rest site entry and choice selection (heal, smith, dig, etc.).
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
/// </summary>
public static class RestPatch
{
    /// <summary>
    /// Postfix patch: fires when a rest site room is entered.
    /// Captures available rest options based on relics and game state.
    ///
    /// TARGET: RestSiteRoom.Enter(IRunState?, bool)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnRestEntered
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Rooms.RestSiteRoom");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "Enter" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);

                // RestSiteRoom may store available options or we need to get them from the room
                var restOptions = new List<RestOption>();
                var options = GameStateApi.GetCollection(__instance, "Options", "_options");

                if (options is System.Collections.IEnumerable enumerable)
                {
                    foreach (var option in enumerable)
                    {
                        var optTraverse = Traverse.Create(option);
                        restOptions.Add(new RestOption
                        {
                            Id = (optTraverse.Property("OptionId")?.GetValue<object>()
                                ?? optTraverse.Field("_optionId")?.GetValue<object>())?.ToString()
                                ?? option.GetType().Name.Replace("RestSiteOption", "").ToLowerInvariant(),
                            Name = (optTraverse.Property("Title")?.GetValue<object>()
                                ?? optTraverse.Field("_title")?.GetValue<object>())?.ToString()
                                ?? option.GetType().Name.Replace("RestSiteOption", ""),
                            Description = (optTraverse.Property("Description")?.GetValue<object>()
                                ?? optTraverse.Field("_description")?.GetValue<object>())?.ToString() ?? "",
                            Enabled = optTraverse.Property("IsEnabled")?.GetValue<bool>()
                                ?? optTraverse.Field("_isEnabled")?.GetValue<bool>()
                                ?? true,
                        });
                    }
                }

                Plugin.StateTracker?.UpdateState(state =>
                {
                    state.Screen = ScreenType.Rest;
                    state.RestOptions = restOptions;
                });

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "rest_entered",
                    Data = new { options = restOptions },
                });

                GD.Print($"[SpireSense] Rest site entered: {restOptions.Count} options available");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] RestPatch OnRestEntered error: {ex.Message}");
            }
        }
    }

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
