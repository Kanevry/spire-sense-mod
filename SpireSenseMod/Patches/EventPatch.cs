using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for in-game event encounters.
/// Intercepts event start and choice selection.
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
/// </summary>
public static class EventPatch
{
    /// <summary>
    /// Postfix patch: fires when an event encounter starts.
    /// Captures event name, description, and available options.
    ///
    /// TARGET: EventModel.BeginEvent(Player, bool)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnEventStarted
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.EventModel");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "BeginEvent" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);

                // EventModel has name/description via properties or type name
                var eventName = traverse.Property("Name")?.GetValue<string>()
                    ?? traverse.Property("EventId")?.GetValue<string>()
                    ?? __instance.GetType().Name;
                var description = traverse.Property("Description")?.GetValue<string>()
                    ?? traverse.Field("_description")?.GetValue<string>()
                    ?? "";

                // Extract event options from CurrentOptions
                var eventOptions = new List<EventOption>();
                var options = traverse.Property("CurrentOptions")?.GetValue<object>()
                    ?? traverse.Field("_currentOptions")?.GetValue<object>();

                if (options is System.Collections.IEnumerable enumerable)
                {
                    var index = 0;
                    foreach (var option in enumerable)
                    {
                        var optTraverse = Traverse.Create(option);
                        eventOptions.Add(new EventOption
                        {
                            Id = optTraverse.Property("OptionId")?.GetValue<string>()
                                ?? optTraverse.Field("_optionId")?.GetValue<string>()
                                ?? $"option_{index}",
                            Text = optTraverse.Property("Title")?.GetValue<string>()
                                ?? optTraverse.Property("Text")?.GetValue<string>()
                                ?? optTraverse.Field("_title")?.GetValue<string>()
                                ?? "",
                            Enabled = optTraverse.Property("IsEnabled")?.GetValue<bool>()
                                ?? optTraverse.Field("_isEnabled")?.GetValue<bool>()
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
    }

    /// <summary>
    /// Postfix patch: fires when the player selects an event option.
    /// Emits the chosen option for analytics tracking.
    ///
    /// TARGET: NEventOptionButton.OnRelease() — the button press handler
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnEventChoiceMade
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "OnRelease" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);

                // NEventOptionButton has Event and Option properties
                var eventObj = traverse.Property("Event")?.GetValue<object>();
                var optionObj = traverse.Property("Option")?.GetValue<object>();

                string? choiceId = null;
                string? choiceText = null;

                if (optionObj != null)
                {
                    var optTraverse = Traverse.Create(optionObj);
                    choiceId = optTraverse.Property("OptionId")?.GetValue<string>()
                        ?? optTraverse.Field("_optionId")?.GetValue<string>();
                    choiceText = optTraverse.Property("Title")?.GetValue<string>()
                        ?? optTraverse.Property("Text")?.GetValue<string>();
                }

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "event_choice",
                    Data = new
                    {
                        choiceId = choiceId ?? "unknown",
                        choiceName = choiceText ?? "unknown",
                    },
                });

                // Clear event state after choice
                Plugin.StateTracker?.UpdateState(state =>
                {
                    state.EventOptions = null;
                });

                GD.Print($"[SpireSense] Event choice made: {choiceId ?? "unknown"}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] EventPatch OnEventChoiceMade error: {ex.Message}");
            }
        }
    }
}
