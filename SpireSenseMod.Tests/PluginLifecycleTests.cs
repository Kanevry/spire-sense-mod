using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace SpireSenseMod.Tests;

/// <summary>
/// Tests for Plugin lifecycle behavior:
/// - Double-init protection (_initialized guard)
/// - Unload nullifies all static properties
/// - After Unload, _initialized is false
///
/// Note: Plugin.cs depends on Godot.GD, HttpServer, WebSocketServer, OverlayManager,
/// and HarmonyLib — none of which are available in the test runner (Godot SDK required).
/// We test the lifecycle CONTRACT via a testable mirror class (PluginLifecycleMirror)
/// that replicates Plugin's exact guard/cleanup pattern. Tests that require the actual
/// Godot runtime or network servers are marked as skipped with a reason.
/// </summary>
public class PluginLifecycleTests
{
    // ─── Double-Init Protection ────────────────────────────────────────

    [Fact]
    public void AutoInit_WhenAlreadyInitialized_IsNoOp()
    {
        var mirror = new PluginLifecycleMirror();

        mirror.AutoInit();
        Assert.True(mirror.IsInitialized);
        Assert.Equal(1, mirror.InitCount);

        // Second call should be a no-op
        mirror.AutoInit();
        Assert.True(mirror.IsInitialized);
        Assert.Equal(1, mirror.InitCount);
    }

    [Fact]
    public void Init_WhenAlreadyInitializedViaAutoInit_IsNoOp()
    {
        var mirror = new PluginLifecycleMirror();

        mirror.AutoInit();
        Assert.Equal(1, mirror.InitCount);

        // Init() should detect _initialized is true and skip
        mirror.Init();
        Assert.Equal(1, mirror.InitCount);
    }

    [Fact]
    public void Init_WhenNotInitialized_Initializes()
    {
        var mirror = new PluginLifecycleMirror();

        mirror.Init();

        Assert.True(mirror.IsInitialized);
        Assert.Equal(1, mirror.InitCount);
    }

    [Fact]
    public void Init_CalledTwice_OnlyInitializesOnce()
    {
        var mirror = new PluginLifecycleMirror();

        mirror.Init();
        mirror.Init();

        Assert.True(mirror.IsInitialized);
        Assert.Equal(1, mirror.InitCount);
    }

    [Fact]
    public void AutoInit_CalledTwice_OnlyInitializesOnce()
    {
        var mirror = new PluginLifecycleMirror();

        mirror.AutoInit();
        mirror.AutoInit();

        Assert.True(mirror.IsInitialized);
        Assert.Equal(1, mirror.InitCount);
    }

    // ─── Unload Nullifies Properties ───────────────────────────────────

    [Fact]
    public void Unload_NullifiesAllStaticProperties()
    {
        var mirror = new PluginLifecycleMirror();
        mirror.AutoInit();

        // Verify all properties are set after init
        Assert.NotNull(mirror.Server);
        Assert.NotNull(mirror.WsServer);
        Assert.NotNull(mirror.Overlay);
        Assert.NotNull(mirror.StateTracker);

        mirror.Unload();

        Assert.Null(mirror.Server);
        Assert.Null(mirror.WsServer);
        Assert.Null(mirror.Overlay);
        Assert.Null(mirror.StateTracker);
        Assert.Null(mirror.HarmonyInstance);
    }

    [Fact]
    public void Unload_SetsInitializedToFalse()
    {
        var mirror = new PluginLifecycleMirror();
        mirror.AutoInit();
        Assert.True(mirror.IsInitialized);

        mirror.Unload();

        Assert.False(mirror.IsInitialized);
    }

    [Fact]
    public void Unload_WhenNeverInitialized_DoesNotThrow()
    {
        var mirror = new PluginLifecycleMirror();

        // All properties are null, Unload should handle gracefully (null-conditional ?.)
        var exception = Record.Exception(() => mirror.Unload());

        Assert.Null(exception);
    }

    [Fact]
    public void Unload_CalledTwice_DoesNotThrow()
    {
        var mirror = new PluginLifecycleMirror();
        mirror.AutoInit();

        mirror.Unload();
        var exception = Record.Exception(() => mirror.Unload());

        Assert.Null(exception);
        Assert.False(mirror.IsInitialized);
    }

    // ─── Re-Init After Unload ──────────────────────────────────────────

