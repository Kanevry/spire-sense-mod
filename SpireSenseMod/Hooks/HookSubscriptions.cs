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
                            state.Combat.Player.Hp = (int?)GameStateApi.GetProp(ally, "CurrentHp") ?? 0;
                            state.Combat.Player.MaxHp = (int?)GameStateApi.GetProp(ally, "MaxHp") ?? 0;
                            state.Combat.Player.Block = (int?)GameStateApi.GetProp(ally, "Block") ?? 0;
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
                        var gold = (int?)GameStateApi.GetProp(playerObj, "Gold") ?? state.Combat.Player.Gold;
                        state.Combat.Player.Gold = gold;
                        state.Gold = gold;
                        state.Combat.Player.MaxEnergy = (int?)GameStateApi.GetProp(playerObj, "MaxEnergy") ?? 3;
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
                                var gold = (int?)GameStateApi.GetProp(player, "Gold") ?? 0;
                                state.Combat.Player.Gold = gold;
                                state.Gold = gold;
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
                GD.PrintErr($"[SpireSense] AfterPlayerTurnStart error: {ex.Message}\n{ex.StackTrace}");
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
                            combatState.Player.Hp = (int?)GameStateApi.GetProp(ally, "CurrentHp") ?? 0;
                            combatState.Player.MaxHp = (int?)GameStateApi.GetProp(ally, "MaxHp") ?? 0;
                            combatState.Player.Block = (int?)GameStateApi.GetProp(ally, "Block") ?? 0;
                            break;
                        }
                    }
                }

                // Extract Gold/MaxEnergy/Orbs from RunState.Players
                if (runStateObj != null)
                {
                    var rsPlayers = GameStateApi.GetCollection(runStateObj, "Players")
                        ?? GameStateApi.GetField(runStateObj, "_players") as System.Collections.IEnumerable;
                    if (rsPlayers != null)
                    {
                        foreach (var player in rsPlayers)
                        {
                            combatState.Player.Gold = (int?)GameStateApi.GetProp(player, "Gold") ?? 0;
                            combatState.Player.MaxEnergy = (int?)GameStateApi.GetProp(player, "MaxEnergy") ?? 3;
                            combatState.Player.Energy = combatState.Player.MaxEnergy;

                            // Extract orb slots (Defect) from Player.PlayerCombatState.OrbQueue
                            combatState.Player.Orbs = GameStateApi.ExtractOrbs(player);
                            combatState.Player.MaxOrbs = GameStateApi.ExtractMaxOrbs(player);
                            combatState.Player.Focus = combatState.Player.Powers
                                .FirstOrDefault(p => p.Id.Equals("Focus", System.StringComparison.OrdinalIgnoreCase))?.Amount ?? 0;

                            break;
                        }
                    }
                }

                // Cache CombatState for RefreshCombatState + extract card piles
                GameStateApi.CurrentCombatState = combatStateObj;
                if (combatStateObj != null)
                    GameStateApi.ExtractCardPilesFromCombat(combatStateObj, combatState);

                Plugin.StateTracker?.UpdateState(state =>
                {
                    state.Combat = combatState;
                    state.Screen = ScreenType.Combat;
                    state.Gold = combatState.Player.Gold;
                });
                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "combat_start",
                    Data = new { monsters },
                });

                GD.Print($"[SpireSense] Combat started: {monsters.Count} monsters");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] BeforeCombatStart error: {ex.Message}\n{ex.StackTrace}");
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
                    won = (bool?)GameStateApi.GetProp(combatStateObj, "IsVictory")
                        ?? (bool?)GameStateApi.GetField(combatStateObj, "_isVictory")
                        ?? false;
                }

                if (combatRoomObj != null)
                {
                    var roomType = GameStateApi.GetProp(combatRoomObj, "RoomType")?.ToString();
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
                GD.PrintErr($"[SpireSense] AfterCombatEnd error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix: Map is generated — extract map nodes.
    /// TARGET: Hook.AfterMapGenerated(IRunState, ActMap, int actIndex)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterMapGenerated
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterMapGenerated" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.AfterMapGenerated(IRunState, ActMap, int actIndex)
                var actMapObj = __args?.Length > 1 ? __args[1] : null;
                if (actMapObj != null)
                {
                    var mapNodes = GameStateApi.ExtractMapNodes(actMapObj);
                    Plugin.StateTracker?.UpdateState(state =>
                    {
                        state.Map = mapNodes;
                    });
                    GD.Print($"[SpireSense] Map generated: {mapNodes.Count} nodes");
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] AfterMapGenerated error: {ex.Message}\n{ex.StackTrace}");
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
                        targetName = GameStateApi.GetProp(targetObj, "Name")?.ToString() ?? "";
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
                GD.PrintErr($"[SpireSense] AfterCardPlayed error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix: Attack resolves — refresh monster HP/Block after damage is applied.
    /// TARGET: Hook.AfterAttack(CombatState, AttackCommand)
    ///
    /// AfterCardPlayed fires before damage is applied (async game actions).
    /// AfterAttack fires AFTER the attack command completes, so HP is accurate.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterAttack
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterAttack" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.AfterAttack(CombatState, AttackCommand)
                var combatStateObj = __args?.Length > 0 ? __args[0] : null;
                if (combatStateObj != null)
                    GameStateApi.CurrentCombatState = combatStateObj;

                // Refresh full combat state — HP is now accurate post-damage
                GameStateApi.RefreshCombatState();
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] AfterAttack error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix: Potion used — emit event, update potion inventory.
    /// TARGET: Hook.AfterPotionUsed(IRunState, CombatState?, PotionModel, Creature?)
    /// Replaces PotionPatch.OnPotionUsed (Harmony → Hook migration).
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterPotionUsed
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterPotionUsed" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.AfterPotionUsed(IRunState, CombatState?, PotionModel, Creature?)
                var potionObj = __args?.Length > 2 ? __args[2] : null;
                var targetObj = __args?.Length > 3 ? __args[3] : null;

                var potionInfo = new PotionInfo();
                if (potionObj != null)
                {
                    potionInfo = new PotionInfo
                    {
                        Id = GameStateApi.GetProp(potionObj, "PotionId")?.ToString() ?? "",
                        Name = GameStateApi.ResolveLocString(GameStateApi.GetProp(potionObj, "Name")),
                        Description = GameStateApi.ResolveLocString(GameStateApi.GetProp(potionObj, "Description")),
                        CanUse = false,
                    };
                }

                var targetName = "";
                if (targetObj != null)
                {
                    targetName = GameStateApi.GetProp(targetObj, "Name")?.ToString() ?? "";
                }

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "potion_used",
                    Data = new { potion = potionInfo, target = targetName },
                });

                Plugin.StateTracker?.UpdateState(state =>
                {
                    if (state.Combat?.Player.Potions != null)
                    {
                        state.Combat.Player.Potions.RemoveAll(p => p.Id == potionInfo.Id);
                    }
                });

                GD.Print($"[SpireSense] Potion used: {potionInfo.Name}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] AfterPotionUsed error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix: Potion obtained — update inventory, emit event.
    /// TARGET: Hook.AfterPotionProcured(IRunState, CombatState?, PotionModel)
    /// Replaces PotionPatch.OnPotionObtained (Harmony → Hook migration).
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterPotionProcured
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterPotionProcured" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.AfterPotionProcured(IRunState, CombatState?, PotionModel)
                var potionObj = __args?.Length > 2 ? __args[2] : null;

                var potionInfo = new PotionInfo();
                if (potionObj != null)
                {
                    potionInfo = new PotionInfo
                    {
                        Id = GameStateApi.GetProp(potionObj, "PotionId")?.ToString() ?? "",
                        Name = GameStateApi.ResolveLocString(GameStateApi.GetProp(potionObj, "Name")),
                        Description = GameStateApi.ResolveLocString(GameStateApi.GetProp(potionObj, "Description")),
                        CanUse = true,
                    };
                }

                Plugin.StateTracker?.UpdateState(state =>
                {
                    if (state.Combat?.Player.Potions != null)
                    {
                        state.Combat.Player.Potions.Add(potionInfo);
                    }
                });

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "potion_obtained",
                    Data = new { potion = potionInfo },
                });

                GD.Print($"[SpireSense] Potion obtained: {potionInfo.Name}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] AfterPotionProcured error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix: Room entered — update floor, screen, map + detect shop/rest rooms.
    /// TARGET: Hook.AfterRoomEntered(IRunState, AbstractRoom)
    /// Replaces MapPatch.OnFloorChanged (Harmony → Hook migration).
    /// Also replaces ShopPatch.OnShopEntered and RestPatch.OnRestEntered for room-type detection.
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterRoomEntered
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterRoomEntered" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.AfterRoomEntered(IRunState, AbstractRoom)
                var runStateObj = __args?.Length > 0 ? __args[0] : null;
                var roomObj = __args?.Length > 1 ? __args[1] : null;

                var nodeType = "monster";
                if (roomObj != null)
                {
                    var roomType = GameStateApi.GetProp(roomObj, "RoomType");
                    nodeType = roomType?.ToString()?.ToLowerInvariant() ?? "monster";
                }

                var floor = 0;
                var mapNodes = new List<MapNode>();

                if (runStateObj != null)
                {
                    floor = (int?)GameStateApi.GetProp(runStateObj, "ActFloor")
                        ?? (int?)GameStateApi.GetProp(runStateObj, "TotalFloor")
                        ?? 0;

                    var mapData = GameStateApi.GetProp(runStateObj, "Map")
                        ?? GameStateApi.GetField(runStateObj, "_map");
                    if (mapData != null)
                    {
                        mapNodes = GameStateApi.ExtractMapNodes(mapData);
                    }
                }

                Plugin.StateTracker?.UpdateState(state =>
                {
                    state.Floor = floor;
                    state.Screen = ScreenType.Map;
                    state.Map = mapNodes;
                });

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "floor_changed",
                    Data = new { floor, node = new MapNode { Type = nodeType, Visited = true } },
                });

                GD.Print($"[SpireSense] Room entered: floor {floor} ({nodeType}), map: {mapNodes.Count} nodes");

                // ── Shop detection: extract inventory if MerchantRoom ───────
                if (roomObj != null && roomObj.GetType().Name.Contains("Merchant"))
                {
                    try
                    {
                        var inventory = GameStateApi.GetField(roomObj, "_inventory")
                            ?? GameStateApi.GetProp(roomObj, "Inventory");

                        var shopCards = new List<CardInfo>();
                        var shopRelics = new List<RelicInfo>();

                        if (inventory != null)
                        {
                            // Extract card entries (MerchantCardEntry)
                            var cardEntries = GameStateApi.GetCollection(inventory, "CardEntries", "_cardEntries");
                            if (cardEntries != null)
                            {
                                foreach (var entry in cardEntries)
                                {
                                    var cardModel = GameStateApi.GetProp(entry, "CardModel")
                                        ?? GameStateApi.GetField(entry, "_cardModel");
                                    if (cardModel != null)
                                        shopCards.Add(GameStateApi.ExtractCardInfo(cardModel));
                                }
                            }

                            // Extract relic entries (MerchantRelicEntry)
                            var relicEntries = GameStateApi.GetCollection(inventory, "RelicEntries", "_relicEntries");
                            if (relicEntries != null)
                            {
                                foreach (var entry in relicEntries)
                                {
                                    var relicModel = GameStateApi.GetProp(entry, "RelicModel")
                                        ?? GameStateApi.GetField(entry, "_relicModel");
                                    if (relicModel != null)
                                        shopRelics.Add(GameStateApi.ExtractRelicInfo(relicModel));
                                }
                            }
                        }

                        Plugin.StateTracker?.UpdateState(state =>
                        {
                            state.Screen = ScreenType.Shop;
                            state.ShopCards = shopCards;
                            state.ShopRelics = shopRelics;
                        });

                        Plugin.StateTracker?.EmitEvent(new GameEvent
                        {
                            Type = "floor_changed",
                            Data = new { screen = ScreenType.Shop, shopCards = shopCards.Count, shopRelics = shopRelics.Count },
                        });

                        GD.Print($"[SpireSense] Shop detected via Hook: {shopCards.Count} cards, {shopRelics.Count} relics");
                    }
                    catch (System.Exception ex)
                    {
                        GD.PrintErr($"[SpireSense] AfterRoomEntered shop extraction error: {ex.Message}\n{ex.StackTrace}");
                    }
                }

                // ── Rest site detection: extract options if RestSiteRoom ────
                if (roomObj != null && roomObj.GetType().Name.Contains("RestSite"))
                {
                    try
                    {
                        var restOptions = new List<RestOption>();
                        var options = GameStateApi.GetCollection(roomObj, "Options", "_options");

                        if (options != null)
                        {
                            foreach (var option in options)
                            {
                                restOptions.Add(new RestOption
                                {
                                    Id = (GameStateApi.GetProp(option, "OptionId")
                                        ?? GameStateApi.GetField(option, "_optionId"))?.ToString()
                                        ?? option.GetType().Name.Replace("RestSiteOption", "").ToLowerInvariant(),
                                    Name = (GameStateApi.GetProp(option, "Title")
                                        ?? GameStateApi.GetField(option, "_title"))?.ToString()
                                        ?? option.GetType().Name.Replace("RestSiteOption", ""),
                                    Description = (GameStateApi.GetProp(option, "Description")
                                        ?? GameStateApi.GetField(option, "_description"))?.ToString() ?? "",
                                    Enabled = (bool?)GameStateApi.GetProp(option, "IsEnabled")
                                        ?? (bool?)GameStateApi.GetField(option, "_isEnabled")
                                        ?? true,
                                });
                            }
                        }

                        Plugin.StateTracker?.UpdateState(state =>
                        {
                            state.Screen = ScreenType.Rest;
                            state.RestOptions = restOptions;
                        });

                        Plugin.StateTracker?.EmitEvent(new GameEvent
                        {
                            Type = "rest_entered",
                            Data = new { options = restOptions },
                        });

                        GD.Print($"[SpireSense] Rest site detected via Hook: {restOptions.Count} options");
                    }
                    catch (System.Exception ex)
                    {
                        GD.PrintErr($"[SpireSense] AfterRoomEntered rest extraction error: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] AfterRoomEntered error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix: Card changed piles — detect cards added to deck.
    /// TARGET: Hook.AfterCardChangedPiles(IRunState, CombatState?, CardModel, PileType oldPile, AbstractModel? source)
    ///
    /// Replaces DeckPatch.OnCardAdded (Harmony → Hook migration).
    /// Fires whenever a card moves between piles. We check if the card's current pile
    /// is "Deck" to detect additions (reward picks, shop purchases, events).
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterCardChangedPiles
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterCardChangedPiles" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.AfterCardChangedPiles(IRunState, CombatState?, CardModel, PileType oldPile, AbstractModel? source)
                var cardObj = __args?.Length > 2 ? __args[2] : null;
                var oldPileObj = __args?.Length > 3 ? __args[3] : null;

                if (cardObj == null) return;

                // Determine the card's current pile — CardModel has a Pile or PileType property
                var currentPileStr = "";
                var pileObj = GameStateApi.GetProp(cardObj, "Pile");
                if (pileObj != null)
                {
                    // Pile may be a CardPile object with a Type property, or a PileType enum directly
                    var pileType = GameStateApi.GetProp(pileObj, "Type");
                    currentPileStr = (pileType ?? pileObj).ToString() ?? "";
                }
                else
                {
                    // Fallback: try PileType property directly on CardModel
                    var pileType = GameStateApi.GetProp(cardObj, "PileType");
                    currentPileStr = pileType?.ToString() ?? "";
                }

                // Only track cards that moved TO the Deck pile (not combat piles like Hand, Draw, Discard)
                if (!currentPileStr.Contains("Deck")) return;

                // oldPile should NOT be Deck (we only want new additions, not Deck→Deck moves)
                var oldPileStr = oldPileObj?.ToString() ?? "";
                if (oldPileStr.Contains("Deck")) return;

                var cardInfo = GameStateApi.ExtractCardInfo(cardObj);

                Plugin.StateTracker?.UpdateState(state =>
                {
                    state.Deck.Add(cardInfo);
                });

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "deck_changed",
                    Data = new { action = "added", card = cardInfo },
                });

                GD.Print($"[SpireSense] Card added to deck (Hook): {cardInfo.Name} (from {oldPileStr})");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] AfterCardChangedPiles error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix: Card about to be removed — track deck removals.
    /// TARGET: Hook.BeforeCardRemoved(IRunState, CardModel)
    ///
    /// Replaces DeckPatch.OnCardRemoved (Harmony → Hook migration).
    /// Fires before a card is permanently removed from the game (shop removal, events).
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnBeforeCardRemoved
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "BeforeCardRemoved" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Hook.BeforeCardRemoved(IRunState, CardModel)
                var cardObj = __args?.Length > 1 ? __args[1] : null;
                if (cardObj == null) return;

                var cardInfo = GameStateApi.ExtractCardInfo(cardObj);

                Plugin.StateTracker?.UpdateState(state =>
                {
                    var index = state.Deck.FindIndex(c => c.Id == cardInfo.Id);
                    if (index >= 0)
                        state.Deck.RemoveAt(index);
                });

                Plugin.StateTracker?.EmitEvent(new GameEvent
                {
                    Type = "card_removed",
                    Data = new { card = cardInfo },
                });

                GD.Print($"[SpireSense] Card removed from deck (Hook): {cardInfo.Name}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] BeforeCardRemoved error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix: Orb channeled — refresh combat state to update orb slots.
    /// TARGET: Hook.AfterOrbChanneled(CombatState, OrbModel)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterOrbChanneled
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterOrbChanneled" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Cache CombatState if available
                var combatStateObj = __args?.Length > 0 ? __args[0] : null;
                if (combatStateObj != null)
                    GameStateApi.CurrentCombatState = combatStateObj;

                GameStateApi.RefreshCombatState();
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] AfterOrbChanneled error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Postfix: Orb evoked — refresh combat state to update orb slots.
    /// TARGET: Hook.AfterOrbEvoked(CombatState, OrbModel)
    /// </summary>
    [HarmonyPatch]
    [HarmonyPriority(Priority.HigherThanNormal)]
    public static class OnAfterOrbEvoked
    {
        [HarmonyTargetMethod]
        static MethodBase? TargetMethod()
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
            if (type == null) return null;
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "AfterOrbEvoked" && !m.IsGenericMethod)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [HarmonyPostfix]
        static void Postfix(object[] __args)
        {
            try
            {
                // Cache CombatState if available
                var combatStateObj = __args?.Length > 0 ? __args[0] : null;
                if (combatStateObj != null)
                    GameStateApi.CurrentCombatState = combatStateObj;

                GameStateApi.RefreshCombatState();
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[SpireSense] AfterOrbEvoked error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
