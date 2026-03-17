using HarmonyLib;
using Godot;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for map/floor navigation.
/// Tracks when the player moves to a new floor or selects a path.
///
/// NOTE: Target classes are placeholders.
/// </summary>
public static class MapPatch
{
    /// <summary>
    /// Postfix: Player moves to a new floor.
    /// </summary>
    // [HarmonyPatch(typeof(MapManager), "TravelToNode")]
    // [HarmonyPostfix]
    public static void OnFloorChanged(object __instance, object node)
    {
        try
        {
            var traverse = Traverse.Create(node);
            var floor = traverse.Field("y")?.GetValue<int>() ?? 0;
            var nodeType = traverse.Field("type")?.GetValue<string>() ?? "monster";

            Plugin.StateTracker?.UpdateState(state =>
            {
                state.Floor = floor;
                state.Screen = "map";
            });

            var mapNode = new MapNode
            {
                X = traverse.Field("x")?.GetValue<int>() ?? 0,
                Y = floor,
                Type = nodeType.ToLowerInvariant(),
                Visited = true,
            };

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "floor_changed",
                Data = new { floor, node = mapNode },
            });

            GD.Print($"[SpireSense] Floor changed: {floor} ({nodeType})");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] MapPatch error: {ex.Message}");
        }
    }
}
