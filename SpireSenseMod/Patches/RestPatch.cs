using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for rest site interactions.
/// Intercepts rest site entry and choice selection (heal, smith, dig, etc.).
///
/// STS2 classes (from sts2.dll decompilation):
/// - RestSiteRoom (MegaCrit.Sts2.Core.Rooms) — extends AbstractRoom
///   - Enter(IRunState?, bool) — enters rest site
///   - Exit(IRunState?) — exits rest site
/// - RestSiteOption (MegaCrit.Sts2.Core.Entities.RestSite) — abstract
///   - OnSelect() — async, executes the option
///   - Properties: OptionId, IsEnabled, Title, Description
///   - Subclasses: HealRestSiteOption, SmithRestSiteOption, DigRestSiteOption,
///     CookRestSiteOption, LiftRestSiteOption, MendRestSiteOption,
///     CloneRestSiteOption, HatchRestSiteOption
///   - static Generate(Player) — generates available options
/// - NRestSiteButton (MegaCrit.Sts2.Core.Nodes.RestSite)
///   - SelectOption(RestSiteOption) — async, selects option
/// </summary>
[HarmonyPriority(Priority.HigherThanNormal)]
public static class RestPatch
{
    /// <summary>
    /// Postfix patch: fires when a rest site room is entered.
    /// Captures available rest options based on relics and game state.
    ///
    /// TARGET: RestSiteRoom.Enter(IRunState?, bool)
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Rooms.RestSiteRoom", "Enter")]
    [HarmonyPostfix]
    public static void OnRestEntered(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);

            // RestSiteRoom may store available options or we need to get them from the room
            var restOptions = new List<RestOption>();
            var options = traverse.Property("Options")?.GetValue<object>()
                ?? traverse.Field("_options")?.GetValue<object>();

            if (options is System.Collections.IEnumerable enumerable)
            {
                foreach (var option in enumerable)
                {
                    var optTraverse = Traverse.Create(option);
                    restOptions.Add(new RestOption
                    {
                        Id = optTraverse.Property("OptionId")?.GetValue<string>()
                            ?? optTraverse.Field("_optionId")?.GetValue<string>()
                            ?? option.GetType().Name.Replace("RestSiteOption", "").ToLowerInvariant(),
                        Name = optTraverse.Property("Title")?.GetValue<string>()
                            ?? optTraverse.Field("_title")?.GetValue<string>()
                            ?? option.GetType().Name.Replace("RestSiteOption", ""),
                        Description = optTraverse.Property("Description")?.GetValue<string>()
                            ?? optTraverse.Field("_description")?.GetValue<string>()
                            ?? "",
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

    /// <summary>
    /// Postfix patch: fires when the player makes a rest site choice.
    /// Emits the chosen option for analytics tracking.
    ///
    /// TARGET: RestSiteOption.OnSelect() — the abstract method called when selected
    /// __instance IS the RestSiteOption subclass, so we can read its properties directly.
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption", "OnSelect")]
    [HarmonyPostfix]
    public static void OnRestChoice(object __instance)
    {
        try
        {
            var optTraverse = Traverse.Create(__instance);
            var choiceId = optTraverse.Property("OptionId")?.GetValue<string>()
                ?? optTraverse.Field("_optionId")?.GetValue<string>()
                ?? __instance.GetType().Name.Replace("RestSiteOption", "").ToLowerInvariant();
            var choiceName = optTraverse.Property("Title")?.GetValue<string>()
                ?? optTraverse.Field("_title")?.GetValue<string>()
                ?? __instance.GetType().Name.Replace("RestSiteOption", "");

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
