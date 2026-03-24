using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Hooks;

/// <summary>
/// Harmony patches targeting the STS2 Hook system (static hook methods).
///
/// These fire AFTER the game's internal processing, providing reliable state
/// (e.g., hand is fully dealt before AfterPlayerTurnStart fires).
///
/// All patches use [HarmonyTargetMethod] for manual method resolution and
/// object parameters to avoid compile-time dependencies on game types.
/// </summary>
public static class HookSubscriptions
{
    /// <summary>
    /// Postfix: Player turn starts — extract card piles, player stats, monsters.
    /// TARGET: Hook.AfterPlayerTurnStart(CombatState, PlayerChoiceContext, Player)
    ///
    /// This fires AFTER cards are dealt to hand, fixing the empty hand/discard bug
    /// that occurred when patching CombatManager.SetupPlayerTurn directly.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterPlayerTurnStart
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterPlayerTurnStart" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.AfterPlayerTurnStart(CombatState, PlayerChoiceContext, Player)
                var combatStateObj = __args?.Length > 0 ? __args[0] : null;
                var playerObj = __args?.Length > 2 ? __args[2] : null;

                if (combatStateObj == null)
                {
                    GD.PrintErr("[SpireSense] AfterPlayerTurnStart: CombatState is null");
                    return;
                }

                // Cache CombatState for RefreshCombatState
                GameStateApi.CurrentCombatState = combatStateObj;

