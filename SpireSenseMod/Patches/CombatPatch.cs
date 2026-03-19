using System.Collections.Generic;
using HarmonyLib;
using Godot;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for combat state tracking.
///
/// Hooks into:
/// - Combat start/end
/// - Turn start (hand drawn)
/// - Card played
/// - Monster intent updates
///
/// NOTE: Target classes are placeholders. Must be verified via decompilation.
/// </summary>
public static class CombatPatch
{
    /// <summary>
    /// Postfix: Combat begins — extract initial monster state.
    /// TARGET: CombatManager.StartCombat or equivalent
    /// </summary>
    // [HarmonyPatch(typeof(CombatManager), "StartCombat")]
    // [HarmonyPostfix]
    public static void OnCombatStart(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);
            var monsters = ExtractMonsters(traverse);

            var combatState = new CombatState
            {
                Turn = 1,
                Monsters = monsters,
            };

            Plugin.StateTracker?.SetCombatState(combatState);
            Plugin.StateTracker?.SetScreen("combat");
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

    /// <summary>
    /// Postfix: New turn starts — update hand and draw pile.
    /// TARGET: TurnManager.StartTurn or equivalent
    /// </summary>
    // [HarmonyPatch(typeof(TurnManager), "StartPlayerTurn")]
    // [HarmonyPostfix]
    public static void OnTurnStart(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);

            Plugin.StateTracker?.UpdateState(state =>
            {
                if (state.Combat != null)
                {
                    state.Combat.Turn++;
                    // Extract card piles — field names are placeholders pending decompilation
                    state.Combat.Hand = GameStateApi.ExtractCards(traverse.Field("hand"));
                    state.Combat.DrawPile = GameStateApi.ExtractCards(traverse.Field("drawPile"));
                    state.Combat.DiscardPile = GameStateApi.ExtractCards(traverse.Field("discardPile"));
                    state.Combat.ExhaustPile = GameStateApi.ExtractCards(traverse.Field("exhaustPile"));
                }
            });

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "state_update",
                Data = Plugin.StateTracker?.GetCurrentState(),
            });
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] TurnStart error: {ex.Message}");
        }
    }

    /// <summary>
    /// Postfix: Card is played — update combat state.
    /// TARGET: CardManager.PlayCard or equivalent
    /// </summary>
    // [HarmonyPatch(typeof(CardManager), "PlayCard")]
    // [HarmonyPostfix]
    public static void OnCardPlayed(object __instance, object card, object target)
    {
        try
        {
            var cardInfo = GameStateApi.ExtractCardInfo(card);
            var targetName = target != null
                ? Traverse.Create(target).Field("name")?.GetValue<string>() ?? ""
                : "";

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

    /// <summary>
    /// Postfix: Combat ends — record result.
    /// TARGET: CombatManager.EndCombat or equivalent
    /// </summary>
    // [HarmonyPatch(typeof(CombatManager), "EndCombat")]
    // [HarmonyPostfix]
    public static void OnCombatEnd(object __instance)
    {
        try
        {
            var traverse = Traverse.Create(__instance);
            var won = traverse.Field("playerWon")?.GetValue<bool>() ?? false;
            var floor = Plugin.StateTracker?.GetCurrentState().Floor ?? 0;

            Plugin.StateTracker?.SetCombatState(null);
            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "combat_end",
                Data = new { won, floor },
            });

            Plugin.Overlay?.HideCardTiers();

            GD.Print($"[SpireSense] Combat ended: {(won ? "Victory" : "Defeat")}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] CombatEnd error: {ex.Message}");
        }
    }

    private static List<MonsterInfo> ExtractMonsters(Traverse traverse)
    {
        var monsters = new List<MonsterInfo>();
        var monsterList = traverse.Field("monsters")?.GetValue<object>();

        if (monsterList is System.Collections.IEnumerable enumerable)
        {
            foreach (var monster in enumerable)
            {
                monsters.Add(GameStateApi.ExtractMonsterInfo(monster));
            }
        }

        return monsters;
    }
}
