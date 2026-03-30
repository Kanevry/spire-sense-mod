using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

// MOD-001: All Traverse operations go through GameStateApi helpers.

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for in-game event encounters.
/// Intercepts event start and choice selection.
///
/// HARMONY-ONLY — NO HOOK EQUIVALENT (GitLab #126 analysis, 2026-03-30):
/// The STS2 Hook system does not expose event-specific hooks:
///   - Hook.AfterRoomEntered(IRunState, AbstractRoom) — fires when the player enters a room,
///     but this occurs BEFORE EventModel.BeginEvent() populates the event name, description,
///     and available options. The room object at that point has no usable event data.
///   - No Hook.AfterEventStarted, Hook.BeforeEventStarted, or Hook.AfterEventOptionSelected
///     exists in MegaCrit.Sts2.Core.Hooks.Hook.
///
/// Neither available hook provides:
///   1. The EventModel with Name, Description, and CurrentOptions (populated by BeginEvent)
///   2. The NEventOptionButton context (the specific option the player clicked)
///
/// Relationship with HookSubscriptions:
///   - OnAfterRoomEntered detects MerchantRoom and RestSiteRoom but does NOT detect event rooms.
///     There is no duplicate event emission — AfterRoomEntered sets Screen=Map for all unrecognized
///     room types, and this patch later overrides it to Screen=Event when BeginEvent fires.
///   - This timing gap (Map → Event) is correct and expected: the overlay shows the map briefly
///     while the event UI loads, then transitions to the event screen.
///
/// State mutations are delegated to HookEventAdapter for testability (GitLab #126).
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
///
/// Decompiled from EventModel + NEventOptionButton (sts2.dll v0.99.x):
///   - EventModel.BeginEvent(Player, bool) — instance method, populates CurrentOptions
///   - EventModel.Name / EventId — string properties
///   - EventModel.Description / _description — event flavor text
///   - EventModel.CurrentOptions / _currentOptions — IReadOnlyList of option objects
///   - NEventOptionButton.OnRelease() — instance method, UI button press handler
///   - NEventOptionButton.Event — the parent EventModel
///   - NEventOptionButton.Option — the selected option with OptionId/Title/IsEnabled
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
            if (type == null)
            {
                GD.PrintErr("[SpireSense] EventPatch.OnEventStarted: Could not resolve target type MegaCrit.Sts2.Core.Models.EventModel");
                return null;
            }
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
                // EventModel has name/description via properties or type name
                var eventName = (GameStateApi.GetProp(__instance, "Name")
                    ?? GameStateApi.GetProp(__instance, "EventId"))?.ToString()
                    ?? __instance.GetType().Name;
                var description = (GameStateApi.GetProp(__instance, "Description")
                    ?? GameStateApi.GetField(__instance, "_description"))?.ToString() ?? "";

                // Extract event options from CurrentOptions
                var eventOptions = new List<EventOption>();
                var options = GameStateApi.GetCollection(__instance, "CurrentOptions", "_currentOptions");

                if (options is System.Collections.IEnumerable enumerable)
                {
                    var index = 0;
                    foreach (var option in enumerable)
                    {
                        eventOptions.Add(new EventOption
                        {
                            Id = (GameStateApi.GetProp(option, "OptionId")
                                ?? GameStateApi.GetField(option, "_optionId"))?.ToString()
                                ?? $"option_{index}",
                            Text = (GameStateApi.GetProp(option, "Title")
                                ?? GameStateApi.GetProp(option, "Text")
                                ?? GameStateApi.GetField(option, "_title"))?.ToString() ?? "",
                            Enabled = (bool?)GameStateApi.GetProp(option, "IsEnabled")
                                ?? (bool?)GameStateApi.GetField(option, "_isEnabled")
                                ?? true,
                        });
                        index++;
                    }
                }

                // Delegate state mutations to the testable adapter (GitLab #126)
                Plugin.Adapter?.HandleEventStarted(eventName, description, eventOptions);

                GD.Print($"[SpireSense] Event started: {eventName} ({eventOptions.Count} options)");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] EventPatch OnEventStarted error: {ex.Message}\n{ex.StackTrace}");
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
            if (type == null)
            {
                GD.PrintErr("[SpireSense] EventPatch.OnEventChoiceMade: Could not resolve target type MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton");
                return null;
            }
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
                // NEventOptionButton has Event and Option properties
                var optionObj = GameStateApi.GetProp(__instance, "Option");

                string? choiceId = null;
                string? choiceText = null;

                if (optionObj != null)
                {
                    choiceId = (GameStateApi.GetProp(optionObj, "OptionId")
                        ?? GameStateApi.GetField(optionObj, "_optionId"))?.ToString();
                    choiceText = (GameStateApi.GetProp(optionObj, "Title")
                        ?? GameStateApi.GetProp(optionObj, "Text"))?.ToString();
                }

                // Delegate state mutations to the testable adapter (GitLab #126)
                Plugin.Adapter?.HandleEventChoice(
                    choiceId ?? "unknown",
                    choiceText ?? "unknown");

                GD.Print($"[SpireSense] Event choice made: {choiceId ?? "unknown"}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] EventPatch OnEventChoiceMade error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