    [Fact]
    public void Init_AfterUnload_CanReinitialize()
    {
        var mirror = new PluginLifecycleMirror();

        mirror.AutoInit();
        Assert.True(mirror.IsInitialized);

        mirror.Unload();
        Assert.False(mirror.IsInitialized);

        // Should be able to re-initialize after unload
        mirror.Init();
        Assert.True(mirror.IsInitialized);
        Assert.NotNull(mirror.StateTracker);
        Assert.NotNull(mirror.Server);
    }

    [Fact]
    public void AutoInit_AfterUnload_CanReinitialize()
    {
        var mirror = new PluginLifecycleMirror();

        mirror.AutoInit();
        mirror.Unload();

        mirror.AutoInit();

        Assert.True(mirror.IsInitialized);
        Assert.Equal(2, mirror.InitCount);
    }

    // ─── Init Fallback Guard ───────────────────────────────────────────

    [Fact]
    public void Init_WhenStateTrackerAlreadyExists_SkipsCreation()
    {
        var mirror = new PluginLifecycleMirror();

        // Simulate partial initialization: StateTracker exists but _initialized is false
        // (matches Plugin.Init's guard: if (StateTracker != null) return)
        mirror.SimulatePartialInit();

        Assert.NotNull(mirror.StateTracker);
        Assert.False(mirror.IsInitialized);

        mirror.Init();

        // Should have detected StateTracker exists and skipped full init
        Assert.Equal(0, mirror.InitCount);
    }

    // ─── Dispose Pattern ───────────────────────────────────────────────

    [Fact]
    public void Unload_CallsDisposeOnServer()
    {
        var mirror = new PluginLifecycleMirror();
        mirror.AutoInit();

        mirror.Unload();

        Assert.True(mirror.ServerDisposed);
        Assert.True(mirror.WsServerDisposed);
        Assert.True(mirror.OverlayCleaned);
    }

    [Fact]
    public void Unload_HarmonyUnpatchAllCalled_WhenInstanceExists()
    {
        var mirror = new PluginLifecycleMirror();
        mirror.AutoInit();
        mirror.HarmonyInstance = new FakeHarmony();

        mirror.Unload();

        Assert.True(mirror.HarmonyUnpatched);
        Assert.Null(mirror.HarmonyInstance);
    }

    [Fact]
    public void Unload_HarmonyNull_DoesNotThrow()
    {
        var mirror = new PluginLifecycleMirror();
        mirror.AutoInit();
        mirror.HarmonyInstance = null;

        var exception = Record.Exception(() => mirror.Unload());

        Assert.Null(exception);
        Assert.False(mirror.HarmonyUnpatched);
    }

    // ─── Concurrency (volatile _initialized) ───────────────────────────

