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

    /// <summary>Debug mode enables type discovery logging and verbose output.</summary>
    public static bool DebugMode { get; set; }

    private static bool _initialized;
    private const string HarmonyId = "com.spiresense.mod";
    private const int HttpPort = 8080;

    [ModInitializer("Init")]
    public static void Init()
    {
        if (_initialized)
        {
            GD.PrintErr("[SpireSense] Plugin already initialized, skipping");
            return;
        }

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

            // Initialize overlay (lazy — attaches to scene tree when game UI is ready)
            Overlay = new OverlayManager();
            GD.Print("[SpireSense] Overlay initialized (will attach to scene tree on first use).");

            // Run type discovery in debug mode
            if (DebugMode)
            {
                Data.TypeDiscovery.DiscoverAndLog();
            }

            _initialized = true;
            GD.Print("[SpireSense] Ready! Connect at http://localhost:8080");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireSense] Initialization failed: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
        }
    }

    /// <summary>
    /// Clean shutdown: stop servers, unpatch Harmony, release resources.
    /// Called by the mod manager when the mod is unloaded or the game exits.
    /// </summary>
    public static void Unload()
    {
        try
        {
            GD.Print("[SpireSense] Unloading...");

            Server?.Dispose();
            Server = null;

            WsServer?.Dispose();
            WsServer = null;

            HarmonyInstance?.UnpatchAll(HarmonyId);
            HarmonyInstance = null;

            Overlay?.Cleanup();
            Overlay = null;
            StateTracker = null;
            _initialized = false;

            GD.Print("[SpireSense] Unloaded successfully.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireSense] Unload error: {ex.Message}");
        }
    }
}
