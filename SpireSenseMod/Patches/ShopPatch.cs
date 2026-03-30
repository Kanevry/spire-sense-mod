using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for the shop screen.
/// Intercepts shop exit and clears shop state.
///
/// HARMONY-ONLY — NO HOOK EQUIVALENT (GitLab #126 audit, 2026-03-30):
/// The STS2 Hook system does not expose a hook for shop exit:
///   - Hook.AfterRoomEntered fires when entering a MerchantRoom, and HookSubscriptions
///     uses this to detect the shop, extract inventory (cards/relics), set Screen=Shop,
///     and emit a floor_changed event.
///   - No AfterShopExited, BeforeShopExited, or AfterMerchantRoomExit hook exists in
///     MegaCrit.Sts2.Core.Hooks.Hook.
///   - MerchantRoom.Exit(IRunState?) is a room lifecycle method — the only way to
///     intercept shop exit is via Harmony patch.
///
/// Relationship with HookSubscriptions:
///   - OnAfterRoomEntered handles shop ENTRY (inventory extraction + Screen=Shop).
///     This patch handles shop EXIT (clear shop data + Screen=Map).
///     These are complementary lifecycle events with no overlap.
///
/// State mutations are delegated to HookEventAdapter for testability (GitLab #126).
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
/// </summary>
public static class ShopPatch
{
    /// <summary>
    /// Postfix patch: fires when the merchant room is exited.
    /// Clears shop data and resets screen.
    ///
    /// TARGET: MerchantRoom.Exit(IRunState?)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnShopExited
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Rooms.MerchantRoom");
            if (type == null)
            {
                GD.PrintErr("[SpireSense] ShopPatch.OnShopExited: Could not resolve target type MegaCrit.Sts2.Core.Rooms.MerchantRoom");
                return null;
            }
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "Exit" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                // Delegate state mutations to the testable adapter (GitLab #126)
                Plugin.Adapter?.HandleShopExited();

                GD.Print("[SpireSense] Shop exited");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] ShopPatch OnShopExited error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
