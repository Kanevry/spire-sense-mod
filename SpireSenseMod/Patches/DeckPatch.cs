using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for deck/relic state tracking.
/// Updates the state tracker when cards are added/removed and relics obtained.
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
/// </summary>
public static class DeckPatch
{
    /// <summary>
    /// Postfix: Card added to deck.
    /// TARGET: CardPileCmd.Add(CardModel, PileType, ...) — static method
    /// Uses manual resolution because CardPileCmd.Add has multiple overloads.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnCardAdded
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Commands.CardPileCmd");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                .Where(m => m.Name == "Add" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // First arg is CardModel
                if (__args.Length == 0 || __args[0] == null) return;
                var card = __args[0];

                // Second arg is PileType enum or CardPile object — only track additions to the Deck pile
                // PileType enum: MegaCrit.Sts2.Core.Entities.Cards.PileType { None, Draw, Hand, Discard, Exhaust, Play, Deck }
                if (__args.Length > 1 && __args[1] != null)
                {
                    var arg = __args[1];
                    if (arg.GetType().IsEnum)
                    {
                        // Direct PileType enum — ToString() returns member name e.g. "Deck"
                        if (arg.ToString() != "Deck") return;
                    }
                    else
                    {
                        // CardPile object — check its Type property (which is a PileType enum)
                        var pileType = GameStateApi.GetProp(arg, "Type")?.ToString();
                        if (pileType != null && pileType != "Deck") return;
                    }
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
    }

    /// <summary>
    /// Postfix: Card removed from deck (shop, event, etc.).
    /// TARGET: CardPileCmd.RemoveFromDeck(CardModel, ...) — static method
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnCardRemoved
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Commands.CardPileCmd");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                .Where(m => m.Name == "RemoveFromDeck" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
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
    }

    /// <summary>
    /// Postfix: Relic obtained.
    /// TARGET: RelicCmd.Obtain(RelicModel, Player, ...) — static method
    /// Uses manual resolution because RelicCmd.Obtain may have generic overloads.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnRelicObtained
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Commands.RelicCmd");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                .Where(m => m.Name == "Obtain" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
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
    }

    /// <summary>
    /// Postfix: Run starts — initialize state.
    /// TARGET: RunManager.Launch() — returns RunState
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnRunStart
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "Launch" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance, object __result)
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

                var startingDeck = new List<CardInfo>();
                var startingRelics = new List<RelicInfo>();

                if (__result != null)
                {
                    GameStateApi.DumpObjectOnce(__result, "RunState");

                    // Get seed from RunState — use direct reflection (Traverse fails for non-string types)
                    var seedObj = GameStateApi.GetProp(__result, "Seed")
                        ?? GameStateApi.GetField(__result, "_seed");
                    seed = seedObj?.ToString() ?? "";

                    // Get ascension from RunState
                    ascension = (int?)GameStateApi.GetProp(__result, "AscensionLevel")
                        ?? Traverse.Create(__result).Field("_ascensionLevel")?.GetValue<int>()
                        ?? 0;

                    // Get players from RunState — use GetCollection for IReadOnlyList
                    var playerColl = GameStateApi.GetCollection(__result, "Players")
                        ?? GameStateApi.GetField(__result, "_players") as System.Collections.IEnumerable;
                    if (playerColl is System.Collections.IEnumerable playerEnum)
                    {
                        foreach (var player in playerEnum)
                        {
                            GameStateApi.DumpObjectOnce(player, "RunState.Player");

                            // Extract character
                            var charStr = GameStateApi.GetProp(player, "Character")?.ToString() ?? "";
                            if (charStr.Contains("IRONCLAD")) character = "ironclad";
                            else if (charStr.Contains("SILENT")) character = "silent";
                            else if (charStr.Contains("DEFECT")) character = "defect";
                            else if (charStr.Contains("REGENT")) character = "regent";
                            else if (charStr.Contains("NECROBINDER")) character = "necrobinder";
                            else if (charStr.Contains("DEPRIVED")) character = "deprived";

                            // Extract starting deck from Player.Deck (CardPile)
                            var deckPile = GameStateApi.GetProp(player, "Deck");
                            if (deckPile != null)
                            {
                                GameStateApi.DumpObjectOnce(deckPile, "Player.Deck");
                                // CardPile may be IEnumerable directly, or have a Cards property
                                var deckCards = GameStateApi.GetCollection(deckPile, "Cards")
                                    ?? deckPile as System.Collections.IEnumerable;
                                if (deckCards != null)
                                    startingDeck = GameStateApi.ExtractCardsFromEnum(deckCards);
                            }

                            // Fallback: try Player.Piles, filter for PileType.Deck
                            if (startingDeck.Count == 0)
                            {
                                var piles = GameStateApi.GetCollection(player, "Piles")
                                    ?? GameStateApi.GetField(player, "_runPiles") as System.Collections.IEnumerable;
                                if (piles != null)
                                {
                                    foreach (var pile in piles)
                                    {
                                        // CardPile.Type is PileType enum — ToString() returns member name
                                        // Use Contains() for resilience against qualified enum names
                                        var pileTypeStr = GameStateApi.GetProp(pile, "Type")?.ToString() ?? "";
                                        if (pileTypeStr == "Deck" || pileTypeStr.EndsWith(".Deck"))
                                        {
                                            var cards = GameStateApi.GetCollection(pile, "Cards")
                                                ?? pile as System.Collections.IEnumerable;
                                            if (cards != null)
                                                startingDeck = GameStateApi.ExtractCardsFromEnum(cards);
                                            break;
                                        }
                                    }
                                }
                            }

                            // Extract starting relics from Player.Relics (IReadOnlyList)
                            var relicColl = GameStateApi.GetCollection(player, "Relics", "_relics");
                            if (relicColl != null)
                            {
                                foreach (var relic in relicColl)
                                {
                                    if (relic != null)
                                        startingRelics.Add(GameStateApi.ExtractRelicInfo(relic));
                                }
                            }

                            break; // First player only
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
                    Deck = startingDeck,
                    Relics = startingRelics,
                });

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "run_start",
                    Data = new { character, ascension, seed },
                });

                GD.Print($"[SpireSense] Run started: {character} A{ascension}, deck={startingDeck.Count}, relics={startingRelics.Count}, seed={seed}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] RunStart error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Postfix: Run ends — record result.
    /// TARGET: RunManager.OnEnded(bool isVictory)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnRunEnd
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "OnEnded" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance, bool isVictory)
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
}
