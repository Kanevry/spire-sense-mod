using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

// MOD-001: All Traverse operations go through GameStateApi helpers.

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for rest site interactions.
/// Intercepts rest site choice selection (heal, smith, dig, etc.).
///
/// HARMONY-ONLY — NO HOOK EQUIVALENT (GitLab #126 audit, 2026-03-30):
/// The STS2 Hook system does not expose a hook for rest site choice selection:
///   - Hook.AfterRoomEntered fires when entering a RestSiteRoom, but BEFORE the player
///     selects an option. HookSubscriptions uses this to detect the rest site and extract
///     available options (rest_entered event).
///   - No AfterRestChoiceSelected, BeforeRestChoiceSelected, or AfterRestSiteOptionUsed
///     hook exists in MegaCrit.Sts2.Core.Hooks.Hook.
///   - NRestSiteButton.SelectOption is a UI button handler — the only way to intercept
///     the player's choice is via Harmony patch.
///
/// Relationship with HookSubscriptions:
///   - OnAfterRoomEntered detects RestSiteRoom and emits "rest_entered" with options.
///     This is COMPLEMENTARY: rest_entered (Hook) fires on room entry, rest_choice (Patch)
///     fires on button press. No duplicate emission occurs.
///
/// State mutations are delegated to HookEventAdapter for testability (GitLab #126).
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
            if (type == null)
            {
                GD.PrintErr("[SpireSense] RestPatch.OnRestChoice: Could not resolve target type MegaCrit.Sts2.Core.Nodes.RestSite.NRestSiteButton");
                return null;
            }
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
                var choiceId = (GameStateApi.GetProp(option, "OptionId")
                    ?? GameStateApi.GetField(option, "_optionId"))?.ToString()
                    ?? option.GetType().Name.Replace("RestSiteOption", "").ToLowerInvariant();
                var choiceName = (GameStateApi.GetProp(option, "Title")
                    ?? GameStateApi.GetField(option, "_title"))?.ToString()
                    ?? option.GetType().Name.Replace("RestSiteOption", "");

                // Delegate state mutations to the testable adapter (GitLab #126)
                Plugin.Adapter?.HandleRestChoice(choiceId, choiceName);

                GD.Print($"[SpireSense] Rest choice made: {choiceName}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] RestPatch OnRestChoice error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
