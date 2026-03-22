using HarmonyLib;
using Godot;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for map/floor navigation.
/// Tracks when the player moves to a new floor or selects a path.
///
/// NOTE: Target classes are placeholders — uncomment [HarmonyPatch] when
/// the actual STS2 MapManager class name is confirmed.
/// </summary>
public static class MapPatch
{
    /// <summary>
    /// Postfix: Player moves to a new floor.
    /// Extracts the current node info, updates floor/screen,
    /// and rebuilds the full map snapshot for the overlay.
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

            // Extract the full map from the manager instance.
            // __instance is expected to be the MapManager (or equivalent).
            // Field names are placeholders — adjust once STS2 internals are confirmed.
            var mapTraverse = Traverse.Create(__instance);
            var mapData = mapTraverse.Field("mapData")?.GetValue<object>()
                ?? mapTraverse.Field("map")?.GetValue<object>()
                ?? mapTraverse.Field("currentMap")?.GetValue<object>();
            var mapNodes = GameStateApi.ExtractMapNodes(mapData);

            // Mark the current node as visited in the extracted snapshot
            foreach (var mn in mapNodes)
            {
                if (mn.X == (traverse.Field("x")?.GetValue<int>() ?? -1) && mn.Y == floor)
                {
                    mn.Visited = true;
                }
            }

            Plugin.StateTracker?.UpdateState(state =>
            {
                state.Floor = floor;
                state.Screen = ScreenType.Map;
                state.Map = mapNodes;
            });

            var currentNode = new MapNode
            {
                X = traverse.Field("x")?.GetValue<int>() ?? 0,
                Y = floor,
                Type = nodeType.ToLowerInvariant(),
                Visited = true,
            };

            Plugin.StateTracker?.EmitEvent(new GameEvent
            {
                Type = "floor_changed",
                Data = new { floor, node = currentNode },
            });

            GD.Print($"[SpireSense] Floor changed: {floor} ({nodeType}), map nodes: {mapNodes.Count}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[SpireSense] MapPatch error: {ex.Message}");
        }
    }
}
