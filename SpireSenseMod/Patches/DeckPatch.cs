using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for deck/relic state tracking.
/// Updates the state tracker when cards are added/removed and relics obtained.
///
/// STS2 classes (from sts2.dll decompilation):
/// - CardPileCmd (MegaCrit.Sts2.Core.Commands) — static
///   - Add(CardModel, PileType, ...) — add card to pile
///   - RemoveFromDeck(CardModel, ...) — remove card from deck
/// - RelicCmd (MegaCrit.Sts2.Core.Commands) — static
///   - Obtain(RelicModel, Player, ...) — obtain relic
/// - RunManager (MegaCrit.Sts2.Core.Runs) — Singleton: RunManager.Instance
///   - Launch() — starts the run, returns RunState
///   - OnEnded(bool isVictory) — run ended
/// </summary>
public static class DeckPatch
{
    /// <summary>
    /// Postfix: Card added to deck.
    /// TARGET: CardPileCmd.Add(CardModel, PileType, ...) — static method
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Commands.CardPileCmd", "Add")]
    [HarmonyPostfix]
    public static void OnCardAdded(object[] __args)
    {
        try
        {
            // First arg is CardModel
            if (__args.Length == 0 || __args[0] == null) return;
            var card = __args[0];

            // Second arg is PileType — only track additions to the Deck pile
            if (__args.Length > 1)
            {
                var pileType = __args[1]?.ToString();
                if (pileType != null && pileType != "Deck") return;
            }

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
    /// TARGET: CardPileCmd.RemoveFromDeck(CardModel, ...) — static method
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Commands.CardPileCmd", "RemoveFromDeck")]
    [HarmonyPostfix]
    public static void OnCardRemoved(object[] __args)
    {
        try
        {
            // First arg is CardModel
            if (__args.Length == 0 || __args[0] == null) return;
            var card = __args[0];
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
    /// TARGET: RelicCmd.Obtain(RelicModel, Player, ...) — static method
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Commands.RelicCmd", "Obtain")]
    [HarmonyPostfix]
    public static void OnRelicObtained(object[] __args)
    {
        try
        {
            // First arg is RelicModel
            if (__args.Length == 0 || __args[0] == null) return;
            var relic = __args[0];
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
    /// TARGET: RunManager.Launch() — returns RunState
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Runs.RunManager", "Launch")]
    [HarmonyPostfix]
    public static void OnRunStart(object __instance, object __result)
    {
        try
        {
            // __result is RunState from Launch()
            // __instance is RunManager.Instance
            var traverse = Traverse.Create(__instance);

            // RunState has Players list — extract character from first player
            var character = "unknown";
            var ascension = 0;
            var seed = "";

            if (__result != null)
            {
                var rsTraverse = Traverse.Create(__result);

                // Get seed from RunState
                seed = rsTraverse.Property("Seed")?.GetValue<string>()
                    ?? rsTraverse.Field("_seed")?.GetValue<string>()
                    ?? "";

                // Get players from RunState
                var players = rsTraverse.Property("Players")?.GetValue<object>();
                if (players is System.Collections.IEnumerable playerEnum)
                {
                    foreach (var player in playerEnum)
                    {
                        var playerTraverse = Traverse.Create(player);
                        var rawChar = playerTraverse.Property("CharacterId")?.GetValue<string>()
                            ?? playerTraverse.Field("_characterId")?.GetValue<string>()
                            ?? playerTraverse.Property("Name")?.GetValue<string>();
                        character = CharacterValidator.Validate(rawChar);
                        break;
                    }
                }
            }

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
    /// TARGET: RunManager.OnEnded(bool isVictory)
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Runs.RunManager", "OnEnded")]
    [HarmonyPostfix]
    public static void OnRunEnd(object __instance, bool isVictory)
    {
        try
        {
            var floor = Plugin.StateTracker?.GetCurrentState().Floor ?? 0;

            // Try to get score from RunManager
            var traverse = Traverse.Create(__instance);
            var score = traverse.Property("Score")?.GetValue<int>()
                ?? traverse.Field("_score")?.GetValue<int>()
                ?? 0;

            Plugin.StateTracker?.SetScreen(isVictory ? ScreenType.Victory : ScreenType.GameOver);

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "run_end",
                Data = new { won = isVictory, floor, score },
            });

            GD.Print($"[SpireSense] Run ended: {(isVictory ? "Victory" : "Defeat")} at floor {floor}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] RunEnd error: {ex.Message}");
        }
    }
}
