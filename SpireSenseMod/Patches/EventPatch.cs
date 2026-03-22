using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for in-game event encounters.
/// Intercepts event start and choice selection.
///
/// NOTE: The target class and method names are placeholders based on STS2
/// decompiled patterns. These MUST be verified against actual game assemblies
/// and updated as the game evolves during Early Access.
///
/// Known STS2 patterns from sts2-advisor/BetterSpire2:
/// - Events present a narrative screen with multiple choice options
/// - Each option has text and may be conditionally enabled/disabled
/// - A callback fires when an option is selected
/// </summary>
public static class EventPatch
{
    /// <summary>
    /// Postfix patch: fires when an event encounter starts.
    /// Captures event name, description, and available options.
    ///
    /// TARGET: The method that displays the event UI.
    /// This needs to be identified via decompilation of the game DLL.
    /// Example: [HarmonyPatch(typeof(EventScreen), "ShowEvent")]
    /// </summary>
    // [HarmonyPatch(typeof(EventScreen), "ShowEvent")]
    // [HarmonyPostfix]
    public static void OnEventStarted(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);
            var eventName = traverse.Field("eventName")?.GetValue<string>()
                ?? traverse.Field("name")?.GetValue<string>()
                ?? "Unknown Event";
            var description = traverse.Field("description")?.GetValue<string>()
                ?? traverse.Field("bodyText")?.GetValue<string>()
                ?? "";

            // Extract event options
            var eventOptions = new List<EventOption>();
            var options = traverse.Field("options")?.GetValue<object>()
                ?? traverse.Field("choices")?.GetValue<object>();

            if (options is System.Collections.IEnumerable enumerable)
            {
                var index = 0;
                foreach (var option in enumerable)
                {
                    var optTraverse = Traverse.Create(option);
                    eventOptions.Add(new EventOption
                    {
                        Id = optTraverse.Field("id")?.GetValue<string>() ?? $"option_{index}",
                        Text = optTraverse.Field("text")?.GetValue<string>()
                            ?? optTraverse.Field("label")?.GetValue<string>()
                            ?? "",
                        Enabled = optTraverse.Field("enabled")?.GetValue<bool>()
                            ?? optTraverse.Field("isAvailable")?.GetValue<bool>()
                            ?? true,
                    });
                    index++;
                }
            }

            Plugin.StateTracker?.UpdateState(state =>
            {
                state.Screen = ScreenType.Event;
                state.EventOptions = eventOptions;
            });

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "event_started",
                Data = new { name = eventName, description, options = eventOptions },
            });

            GD.Print($"[SpireSense] Event started: {eventName} ({eventOptions.Count} options)");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] EventPatch OnEventStarted error: {ex.Message}");
        }
    }

    /// <summary>
    /// Postfix patch: fires when the player selects an event option.
    /// Emits the chosen option for analytics tracking.
    ///
    /// TARGET: The method called when a choice button is pressed.
    /// Example: [HarmonyPatch(typeof(EventScreen), "OnChoiceSelected")]
    /// </summary>
    // [HarmonyPatch(typeof(EventScreen), "OnChoiceSelected")]
    // [HarmonyPostfix]
    public static void OnEventChoiceMade(object __instance, int choiceIndex)
    {
        try
        {
            var currentOptions = Plugin.StateTracker?.GetCurrentState().EventOptions;
            var chosenOption = currentOptions != null && choiceIndex >= 0 && choiceIndex < currentOptions.Count
                ? currentOptions[choiceIndex]
                : null;

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "event_choice",
                Data = new
                {
                    choiceIndex,
                    option = chosenOption,
                },
            });

            // Clear event state after choice
            Plugin.StateTracker?.UpdateState(state =>
            {
                state.EventOptions = null;
            });

            GD.Print($"[SpireSense] Event choice made: option {choiceIndex}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] EventPatch OnEventChoiceMade error: {ex.Message}");
        }
    }
}
