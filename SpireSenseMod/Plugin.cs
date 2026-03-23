using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;

namespace SpireSenseMod;

/// <summary>
/// SpireSense mod entry point.
/// Uses [ModuleInitializer] to auto-start servers when the assembly is loaded.
/// Harmony patches are applied by the game's own PatchAll call.
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
    private const int WsPort = 8081;

    /// <summary>
    /// .NET ModuleInitializer — fires automatically when the assembly is loaded.
    /// Starts servers immediately so the game's own PatchAll handles Harmony patches.
    /// This avoids the problem where the game can't find our internal ModInitializerAttribute.
    /// </summary>
    #pragma warning disable CA2255 // ModuleInitializer is intentional — game's mod loader can't see our internal attribute
    [ModuleInitializer]
    public static void AutoInit()
    #pragma warning restore CA2255
    {
        try
        {
            if (_initialized) return;
            GD.Print("[SpireSense] Module loaded, initializing servers...");

            StateTracker = new GameStateTracker();

            Server = new HttpServer(HttpPort, StateTracker);
            Server.Start();
            GD.Print($"[SpireSense] HTTP server started on port {HttpPort}.");

            WsServer = new WebSocketServer(WsPort, StateTracker);
            WsServer.Start();
            GD.Print($"[SpireSense] WebSocket server started on port {WsPort}.");

            Overlay = new OverlayManager();

            _initialized = true;
            GD.Print("[SpireSense] Ready! Connect at http://localhost:8080");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireSense] AutoInit failed: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
        }
    }

    /// <summary>
    /// Legacy entry point kept for compatibility with the game's ModInitializer attribute.
    /// If the game's mod loader DOES find and call this, it's a no-op since AutoInit already ran.
    /// </summary>
    [ModInitializer("Init")]
    public static void Init()
    {
        if (_initialized)
        {
            GD.Print("[SpireSense] Init() called but already initialized via AutoInit, skipping.");
            return;
        }

        // Fallback: if AutoInit somehow didn't run, initialize now
        // (but do NOT call Harmony.PatchAll — the game does that for us)
        try
        {
            GD.Print("[SpireSense] Initializing via Init() fallback...");

            StateTracker = new GameStateTracker();

            Server = new HttpServer(HttpPort, StateTracker);
            Server.Start();
            GD.Print($"[SpireSense] HTTP server started on port {HttpPort}.");

            WsServer = new WebSocketServer(WsPort, StateTracker);
            WsServer.Start();
            GD.Print($"[SpireSense] WebSocket server started on port {WsPort}.");

            Overlay = new OverlayManager();
            GD.Print("[SpireSense] Overlay initialized (will attach to scene tree on first use).");

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
