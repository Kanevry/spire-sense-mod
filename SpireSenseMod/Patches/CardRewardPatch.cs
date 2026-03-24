using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

// MOD-001: All Traverse operations go through GameStateApi helpers.

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for the card reward screen.
/// Intercepts when cards are offered and when the player picks/skips.
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
///
/// Decompiled from NCardRewardSelectionScreen (sts2.dll v0.99.x):
///   - ShowScreen(IReadOnlyList&lt;CardCreationResult&gt; options, IReadOnlyList&lt;CardRewardAlternative&gt; extraOptions) — static
///   - SelectCard(NCardHolder cardHolder) — private instance
///   - Field _options: IReadOnlyList&lt;CardCreationResult&gt;
///   - CardCreationResult.Card → CardModel
///   - NCardHolder.CardModel → CardModel (property via CardNode?.Model)
/// </summary>
public static class CardRewardPatch
{
    private const string ScreenTypeName = "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen";

    /// <summary>
    /// Postfix patch: fires when card rewards are displayed.
    /// Captures the offered cards directly from the method parameter (options).
    ///
    /// TARGET: NCardRewardSelectionScreen.ShowScreen (static)
    /// Signature: static NCardRewardSelectionScreen? ShowScreen(IReadOnlyList&lt;CardCreationResult&gt; options, IReadOnlyList&lt;CardRewardAlternative&gt; extraOptions)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnCardRewardsShown
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName(ScreenTypeName);
            if (type == null)
            {
                GD.PrintErr($"[SpireSense] CardRewardPatch: Could not find type {ScreenTypeName}");
                return null;
            }
            // ShowScreen is a public static method with 2 parameters
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "ShowScreen" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
            if (method == null)
            {
                GD.PrintErr("[SpireSense] CardRewardPatch: Could not find ShowScreen method");
            }
            else
            {
                GD.Print($"[SpireSense] CardRewardPatch: Targeting {method.DeclaringType?.Name}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
            }
            return method;
        }

        /// <summary>
        /// ShowScreen is static, so __instance is not available.
        /// __result is the returned NCardRewardSelectionScreen instance (may be null in TestMode).
        /// __0 is the first parameter: IReadOnlyList&lt;CardCreationResult&gt; options.
        /// We read cards from the options parameter directly, then fall back to
        /// the instance _options field if __0 is null.
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(object? __result, object? __0)
        {
            try
            {
                // Primary: read cards from the method parameter (options)
                object? cards = __0;

                // Fallback: read _options field from the returned screen instance
                if (cards == null && __result != null)
                {
                    var fieldVal = GameStateApi.GetField(__result, "_options");
                    if (fieldVal != null)
                    {
                        cards = fieldVal;
                        GD.Print("[SpireSense] CardRewardPatch: Using _options field fallback");
                    }
                }

                if (cards == null)
                {
                    GD.PrintErr("[SpireSense] CardRewardPatch: No card data found (both parameter and _options field are null)");
                    return;
                }

                var cardInfos = new List<CardInfo>();

                if (cards is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item == null) continue;

                        // CardCreationResult has a .Card property → CardModel
                        var cardModel = GameStateApi.GetProp(item, "Card");

                        if (cardModel == null)
                        {
                            // Fallback: try field originalCard (public readonly)
                            cardModel = GameStateApi.GetField(item, "originalCard");
                        }

                        if (cardModel == null)
                        {
                            GD.Print($"[SpireSense] CardRewardPatch: Could not extract CardModel from {item.GetType().Name}, using raw object");
                            cardModel = item;
                        }

                        cardInfos.Add(GameStateApi.ExtractCardInfo(cardModel));
                    }
                }

                if (cardInfos.Count == 0)
                {
                    GD.Print("[SpireSense] CardRewardPatch: Enumerable was empty or not IEnumerable");
                    return;
                }

                Plugin.StateTracker?.SetCardRewards(cardInfos);
                Plugin.StateTracker?.SetScreen(ScreenType.CardReward);
                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "card_rewards_shown",
                    Data = new { cards = cardInfos },
                });

                // Update overlay with tier badges
                Plugin.Overlay?.ShowCardTiers(cardInfos);

                GD.Print($"[SpireSense] Card rewards: {cardInfos.Count} cards offered");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] CardRewardPatch error: {ex.Message}");
                GD.PrintErr($"[SpireSense] CardRewardPatch stack: {ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix patch: fires when a card is picked from rewards.
    ///
    /// TARGET: NCardRewardSelectionScreen.SelectCard(NCardHolder cardHolder)
    /// This is a private instance method.
    /// NCardHolder.CardModel is a virtual property (CardNode?.Model).
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnCardPicked
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName(ScreenTypeName);
            if (type == null)
            {
                GD.PrintErr($"[SpireSense] CardRewardPatch.OnCardPicked: Could not find type {ScreenTypeName}");
                return null;
            }
            // SelectCard is a private instance method with 1 parameter (NCardHolder)
            var method = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "SelectCard" && !m.IsGenericMethod)
                .FirstOrDefault();
            if (method == null)
            {
                // Fallback: also check public instance in case visibility changed
                method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "SelectCard" && !m.IsGenericMethod)
                    .FirstOrDefault();
            }
            if (method == null)
            {
                GD.PrintErr("[SpireSense] CardRewardPatch.OnCardPicked: Could not find SelectCard method");
            }
            else
            {
                GD.Print($"[SpireSense] CardRewardPatch.OnCardPicked: Targeting {method.DeclaringType?.Name}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
            }
            return method;
        }

        [HarmonyPostfix]
        static void Postfix(object __instance, object[] __args)
        {
            try
            {
                // First arg is NCardHolder (the cardHolder parameter)
                if (__args.Length == 0 || __args[0] == null)
                {
                    GD.PrintErr("[SpireSense] CardPicked: No arguments received");
                    return;
                }
                var cardHolder = __args[0];

                // NCardHolder.CardModel is a virtual property (not a field)
                // It accesses CardNode?.Model under the hood
                var cardModel = GameStateApi.GetProp(cardHolder, "CardModel");

                if (cardModel == null)
                {
                    // Fallback: try CardNode.Model path
                    var cardNode = GameStateApi.GetProp(cardHolder, "CardNode");
                    if (cardNode != null)
                    {
                        cardModel = GameStateApi.GetProp(cardNode, "Model");
                    }
                }

                if (cardModel == null)
                {
                    GD.PrintErr($"[SpireSense] CardPicked: Could not extract CardModel from {cardHolder.GetType().Name}");
                    return;
                }

                var cardInfo = GameStateApi.ExtractCardInfo(cardModel);
                var alternatives = Plugin.StateTracker?.GetCurrentState().CardRewards ?? new List<CardInfo>();

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "card_picked",
                    Data = new { card = cardInfo, alternatives },
                });

                Plugin.StateTracker?.SetCardRewards(null);
                Plugin.Overlay?.HideCardTiers();

                GD.Print($"[SpireSense] Card picked: {cardInfo.Name}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] CardPicked error: {ex.Message}");
                GD.PrintErr($"[SpireSense] CardPicked stack: {ex.StackTrace}");
            }
        }
    }
}
