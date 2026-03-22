using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for rest site interactions.
/// Intercepts rest site entry and choice selection (heal, upgrade, etc.).
///
/// NOTE: The target class and method names are placeholders based on STS2
/// decompiled patterns. These MUST be verified against actual game assemblies
/// and updated as the game evolves during Early Access.
///
/// Known STS2 patterns from sts2-advisor/BetterSpire2:
/// - Rest sites present options like Rest (heal), Smith (upgrade), Recall, Lift, Toke, Dig
/// - Available options depend on relics and game state
/// - A callback fires when an option is selected
/// </summary>
public static class RestPatch
{
    /// <summary>
    /// Postfix patch: fires when a rest site is entered.
    /// Captures available rest options based on relics and game state.
    ///
    /// TARGET: The method that displays the rest site UI.
    /// This needs to be identified via decompilation of the game DLL.
    /// Example: [HarmonyPatch(typeof(RestScreen), "OnEnter")]
    /// </summary>
    // [HarmonyPatch(typeof(RestScreen), "OnEnter")]
    // [HarmonyPostfix]
    public static void OnRestEntered(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);

            // Extract available rest options
            var restOptions = new List<RestOption>();
            var options = traverse.Field("options")?.GetValue<object>()
                ?? traverse.Field("restOptions")?.GetValue<object>();

            if (options is System.Collections.IEnumerable enumerable)
            {
                foreach (var option in enumerable)
                {
                    var optTraverse = Traverse.Create(option);
                    restOptions.Add(new RestOption
                    {
                        Id = optTraverse.Field("id")?.GetValue<string>()
                            ?? optTraverse.Field("type")?.GetValue<string>()?.ToLowerInvariant()
                            ?? "",
                        Name = optTraverse.Field("name")?.GetValue<string>()
                            ?? optTraverse.Field("label")?.GetValue<string>()
                            ?? "",
                        Description = optTraverse.Field("description")?.GetValue<string>() ?? "",
                        Enabled = optTraverse.Field("enabled")?.GetValue<bool>()
                            ?? optTraverse.Field("isAvailable")?.GetValue<bool>()
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

    /// <summary>
    /// Postfix patch: fires when the player makes a rest site choice.
    /// Emits the chosen option for analytics tracking.
    ///
    /// TARGET: The method called when a rest option is selected.
    /// Example: [HarmonyPatch(typeof(RestScreen), "OnOptionSelected")]
    /// </summary>
    // [HarmonyPatch(typeof(RestScreen), "OnOptionSelected")]
    // [HarmonyPostfix]
    public static void OnRestChoice(object __instance, object selectedOption)
    {
        try
        {
            var optTraverse = Traverse.Create(selectedOption);
            var choiceId = optTraverse.Field("id")?.GetValue<string>()
                ?? optTraverse.Field("type")?.GetValue<string>()?.ToLowerInvariant()
                ?? "unknown";
            var choiceName = optTraverse.Field("name")?.GetValue<string>()
                ?? optTraverse.Field("label")?.GetValue<string>()
                ?? choiceId;

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
