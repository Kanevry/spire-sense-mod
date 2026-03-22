using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for the card reward screen.
/// Intercepts when cards are offered and when the player picks/skips.
///
/// STS2 classes (from sts2.dll decompilation):
/// - NCardRewardSelectionScreen (MegaCrit.Sts2.Core.Nodes.Screens.CardSelection)
///   - ShowScreen() — static, shows reward screen
///   - SelectCard(NCardHolder) — player picks a card
/// </summary>
public static class CardRewardPatch
{
    /// <summary>
    /// Postfix patch: fires when card rewards are displayed.
    /// Captures the offered cards and updates the state tracker.
    ///
    /// TARGET: NCardRewardSelectionScreen.ShowScreen (static)
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen", "ShowScreen")]
    [HarmonyPostfix]
    public static void OnCardRewardsShown(object __instance)
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

    /// <summary>
    /// Postfix patch: fires when a card is picked from rewards.
    ///
    /// TARGET: NCardRewardSelectionScreen.SelectCard(NCardHolder)
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen", "SelectCard")]
    [HarmonyPostfix]
    public static void OnCardPicked(object __instance, object cardHolder)
    {
        try
        {
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
