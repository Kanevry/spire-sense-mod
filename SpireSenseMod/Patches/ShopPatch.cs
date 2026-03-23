using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for the shop screen.
/// Intercepts shop entry/exit and extracts available cards, relics, and prices.
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
/// </summary>
public static class ShopPatch
{
    /// <summary>
    /// Postfix patch: fires when the merchant room is entered.
    /// Captures available cards, relics, and their prices.
    ///
    /// TARGET: MerchantRoom.Enter(IRunState?, bool)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnShopEntered
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Rooms.MerchantRoom");
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

                // MerchantRoom should have a MerchantInventory
                var inventory = traverse.Field("_inventory")?.GetValue<object>()
                    ?? traverse.Property("Inventory")?.GetValue<object>();

                var shopCards = new List<ShopItem>();
                var shopRelics = new List<ShopRelicItem>();

                if (inventory != null)
                {
                    var invTraverse = Traverse.Create(inventory);

                    // Extract card entries (MerchantCardEntry)
                    var cardEntries = invTraverse.Property("CardEntries")?.GetValue<object>()
                        ?? invTraverse.Field("_cardEntries")?.GetValue<object>();
                    if (cardEntries is System.Collections.IEnumerable cardEnum)
                    {
                        foreach (var entry in cardEnum)
                        {
                            var entryTraverse = Traverse.Create(entry);
                            var cardModel = entryTraverse.Property("CardModel")?.GetValue<object>()
                                ?? entryTraverse.Field("_cardModel")?.GetValue<object>();
                            var price = entryTraverse.Property("Price")?.GetValue<int>()
                                ?? entryTraverse.Field("_price")?.GetValue<int>()
                                ?? 0;

                            if (cardModel != null)
                            {
                                shopCards.Add(new ShopItem
                                {
                                    Card = GameStateApi.ExtractCardInfo(cardModel),
                                    Price = price,
                                });
                            }
                        }
                    }

                    // Extract relic entries (MerchantRelicEntry)
                    var relicEntries = invTraverse.Property("RelicEntries")?.GetValue<object>()
                        ?? invTraverse.Field("_relicEntries")?.GetValue<object>();
                    if (relicEntries is System.Collections.IEnumerable relicEnum)
                    {
                        foreach (var entry in relicEnum)
                        {
                            var entryTraverse = Traverse.Create(entry);
                            var relicModel = entryTraverse.Property("RelicModel")?.GetValue<object>()
                                ?? entryTraverse.Field("_relicModel")?.GetValue<object>();
                            var price = entryTraverse.Property("Price")?.GetValue<int>()
                                ?? entryTraverse.Field("_price")?.GetValue<int>()
                                ?? 0;

                            if (relicModel != null)
                            {
                                shopRelics.Add(new ShopRelicItem
                                {
                                    Relic = GameStateApi.ExtractRelicInfo(relicModel),
                                    Price = price,
                                });
                            }
                        }
                    }
                }

                // Update state with shop data
                var cardInfos = new List<CardInfo>();
                foreach (var shopCard in shopCards)
                {
                    cardInfos.Add(shopCard.Card);
                }

                var relicInfos = new List<RelicInfo>();
                foreach (var shopRelic in shopRelics)
                {
                    relicInfos.Add(shopRelic.Relic);
                }

                Plugin.StateTracker?.UpdateState(state =>
                {
                    state.Screen = ScreenType.Shop;
                    state.ShopCards = cardInfos;
                    state.ShopRelics = relicInfos;
                });

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "floor_changed",
                    Data = new { screen = ScreenType.Shop, shopCards, shopRelics },
                });

                GD.Print($"[SpireSense] Shop entered: {shopCards.Count} cards, {shopRelics.Count} relics");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] ShopPatch OnShopEntered error: {ex.Message}");
            }
        }
    }

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
