using System.Collections.Generic;
using Godot;
using HarmonyLib;

namespace SpireSenseMod.Patches;

/// <summary>
/// Harmony patches for map/floor navigation.
/// Tracks when the player moves to a new floor or selects a path.
///
/// STS2 classes (from sts2.dll decompilation):
/// - RunManager (MegaCrit.Sts2.Core.Runs) — Singleton: RunManager.Instance
///   - EnterMapCoord(MapCoord) — travel to map coordinate
///   - EnterMapCoordInternal(MapCoord, AbstractRoom?, bool) — internal travel
/// - RunState has CurrentActIndex, ActFloor, TotalFloor, CurrentRoom, Map
/// </summary>
[HarmonyPriority(Priority.HigherThanNormal)]
public static class MapPatch
{
    /// <summary>
    /// Postfix: Player moves to a new floor.
    /// Extracts the current node info, updates floor/screen,
    /// and rebuilds the full map snapshot for the overlay.
    ///
    /// TARGET: RunManager.EnterMapCoord(MapCoord)
    /// </summary>
    [HarmonyPatch("MegaCrit.Sts2.Core.Runs.RunManager", "EnterMapCoord")]
    [HarmonyPostfix]
    public static void OnFloorChanged(object __instance, object coord)
    {
        try
        {
            // coord is MapCoord — extract position
            var coordTraverse = Traverse.Create(coord);
            var x = coordTraverse.Field("X")?.GetValue<int>()
                ?? coordTraverse.Property("X")?.GetValue<int>()
                ?? 0;
            var y = coordTraverse.Field("Y")?.GetValue<int>()
                ?? coordTraverse.Property("Y")?.GetValue<int>()
                ?? 0;

            // Get RunState from RunManager to extract floor and room type
            var rmTraverse = Traverse.Create(__instance);
            var runState = rmTraverse.Field("_runState")?.GetValue<object>()
                ?? rmTraverse.Property("RunState")?.GetValue<object>();

            var floor = y;
            var nodeType = "monster";

            if (runState != null)
            {
                var rsTraverse = Traverse.Create(runState);
                floor = rsTraverse.Property("TotalFloor")?.GetValue<int>()
                    ?? rsTraverse.Property("ActFloor")?.GetValue<int>()
                    ?? y;

                // Get current room type
                var currentRoom = rsTraverse.Property("CurrentRoom")?.GetValue<object>();
                if (currentRoom != null)
                {
                    var roomType = Traverse.Create(currentRoom).Property("RoomType")?.GetValue<object>();
                    nodeType = roomType?.ToString()?.ToLowerInvariant() ?? "monster";
                }
            }

            // Extract the full map from RunState
            var mapNodes = new List<MapNode>();
            if (runState != null)
            {
                var rsTraverse = Traverse.Create(runState);
                var mapData = rsTraverse.Property("Map")?.GetValue<object>()
                    ?? rsTraverse.Field("_map")?.GetValue<object>();
                if (mapData != null)
                {
                    mapNodes = GameStateApi.ExtractMapNodes(mapData);
                }
            }

            // Mark the current node as visited in the extracted snapshot
            foreach (var mn in mapNodes)
            {
                if (mn.X == x && mn.Y == y)
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
                X = x,
                Y = y,
                Type = nodeType,
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
