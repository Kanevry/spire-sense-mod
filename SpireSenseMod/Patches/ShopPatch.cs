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
/// Migrated to hooks (HookEventAdapter):
///   - OnShopEntered → HandleShopEntered (via AfterRoomEntered hook, MerchantRoom detection)
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
            if (type == null) return null;
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
                Plugin.StateTracker?.UpdateState(state =>
                {
                    state.Screen = ScreenType.Map;
                    state.ShopCards = null;
                    state.ShopRelics = null;
                });

                GD.Print("[SpireSense] Shop exited");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] ShopPatch OnShopExited error: {ex.Message}");
            }
        }
    }
}
