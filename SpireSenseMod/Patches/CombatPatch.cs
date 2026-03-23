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
        static void Postfix(object __instance, object[] __args)
        {
            try
            {
                var traverse = Traverse.Create(__instance);

                // SetUpCombat receives CombatState as first parameter
                // Also try reading it from CombatManager fields/properties
                var combatStateObj = (__args?.Length > 0 ? __args[0] : null)
                    ?? traverse.Property("State")?.GetValue<object>()
                    ?? traverse.Property("CombatState")?.GetValue<object>()
                    ?? traverse.Field("_combatState")?.GetValue<object>();

                var monsters = new List<MonsterInfo>();
                if (combatStateObj != null)
                {
                    GameStateApi.DumpObjectOnce(combatStateObj, "CombatState");
                    var enemies = GameStateApi.GetCollection(combatStateObj, "Enemies", "_enemies");
                    if (enemies != null)
                    {
                        foreach (var monster in enemies)
                            monsters.Add(GameStateApi.ExtractMonsterInfo(monster));
                    }
                }

                var combatState = new CombatState
                {
                    Turn = 1,
                    Monsters = monsters,
                };

                // Extract player HP/Block from Allies + Gold/MaxEnergy from RunState
                // Using direct reflection since Traverse.GetValue on IReadOnlyList can return null
                if (combatStateObj != null)
                {
                    // HP/Block from Allies (Creature objects — same type as monsters)
                    var allies = GameStateApi.GetCollection(combatStateObj, "Allies", "_allies");
                    if (allies != null)
                    {
                        foreach (var ally in allies)
                        {
                            var at = Traverse.Create(ally);
                            combatState.Player.Hp = at.Property("CurrentHp")?.GetValue<int>() ?? 0;
                            combatState.Player.MaxHp = at.Property("MaxHp")?.GetValue<int>() ?? 0;
                            combatState.Player.Block = at.Property("Block")?.GetValue<int>() ?? 0;
                            break;
                        }
                    }

                    // Gold/MaxEnergy/Energy + Card Piles from CombatState.RunState.Players
                    var runState = GameStateApi.GetProp(combatStateObj, "RunState");
                    var rsPlayers = GameStateApi.GetCollection(runState, "Players")
                        ?? GameStateApi.GetField(runState, "_players") as System.Collections.IEnumerable;
                    if (rsPlayers != null)
                    {
                        foreach (var player in rsPlayers)
                        {
                            var pt = Traverse.Create(player);
                            combatState.Player.Gold = pt.Property("Gold")?.GetValue<int>() ?? 0;
                            combatState.Player.MaxEnergy = pt.Property("MaxEnergy")?.GetValue<int>() ?? 3;
                            combatState.Player.Energy = combatState.Player.MaxEnergy;

                            // Card piles from Player.Piles (array of CardPile, each with Type + Cards)
                            // Card piles extracted below from CombatState._allCards
                            break;
                        }
                    }
                }

                // Cache CombatState for RefreshCombatState + extract card piles
                GameStateApi.CurrentCombatState = combatStateObj;
                if (combatStateObj != null)
                    GameStateApi.ExtractCardPilesFromCombat(combatStateObj, combatState);

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

                // Get CombatState — try __args first (SetupPlayerTurn may pass it), then fields
                var combatStateObj = GameStateApi.GetProp(__instance, "State")
                    ?? GameStateApi.GetField(__instance, "_combatState")
                    ?? traverse.Property("CombatState")?.GetValue<object>();

                Plugin.StateTracker?.UpdateState(state =>
                {
                    if (state.Combat == null || combatStateObj == null) return;
                    state.Combat.Turn++;

                    // Update player HP/Block from Allies (Creature objects)
                    var allies = GameStateApi.GetCollection(combatStateObj, "Allies", "_allies");
                    if (allies != null)
                    {
                        foreach (var ally in allies)
                        {
                            var at = Traverse.Create(ally);
                            state.Combat.Player.Hp = at.Property("CurrentHp")?.GetValue<int>() ?? 0;
                            state.Combat.Player.MaxHp = at.Property("MaxHp")?.GetValue<int>() ?? 0;
                            state.Combat.Player.Block = at.Property("Block")?.GetValue<int>() ?? 0;
                            break;
                        }
                    }

                    // Update monsters
                    var enemies = GameStateApi.GetCollection(combatStateObj, "Enemies", "_enemies");
                    if (enemies != null)
                    {
                        state.Combat.Monsters.Clear();
                        foreach (var monster in enemies)
                            state.Combat.Monsters.Add(GameStateApi.ExtractMonsterInfo(monster));
                    }

                    // Extract card piles from CombatState._allCards
                    if (combatStateObj != null)
                        GameStateApi.ExtractCardPilesFromCombat(combatStateObj, state.Combat);

                    // Get energy from RunState.Player
                    var runState = GameStateApi.GetProp(combatStateObj, "RunState");
                    var rsPlayers = GameStateApi.GetCollection(runState, "Players")
                        ?? GameStateApi.GetField(runState, "_players") as System.Collections.IEnumerable;
                    if (rsPlayers != null)
                    {
                        foreach (var player in rsPlayers)
                        {
                            var pt = Traverse.Create(player);
                            state.Combat.Player.Gold = pt.Property("Gold")?.GetValue<int>() ?? 0;
                            state.Combat.Player.Energy = state.Combat.Player.MaxEnergy; // Reset each turn
                            break;
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

                // PlayCardAction has CardModelId (ModelId) and Target (Creature)
                var cardModelId = GameStateApi.GetProp(__instance, "CardModelId")?.ToString() ?? "";
                var targetObj = GameStateApi.GetProp(__instance, "Target");
                var targetName = targetObj != null
                    ? Traverse.Create(targetObj).Property("Name")?.GetValue<string>() ?? ""
                    : "";

                // Try to get the actual CardModel from the Player's combat cards
                var playerObj = GameStateApi.GetProp(__instance, "Player");
                CardInfo cardInfo = new CardInfo { Id = cardModelId, Name = cardModelId };

                // Find the card in the player's piles by ModelId
                if (playerObj != null)
                {
                    var piles = GameStateApi.GetCollection(playerObj, "Piles")
                        ?? GameStateApi.GetField(playerObj, "_runPiles") as System.Collections.IEnumerable;
                    if (piles != null)
                    {
                        foreach (var pile in piles)
                        {
                            if (pile == null) continue;
                            var cards = GameStateApi.GetCollection(pile, "Cards", "_cards");
                            if (cards == null) continue;
                            foreach (var card in cards)
                            {
                                var id = GameStateApi.GetProp(card, "Id")?.ToString() ?? "";
                                if (id == cardModelId)
                                {
                                    cardInfo = GameStateApi.ExtractCardInfo(card);
                                    break;
                                }
                            }
                            if (cardInfo.Name != cardModelId) break; // Found it
                        }
                    }
                }

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "card_played",
                    Data = new { card = cardInfo, target = targetName },
                });

                // Refresh combat state after card played (HP, Block, monsters, piles)
                GameStateApi.RefreshCombatState();
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
