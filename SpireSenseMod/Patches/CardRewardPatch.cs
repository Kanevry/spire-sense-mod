using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for the card reward screen.
/// Intercepts when cards are offered and when the player picks/skips.
///
/// NOTE: The target class and method names are placeholders based on STS2
/// decompiled patterns. These MUST be verified against actual game assemblies
/// and updated as the game evolves during Early Access.
///
/// Known STS2 patterns from sts2-advisor/BetterSpire2:
/// - Card rewards are managed by a reward screen/panel class
/// - Cards are presented as a list of selectable options
/// - A callback fires when a card is selected or skipped
/// </summary>
public static class CardRewardPatch
{
    /// <summary>
    /// Postfix patch: fires when card rewards are displayed.
    /// Captures the offered cards and updates the state tracker.
    ///
    /// TARGET: The method that populates the card reward UI.
    /// This needs to be identified via decompilation of the game DLL.
    /// Example: [HarmonyPatch(typeof(CardRewardScreen), "ShowRewards")]
    /// </summary>
    // [HarmonyPatch(typeof(CardRewardScreen), "ShowRewards")]
    // [HarmonyPostfix]
    public static void OnCardRewardsShown(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);
            var cards = traverse.Field("rewardCards")?.GetValue<object>();

            if (cards == null) return;

            var cardInfos = new List<CardInfo>();

            // Extract card info from each reward option
            if (cards is System.Collections.IEnumerable enumerable)
            {
                foreach (var card in enumerable)
                {
                    cardInfos.Add(GameStateApi.ExtractCardInfo(card));
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
    /// </summary>
    // [HarmonyPatch(typeof(CardRewardScreen), "OnCardSelected")]
    // [HarmonyPostfix]
    public static void OnCardPicked(object __instance, object selectedCard)
    {
        try
        {
            var cardInfo = GameStateApi.ExtractCardInfo(selectedCard);
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
