using System;
using HarmonyLib;
using Godot;

namespace SpireSenseMod;

/// <summary>
/// SpireSense mod entry point.
/// Initializes Harmony patches, HTTP/WebSocket server, and overlay system.
/// </summary>
public static class Plugin
{
    public static Harmony? HarmonyInstance { get; private set; }
    public static HttpServer? Server { get; private set; }
    public static WebSocketServer? WsServer { get; private set; }
    public static OverlayManager? Overlay { get; private set; }
    public static GameStateTracker? StateTracker { get; private set; }

    private const string HarmonyId = "com.spiresense.mod";
    private const int HttpPort = 8080;

    [ModInitializer("Init")]
    public static void Init()
    {
        try
        {
            GD.Print("[SpireSense] Initializing...");

            // Initialize game state tracker
            StateTracker = new GameStateTracker();

            // Apply Harmony patches
            HarmonyInstance = new Harmony(HarmonyId);
            HarmonyInstance.PatchAll(typeof(Plugin).Assembly);
            GD.Print("[SpireSense] Harmony patches applied.");

            // Start HTTP API server
            Server = new HttpServer(HttpPort, StateTracker);
            Server.Start();
            GD.Print($"[SpireSense] HTTP server started on port {HttpPort}.");

            // Start WebSocket server
            WsServer = new WebSocketServer(HttpPort, StateTracker);
            WsServer.Start();
            GD.Print("[SpireSense] WebSocket server started.");

            // Initialize overlay
            Overlay = new OverlayManager();
            GD.Print("[SpireSense] Overlay initialized.");

            GD.Print("[SpireSense] Ready! Connect at http://localhost:8080");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireSense] Initialization failed: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
        }
    }
}