                Plugin.StateTracker?.UpdateState(state =>
                {
                    if (state.Combat == null) return;
                    state.Combat.Turn++;

                    // Update player HP/Block from Allies (Creature objects in CombatState)
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

                    // Update monsters from CombatState.Enemies
                    var enemies = GameStateApi.GetCollection(combatStateObj, "Enemies", "_enemies");
                    if (enemies != null)
                    {
                        state.Combat.Monsters.Clear();
                        foreach (var monster in enemies)
                            state.Combat.Monsters.Add(GameStateApi.ExtractMonsterInfo(monster));
                    }

                    // Extract card piles — hand is fully dealt at this point
                    GameStateApi.ExtractCardPilesFromCombat(combatStateObj, state.Combat);

                    // Get Gold/Energy from Player object (third Hook param)
                    if (playerObj != null)
                    {
                        var pt = Traverse.Create(playerObj);
                        state.Combat.Player.Gold = pt.Property("Gold")?.GetValue<int>() ?? state.Combat.Player.Gold;
                        state.Combat.Player.MaxEnergy = pt.Property("MaxEnergy")?.GetValue<int>() ?? 3;
                        state.Combat.Player.Energy = state.Combat.Player.MaxEnergy; // Reset each turn
                    }
                    else
                    {
                        // Fallback: get from RunState.Players
                        var runState = GameStateApi.GetProp(combatStateObj, "RunState");
                        var rsPlayers = GameStateApi.GetCollection(runState, "Players")
                            ?? GameStateApi.GetField(runState, "_players") as System.Collections.IEnumerable;
                        if (rsPlayers != null)
                        {
                            foreach (var player in rsPlayers)
                            {
                                var pt = Traverse.Create(player);
                                state.Combat.Player.Gold = pt.Property("Gold")?.GetValue<int>() ?? 0;
                                state.Combat.Player.Energy = state.Combat.Player.MaxEnergy;
                                break;
                            }
                        }
                    }
                });
                // UpdateState() already emits state_update with a thread-safe serialized snapshot
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] AfterPlayerTurnStart error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Postfix: Combat begins — extract monsters, player stats, card piles.
    /// TARGET: Hook.BeforeCombatStart(RunState, CombatState?)
    ///
    /// CombatState is passed as the second argument (__args[1]).
    /// RunState.Players provides Gold/MaxEnergy.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnBeforeCombatStart
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "BeforeCombatStart" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.BeforeCombatStart(RunState, CombatState?)
                var runStateObj = __args?.Length > 0 ? __args[0] : null;
                var combatStateObj = __args?.Length > 1 ? __args[1] : null;

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

                // Extract player HP/Block from CombatState.Allies
                if (combatStateObj != null)
                {
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
                }

                // Extract Gold/MaxEnergy from RunState.Players
                if (runStateObj != null)
                {
                    var rsPlayers = GameStateApi.GetCollection(runStateObj, "Players")
                        ?? GameStateApi.GetField(runStateObj, "_players") as System.Collections.IEnumerable;
                    if (rsPlayers != null)
                    {
                        foreach (var player in rsPlayers)
                        {
                            var pt = Traverse.Create(player);
                            combatState.Player.Gold = pt.Property("Gold")?.GetValue<int>() ?? 0;
                            combatState.Player.MaxEnergy = pt.Property("MaxEnergy")?.GetValue<int>() ?? 3;
                            combatState.Player.Energy = combatState.Player.MaxEnergy;
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
                GD.PrintErr($"[SpireSense] BeforeCombatStart error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Postfix: Combat ends — record result, set screen, hide overlay.
    /// TARGET: Hook.AfterCombatEnd(RunState, CombatState?, CombatRoom)
    ///
    /// CombatState is in __args[1] — check IsVictory property.
    /// CombatRoom is in __args[2] — check RoomType for "Boss".
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterCombatEnd
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterCombatEnd" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.AfterCombatEnd(RunState, CombatState?, CombatRoom)
                var combatStateObj = __args?.Length > 1 ? __args[1] : null;
                var combatRoomObj = __args?.Length > 2 ? __args[2] : null;

                var won = false;
                var isBoss = false;

                if (combatStateObj != null)
                {
                    var csTraverse = Traverse.Create(combatStateObj);
                    won = csTraverse.Property("IsVictory")?.GetValue<bool>()
                        ?? csTraverse.Field("_isVictory")?.GetValue<bool>()
                        ?? false;
                }

                if (combatRoomObj != null)
                {
                    var roomType = Traverse.Create(combatRoomObj).Property("RoomType")?.GetValue<object>()?.ToString();
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
                GD.PrintErr($"[SpireSense] AfterCombatEnd error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Postfix: Card is played — extract card info, emit event, refresh state.
    /// TARGET: Hook.AfterCardPlayed(CombatState, PlayerChoiceContext, CardPlay)
    ///
    /// The CardPlay object contains the played card and target information.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterCardPlayed
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterCardPlayed" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.AfterCardPlayed(CombatState, PlayerChoiceContext, CardPlay)
                var combatStateObj = __args?.Length > 0 ? __args[0] : null;
                var cardPlayObj = __args?.Length > 2 ? __args[2] : null;

                // Cache CombatState for RefreshCombatState
                if (combatStateObj != null)
                    GameStateApi.CurrentCombatState = combatStateObj;

                if (cardPlayObj != null)
                    GameStateApi.DumpObjectOnce(cardPlayObj, "CardPlay");

                // Extract card info from the CardPlay object
                CardInfo cardInfo = new CardInfo { Id = "", Name = "unknown" };
                var targetName = "";

                if (cardPlayObj != null)
                {
                    // CardPlay has CardModel or Card property for the played card
                    var cardObj = GameStateApi.GetProp(cardPlayObj, "CardModel")
                        ?? GameStateApi.GetProp(cardPlayObj, "Card");

                    if (cardObj != null)
                    {
                        cardInfo = GameStateApi.ExtractCardInfo(cardObj);
                    }
                    else
                    {
                        // Fallback: try CardModelId
                        var cardModelId = GameStateApi.GetProp(cardPlayObj, "CardModelId")?.ToString() ?? "";
                        cardInfo = new CardInfo { Id = cardModelId, Name = cardModelId };
                    }

                    // Get target name from CardPlay's Target property
                    var targetObj = GameStateApi.GetProp(cardPlayObj, "Target");
                    if (targetObj != null)
                    {
                        targetName = Traverse.Create(targetObj).Property("Name")?.GetValue<string>() ?? "";
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
                GD.PrintErr($"[SpireSense] AfterCardPlayed error: {ex.Message}");
            }
        }
    }
}
