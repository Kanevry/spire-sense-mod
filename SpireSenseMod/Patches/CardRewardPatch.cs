using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for the card reward screen.
/// Intercepts when cards are offered and when the player picks/skips.
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
/// </summary>
public static class CardRewardPatch
{
    /// <summary>
    /// Postfix patch: fires when card rewards are displayed.
    /// Captures the offered cards and updates the state tracker.
    ///
    /// TARGET: NCardRewardSelectionScreen.ShowScreen (static)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnCardRewardsShown
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                .Where(m => m.Name == "ShowScreen" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                // STS2 uses CardCreationResult list passed to ShowScreen
                var cards = traverse.Field("_options")?.GetValue<object>()
                    ?? traverse.Field("_cardHolders")?.GetValue<object>();

                if (cards == null) return;

                var cardInfos = new List<CardInfo>();

                // Extract card info from each reward option
                if (cards is System.Collections.IEnumerable enumerable)
                {
                    foreach (var card in enumerable)
                    {
                        // Each holder may wrap a CardModel — try to extract it
                        var holderTraverse = Traverse.Create(card);
                        var cardModel = holderTraverse.Field("CardModel")?.GetValue<object>()
                            ?? holderTraverse.Property("CardModel")?.GetValue<object>()
                            ?? card;
                        cardInfos.Add(GameStateApi.ExtractCardInfo(cardModel));
                    }
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
            }
        }
    }

    /// <summary>
    /// Postfix patch: fires when a card is picked from rewards.
    ///
    /// TARGET: NCardRewardSelectionScreen.SelectCard(NCardHolder)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnCardPicked
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "SelectCard" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance, object[] __args)
        {
            try
            {
                // First arg is NCardHolder (the cardHolder parameter)
                if (__args.Length == 0 || __args[0] == null) return;
                var cardHolder = __args[0];

                // cardHolder is NCardHolder — extract the CardModel from it
                var holderTraverse = Traverse.Create(cardHolder);
                var cardModel = holderTraverse.Field("CardModel")?.GetValue<object>()
                    ?? holderTraverse.Property("CardModel")?.GetValue<object>()
                    ?? cardHolder;

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
            }
        }
    }
}
