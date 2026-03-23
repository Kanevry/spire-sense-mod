using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for combat state tracking.
///
/// All patches use [HarmonyTargetMethod] for manual method resolution to avoid
/// "Ambiguous match" errors when the game's PatchAll encounters overloaded methods.
/// </summary>
public static class CombatPatch
{
    /// <summary>
    /// Postfix: Combat begins — extract initial monster state.
    /// TARGET: CombatManager.SetUpCombat(CombatState)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnCombatStart
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "SetUpCombat" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);

                // CombatState has Enemies (creatures) and Allies (players)
                var combatStateObj = traverse.Field("_combatState")?.GetValue<object>()
                    ?? traverse.Property("CombatState")?.GetValue<object>();

                var monsters = new List<MonsterInfo>();
                if (combatStateObj != null)
                {
                    var csTraverse = Traverse.Create(combatStateObj);
                    var enemies = csTraverse.Property("Enemies")?.GetValue<object>()
                        ?? csTraverse.Field("_enemies")?.GetValue<object>();
                    if (enemies is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var monster in enumerable)
                        {
                            monsters.Add(GameStateApi.ExtractMonsterInfo(monster));
                        }
                    }
                }

                var combatState = new CombatState
                {
                    Turn = 1,
                    Monsters = monsters,
                };

                // Extract player state from CombatState.Allies or Players
                if (combatStateObj != null)
                {
                    var csTraverse = Traverse.Create(combatStateObj);
                    var players = csTraverse.Property("Allies")?.GetValue<object>()
                        ?? csTraverse.Field("_allies")?.GetValue<object>();
                    if (players is System.Collections.IEnumerable playerEnum)
                    {
                        foreach (var player in playerEnum)
                        {
                            combatState.Player = GameStateApi.ExtractPlayerState(player);
                            break; // Take first player
                        }
                    }
                }

                Plugin.StateTracker?.SetCombatState(combatState);
                Plugin.StateTracker?.SetScreen(ScreenType.Combat);
                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "combat_start",
                    Data = new { monsters },
                });

                GD.Print($"[SpireSense] Combat started: {monsters.Count} monsters");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] CombatStart error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Postfix: New turn starts — update hand and draw pile.
    /// TARGET: CombatManager.SetupPlayerTurn(Player, HookPlayerChoiceContext)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnTurnStart
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "SetupPlayerTurn" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);

                // Get CombatState from CombatManager
                var combatStateObj = traverse.Field("_combatState")?.GetValue<object>()
                    ?? traverse.Property("CombatState")?.GetValue<object>();

                Plugin.StateTracker?.UpdateState(state =>
                {
                    if (state.Combat != null)
                    {
                        state.Combat.Turn++;

                        // Extract card piles from the first player in CombatState
                        if (combatStateObj != null)
                        {
                            var csTraverse = Traverse.Create(combatStateObj);
                            var players = csTraverse.Property("Allies")?.GetValue<object>();
                            if (players is System.Collections.IEnumerable playerEnum)
                            {
                                foreach (var player in playerEnum)
                                {
                                    var playerTraverse = Traverse.Create(player);
                                    // Player has card piles accessed via the Deck property
                                    var deck = playerTraverse.Property("Deck")?.GetValue<object>();
                                    if (deck != null)
                                    {
                                        var deckTraverse = Traverse.Create(deck);
                                        state.Combat.Hand = GameStateApi.ExtractCards(deckTraverse.Property("Hand") ?? deckTraverse.Field("_hand"));
                                        state.Combat.DrawPile = GameStateApi.ExtractCards(deckTraverse.Property("DrawPile") ?? deckTraverse.Field("_drawPile"));
                                        state.Combat.DiscardPile = GameStateApi.ExtractCards(deckTraverse.Property("DiscardPile") ?? deckTraverse.Field("_discardPile"));
                                        state.Combat.ExhaustPile = GameStateApi.ExtractCards(deckTraverse.Property("ExhaustPile") ?? deckTraverse.Field("_exhaustPile"));
                                    }
                                    break; // First player only
                                }
                            }
                        }
                    }
                });
                // UpdateState() already emits state_update with a thread-safe serialized snapshot
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] TurnStart error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Postfix: Card is played — update combat state.
    /// TARGET: PlayCardAction.ExecuteAction() (extends GameAction)
    /// Uses manual resolution because ExecuteAction might have overloads.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnCardPlayed
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.GameActions.PlayCardAction");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "ExecuteAction" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                GameStateApi.DumpObjectOnce(__instance, "PlayCardAction");
                var traverse = Traverse.Create(__instance);

                // Try multiple field/property name patterns for the card model
                var cardModelObj = traverse.Property("CardModel")?.GetValue<object>()
                    ?? traverse.Field("_cardModel")?.GetValue<object>()
                    ?? traverse.Field("cardModel")?.GetValue<object>()
                    ?? traverse.Property("Card")?.GetValue<object>()
                    ?? traverse.Field("_card")?.GetValue<object>();
                var targetObj = traverse.Property("Target")?.GetValue<object>()
                    ?? traverse.Field("_target")?.GetValue<object>();

                if (cardModelObj == null)
                {
                    GD.Print($"[SpireSense] CardPlayed: cardModel is null, trying all fields...");
                    // Brute force: try to find any CardModel-typed field
                    foreach (var field in __instance.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                    {
                        var val = field.GetValue(__instance);
                        if (val != null && val.GetType().Name.Contains("Card") && !val.GetType().Name.Contains("Action"))
                        {
                            GD.Print($"[SpireSense] Found card-like field: {field.Name} ({field.FieldType.Name}) = {val}");
                            cardModelObj = val;
                            break;
                        }
                    }
                }

                var cardInfo = cardModelObj != null
                    ? GameStateApi.ExtractCardInfo(cardModelObj)
                    : new CardInfo();

                var targetName = "";
                if (targetObj != null)
                {
                    var targetTraverse = Traverse.Create(targetObj);
                    targetName = targetTraverse.Property("Name")?.GetValue<string>()
                        ?? targetTraverse.Field("_name")?.GetValue<string>()
                        ?? "";
                }

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "card_played",
                    Data = new { card = cardInfo, target = targetName },
                });
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] CardPlayed error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Postfix: Combat ends — record result and set appropriate screen.
    /// TARGET: CombatManager.EndCombatInternal()
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnCombatEnd
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == "EndCombatInternal" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);

                // CombatManager tracks combat result internally
                var combatStateObj = traverse.Field("_combatState")?.GetValue<object>()
                    ?? traverse.Property("CombatState")?.GetValue<object>();

                var won = false;
                var isBoss = false;

                if (combatStateObj != null)
                {
                    var csTraverse = Traverse.Create(combatStateObj);
                    won = csTraverse.Property("IsVictory")?.GetValue<bool>()
                        ?? csTraverse.Field("_isVictory")?.GetValue<bool>()
                        ?? false;
                }

                // Check if the current room is a boss room
                var currentRoom = traverse.Field("_currentRoom")?.GetValue<object>();
                if (currentRoom != null)
                {
                    var roomType = Traverse.Create(currentRoom).Property("RoomType")?.GetValue<object>()?.ToString();
                    isBoss = roomType == "Boss";
                }

                var floor = Plugin.StateTracker?.GetCurrentState().Floor ?? 0;

                Plugin.StateTracker?.SetCombatState(null);

                // Set screen based on combat outcome
                if (won && isBoss)
                    Plugin.StateTracker?.SetScreen(ScreenType.BossReward);
                else if (!won)
                    Plugin.StateTracker?.SetScreen(ScreenType.GameOver);
                // Normal victory transitions to card_reward via CardRewardPatch

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "combat_end",
                    Data = new { won, isBoss, floor },
                });

                Plugin.Overlay?.HideCardTiers();

                GD.Print($"[SpireSense] Combat ended: {(won ? "Victory" : "Defeat")}{(isBoss ? " (Boss)" : "")}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] CombatEnd error: {ex.Message}");
            }
        }
    }
}