    [Fact]
    public async Task ConcurrentAutoInit_OnlyInitializesOnce()
    {
        // Tests that volatile _initialized guard prevents double-init
        // under concurrent access (mirrors Plugin's volatile bool)
        var mirror = new PluginLifecycleMirror();
        var tasks = new Task[10];

        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() => mirror.AutoInit());
        }

        await Task.WhenAll(tasks);

        // The volatile guard + lock should ensure only one full init runs
        Assert.True(mirror.IsInitialized);
        Assert.Equal(1, mirror.InitCount);
    }

    // ─── Actual Plugin Tests (require Godot runtime) ───────────────────

    [Fact(Skip = "Requires Godot runtime — Plugin.cs uses GD.Print and Godot-dependent types (HttpServer, WebSocketServer, OverlayManager)")]
    public void ActualPlugin_AutoInit_SetsInitializedFlag()
    {
        // Would test: Plugin.AutoInit() sets _initialized to true
        // via reflection on the actual Plugin static class
    }

    [Fact(Skip = "Requires Godot runtime — HttpServer/WebSocketServer bind to network ports")]
    public void ActualPlugin_AutoInit_CreatesServers()
    {
        // Would test: Plugin.Server, Plugin.WsServer, Plugin.StateTracker are non-null
    }

    [Fact(Skip = "Requires Godot runtime — Plugin.Unload() calls Server.Dispose() which stops HttpListener")]
    public void ActualPlugin_Unload_DisposesServersAndNullifiesProperties()
    {
        // Would test: After Unload(), all public properties are null
        // and _initialized is false via reflection
    }

    [Fact(Skip = "Requires Godot runtime — Harmony.UnpatchAll requires HarmonyLib runtime")]
    public void ActualPlugin_Unload_UnpatchesHarmony()
    {
        // Would test: HarmonyInstance.UnpatchAll(HarmonyId) is called during Unload
    }

    // ─── Mirror Class ──────────────────────────────────────────────────

    /// <summary>
    /// Test double that mirrors Plugin.cs lifecycle pattern exactly:
    /// - volatile _initialized guard
    /// - AutoInit() / Init() double-init protection
    /// - Init() fallback with StateTracker null check
    /// - Unload() disposes, nullifies, resets _initialized
    ///
    /// Uses fake IDisposable objects instead of Godot types.
    /// </summary>
    private class PluginLifecycleMirror
    {
        public FakeHarmony? HarmonyInstance { get; set; }
        public FakeDisposable? Server { get; private set; }
        public FakeDisposable? WsServer { get; private set; }
        public FakeCleanable? Overlay { get; private set; }
        public object? StateTracker { get; private set; }

        public bool DebugMode { get; set; }

        private volatile bool _initialized;
        private readonly object _initLock = new();
        private int _initCount;

        public bool IsInitialized => _initialized;
        public int InitCount => _initCount;

        public bool ServerDisposed { get; private set; }
        public bool WsServerDisposed { get; private set; }
        public bool OverlayCleaned { get; private set; }
        public bool HarmonyUnpatched { get; private set; }

        /// <summary>
        /// Mirrors Plugin.AutoInit() — [ModuleInitializer] entry point.
        /// </summary>
        public void AutoInit()
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;

                StateTracker = new object();
                Server = new FakeDisposable();
                WsServer = new FakeDisposable();
                Overlay = new FakeCleanable();

                _initialized = true;
                _initCount++;
            }
        }

        /// <summary>
        /// Mirrors Plugin.Init() — legacy entry point with StateTracker guard.
        /// </summary>
        public void Init()
        {
            if (_initialized) return;

            // Guard: AutoInit may have partially initialized before failing
            if (StateTracker != null) return;

            lock (_initLock)
            {
                if (_initialized) return;

                StateTracker = new object();
                Server = new FakeDisposable();
                WsServer = new FakeDisposable();
                Overlay = new FakeCleanable();

                _initialized = true;
                _initCount++;
            }
        }

        /// <summary>
        /// Mirrors Plugin.Unload() — dispose, nullify, reset.
        /// </summary>
        public void Unload()
        {
            if (Server != null)
            {
                Server.Dispose();
                ServerDisposed = true;
            }
            Server = null;

            if (WsServer != null)
            {
                WsServer.Dispose();
                WsServerDisposed = true;
            }
            WsServer = null;

            if (HarmonyInstance != null)
            {
                HarmonyInstance.UnpatchAll("com.spiresense.mod");
                HarmonyUnpatched = true;
            }
            HarmonyInstance = null;

            if (Overlay != null)
            {
                Overlay.Cleanup();
                OverlayCleaned = true;
            }
            Overlay = null;
            StateTracker = null;
            _initialized = false;
        }

        /// <summary>
        /// Simulates a partial init failure: StateTracker exists but _initialized is still false.
        /// This tests the Init() fallback guard path.
        /// </summary>
        public void SimulatePartialInit()
        {
            StateTracker = new object();
            // _initialized remains false — simulates AutoInit crash mid-way
        }
    }

    /// <summary>
    /// Fake IDisposable standing in for HttpServer / WebSocketServer.
    /// </summary>
    private class FakeDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    /// <summary>
    /// Fake cleanable standing in for OverlayManager (which has Cleanup(), not Dispose()).
    /// </summary>
    private class FakeCleanable
    {
        public bool IsCleaned { get; private set; }
        public void Cleanup() => IsCleaned = true;
    }

    /// <summary>
    /// Fake Harmony instance standing in for HarmonyLib.Harmony.
    /// </summary>
    internal class FakeHarmony
    {
        public bool UnpatchAllCalled { get; private set; }
        public string? UnpatchId { get; private set; }

        public void UnpatchAll(string harmonyId)
        {
            UnpatchAllCalled = true;
            UnpatchId = harmonyId;
        }
    }
}
