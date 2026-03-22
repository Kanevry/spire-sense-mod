using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for the shop screen.
/// Intercepts shop entry/exit and extracts available cards, relics, and prices.
///
/// NOTE: The target class and method names are placeholders based on STS2
/// decompiled patterns. These MUST be verified against actual game assemblies
/// and updated as the game evolves during Early Access.
///
/// Known STS2 patterns from sts2-advisor/BetterSpire2:
/// - Shop is a distinct screen with card/relic inventories
/// - Items have associated gold prices
/// - A callback fires when the player enters/exits the shop
/// </summary>
public static class ShopPatch
{
    /// <summary>
    /// Postfix patch: fires when the shop screen is entered.
    /// Captures available cards, relics, and their prices.
    ///
    /// TARGET: The method that initializes the shop UI with items.
    /// This needs to be identified via decompilation of the game DLL.
    /// Example: [HarmonyPatch(typeof(ShopScreen), "OnEnter")]
    /// </summary>
    // [HarmonyPatch(typeof(ShopScreen), "OnEnter")]
    // [HarmonyPostfix]
    public static void OnShopEntered(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);

            // Extract shop cards
            var shopCards = new List<ShopItem>();
            var cards = traverse.Field("shopCards")?.GetValue<object>();
            if (cards is System.Collections.IEnumerable cardEnum)
            {
                foreach (var item in cardEnum)
                {
                    var itemTraverse = Traverse.Create(item);
                    var cardInfo = GameStateApi.ExtractCardInfo(
                        itemTraverse.Field("card")?.GetValue<object>() ?? item
                    );
                    var price = itemTraverse.Field("price")?.GetValue<int>() ?? 0;

                    shopCards.Add(new ShopItem
                    {
                        Card = cardInfo,
                        Price = price,
                    });
                }
            }

            // Extract shop relics
            var shopRelics = new List<ShopRelicItem>();
            var relics = traverse.Field("shopRelics")?.GetValue<object>();
            if (relics is System.Collections.IEnumerable relicEnum)
            {
                foreach (var item in relicEnum)
                {
                    var itemTraverse = Traverse.Create(item);
                    var relicInfo = GameStateApi.ExtractRelicInfo(
                        itemTraverse.Field("relic")?.GetValue<object>() ?? item
                    );
                    var price = itemTraverse.Field("price")?.GetValue<int>() ?? 0;

                    shopRelics.Add(new ShopRelicItem
                    {
                        Relic = relicInfo,
                        Price = price,
                    });
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

    /// <summary>
    /// Postfix patch: fires when the shop screen is exited.
    /// Clears shop data and resets screen.
    ///
    /// TARGET: The method that closes the shop UI.
    /// Example: [HarmonyPatch(typeof(ShopScreen), "OnExit")]
    /// </summary>
    // [HarmonyPatch(typeof(ShopScreen), "OnExit")]
    // [HarmonyPostfix]
    public static void OnShopExited(object __instance)
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
