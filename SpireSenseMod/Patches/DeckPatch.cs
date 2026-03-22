using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for deck/relic state tracking.
/// Updates the state tracker when cards are added/removed and relics obtained.
///
/// NOTE: Target classes are placeholders.
/// </summary>
public static class DeckPatch
{
    /// <summary>
    /// Postfix: Card added to deck.
    /// </summary>
    // [HarmonyPatch(typeof(DeckManager), "AddCard")]
    // [HarmonyPostfix]
    public static void OnCardAdded(object __instance, object card)
    {
        try
        {
            var cardInfo = GameStateApi.ExtractCardInfo(card);

            Plugin.StateTracker?.UpdateState(state =>
            {
                state.Deck.Add(cardInfo);
            });

            GD.Print($"[SpireSense] Card added to deck: {cardInfo.Name}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] CardAdded error: {ex.Message}");
        }
    }

    /// <summary>
    /// Postfix: Card removed from deck (shop, event, etc.).
    /// </summary>
    // [HarmonyPatch(typeof(DeckManager), "RemoveCard")]
    // [HarmonyPostfix]
    public static void OnCardRemoved(object __instance, object card)
    {
        try
        {
            var cardInfo = GameStateApi.ExtractCardInfo(card);

            Plugin.StateTracker?.UpdateState(state =>
            {
                state.Deck.RemoveAll(c => c.Id == cardInfo.Id);
            });

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "card_removed",
                Data = new { card = cardInfo },
            });

            GD.Print($"[SpireSense] Card removed from deck: {cardInfo.Name}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] CardRemoved error: {ex.Message}");
        }
    }

    /// <summary>
    /// Postfix: Relic obtained.
    /// </summary>
    // [HarmonyPatch(typeof(RelicManager), "ObtainRelic")]
    // [HarmonyPostfix]
    public static void OnRelicObtained(object __instance, object relic)
    {
        try
        {
            var relicInfo = GameStateApi.ExtractRelicInfo(relic);

            Plugin.StateTracker?.UpdateState(state =>
            {
                state.Relics.Add(relicInfo);
            });

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "relic_obtained",
                Data = new { relic = relicInfo },
            });

            GD.Print($"[SpireSense] Relic obtained: {relicInfo.Name}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] RelicObtained error: {ex.Message}");
        }
    }

    /// <summary>
    /// Postfix: Run starts — initialize state.
    /// </summary>
    // [HarmonyPatch(typeof(RunManager), "StartRun")]
    // [HarmonyPostfix]
    public static void OnRunStart(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);
            var rawCharacter = traverse.Field("character")?.GetValue<string>();
            var character = CharacterValidator.Validate(rawCharacter);
            var ascension = traverse.Field("ascension")?.GetValue<int>() ?? 0;
            var seed = traverse.Field("seed")?.GetValue<string>() ?? "";

            Plugin.StateTracker?.SetState(new GameState
            {
                Screen = ScreenType.Map,
                Character = character,
                Act = 1,
                Floor = 0,
                Ascension = ascension,
                Seed = seed,
                Deck = new List<CardInfo>(),
                Relics = new List<RelicInfo>(),
            });

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "run_start",
                Data = new { character, ascension, seed },
            });

            GD.Print($"[SpireSense] Run started: {character} A{ascension}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] RunStart error: {ex.Message}");
        }
    }

    /// <summary>
    /// Postfix: Run ends — record result.
    /// </summary>
    // [HarmonyPatch(typeof(RunManager), "EndRun")]
    // [HarmonyPostfix]
    public static void OnRunEnd(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);
            var won = traverse.Field("victory")?.GetValue<bool>() ?? false;
            var floor = Plugin.StateTracker?.GetCurrentState().Floor ?? 0;
            var score = traverse.Field("score")?.GetValue<int>() ?? 0;

            Plugin.StateTracker?.SetScreen(won ? ScreenType.Victory : ScreenType.GameOver);

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "run_end",
                Data = new { won, floor, score },
            });

            GD.Print($"[SpireSense] Run ended: {(won ? "Victory" : "Defeat")} at floor {floor}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] RunEnd error: {ex.Message}");
        }
    }
}
