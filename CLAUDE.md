# SpireSense Mod

> Game state extraction mod for Slay the Spire 2 -- feeds real-time data to the SpireSense web companion app.

## Quick Facts

- **Stack:** Godot 4.5.1, C#/.NET 9, HarmonyLib 2.4.2
- **SDK:** Godot.NET.Sdk 4.5.1, LangVersion latest, Nullable enabled
- **Entry Point:** `[ModuleInitializer]` on `Plugin.AutoInit()`, `[ModInitializer("Init")]` fallback, `Plugin.Unload()` for clean shutdown
- **Read-Only:** Never modifies game state. Observation only.
- **Test Count:** 176 tests (xUnit, no Godot dependency)
- **Source Control:** GitHub (public, MIT), GitLab mirror

## Commands

```bash
dotnet build                    # Build DLL (auto-deploys to mods folder if STS2GamePath set)
dotnet build -c Release         # Release build
dotnet test SpireSenseMod.Tests # xUnit tests (176 tests, no Godot dependency)
```

## Setup

1. Copy `local.props.example` to `local.props`
2. Set `<STS2GamePath>` to your STS2 install directory
3. `dotnet build` -- DLL is auto-copied to `$(STS2GamePath)/mods/SpireSense/`

## Architecture

### Data Flow

```
STS2 Game Process
  -> Hooks (11 subscriptions) + Harmony patches (5 files, 9 patches) observe game state
  -> GameStateTracker (in-process, thread-safe, lock + immutable JSON snapshots)
  -> HTTP Server (localhost:8080) -- polled by web app
  -> WebSocket Server (localhost:8081) -- batched real-time push (50ms / 10 msgs)

SpireSense Web App (spiresense.app/overlay)
  -> WebSocket client connects to ws://localhost:8081
  -> HTTP fallback polls localhost:8080/api/state
  -> Feeds data into game store (React context + reducer)
```

### Port Defaults

- **HTTP:** 8080 (configurable via `SPIRESENSE_HTTP_PORT` env var)
- **WebSocket:** 8081 (configurable via `SPIRESENSE_WS_PORT` env var)

### Mod Lifecycle

1. **Plugin.AutoInit()** -- `[ModuleInitializer]` fires when assembly is loaded. Creates `GameStateTracker`, starts HTTP + WebSocket servers, initializes overlay.
2. **Plugin.Init()** -- `[ModInitializer("Init")]` fallback. No-op if AutoInit already ran. Game's own `PatchAll` handles Harmony patches.
3. **Plugin.Unload()** -- Stops servers, unpatches Harmony (`UnpatchAll`), cleans up overlay, resets state.

### Thread-Safety Model

- **GameStateTracker:** Central state under `lock(_lock)`. All mutations serialize + produce immutable JSON snapshot. Readers get pre-serialized string.
- **Event Buffer:** Separate `_eventLock` protects ring buffer (max 100 events).
- **WebSocket Broadcast:** `ConcurrentQueue<GameEvent>` flushed by 50ms timer or 10-message threshold. `Interlocked` guard prevents concurrent flushes.
- **Rule:** Never hold `_lock` and `_eventLock` simultaneously. Never modify `_currentState` outside the lock.

## Directory Structure

```
SpireSenseMod/
  Plugin.cs              # Entry point: AutoInit() + Init() + Unload()
  GameStateTracker.cs    # Thread-safe central state, event emitter, ring buffer
  Models/
    GameState.cs         # Full snapshot: screen, character, act, floor, deck, relics, combat, map, shop, events
    CardInfo.cs          # Card with id, name, type, rarity, cost, tags, upgraded
    CombatState.cs       # Combat + PlayerState + MonsterInfo + PowerInfo + PotionInfo + MapNode + EventOption + RelicInfo
    ScreenType.cs        # Screen type constants (main_menu, combat, map, shop, rest, etc.)
    CharacterValidator.cs # Character name validation
  Hooks/
    HookSubscriptions.cs # STS2 Hook system subscriptions (STD-003): 11 hooks
    HookEventAdapter.cs  # Testable adapter: translates hook data into GameStateTracker mutations
  Patches/               # Harmony patches (fallback where no Hook exists)
    CardRewardPatch.cs   # Card reward shown/picked
    DeckPatch.cs         # Relic obtained, run start/end
    ShopPatch.cs         # Shop exit + inventory extraction
    EventPatch.cs        # Event encounters, option extraction
    RestPatch.cs         # Rest choice tracking
  Server/
    HttpServer.cs        # HttpListener on localhost:8080, CORS, 5s timeout, /api/version
    WebSocketServer.cs   # HttpListener on localhost:8081, batched broadcast, backpressure
    GameStateApi.cs      # Extracts game objects to models via Harmony Traverse
  Overlay/
    OverlayManager.cs    # Godot CanvasLayer (layer 100), lazy-inits on scene tree
    TierBadge.cs         # Custom Control -- draws colored tier badges
  Data/
    TypeDiscovery.cs     # Runtime assembly scanner for STS2 class verification (debug mode only)

SpireSenseMod.Tests/
  GameStateTrackerTests.cs    # 30+ tests: state CRUD, concurrency, ring buffer, events
  HookEventAdapterTests.cs   # Hook adapter unit tests
  DataExtractionTests.cs     # Data extraction logic tests
  CharacterValidatorTests.cs # Character name validation tests
  TestHelpers.cs             # Shared test utilities and JSON options
```

## API Protocol

### HTTP (localhost:8080)

| Method | Path             | Description                           |
|--------|------------------|---------------------------------------|
| GET    | /api/state       | Full game state snapshot (JSON)       |
| GET    | /api/health      | Health check + version                |
| GET    | /api/version     | Mod version info                      |
| GET    | /api/deck        | Deck contents + count                 |
| GET    | /api/combat      | Combat state (404 if not in combat)   |
| GET    | /api/relics      | Relic collection                      |
| GET    | /api/map         | Map state                             |
| GET    | /api/events?since= | Buffered events since timestamp     |

### WebSocket (localhost:8081)

Broadcast-only. Events: `state_update`, `card_rewards_shown`, `card_picked`, `card_played`, `relic_obtained`, `combat_start`, `combat_end`, `floor_changed`, `run_start`, `run_end`, `heartbeat`

Backpressure: max 10 pending sends per client, 5s send timeout. Batching: 50ms interval or 10-message threshold.

## Standards & Patterns

- **MOD-001 (Traverse via helpers):** All Traverse operations via `GameStateApi.GetProp()`/`GetField()`/`GetCollection()` -- no raw `Traverse.Create()` in Patches/Hooks. Traverse stays only in GameStateApi.cs implementation.
- **MOD-002 (TargetMethod logging):** When `AccessTools.TypeByName()` returns null in `[HarmonyTargetMethod]`, ALWAYS log with `GD.PrintErr("[SpireSense] PatchName: Could not resolve target type TypeName")`. Never silently return null.
- **MOD-003 (Hook-first patches):** New mod event subscriptions use STS2 Hook system (`MegaCrit.Sts2.Core.Hooks.Hook`) via `[HarmonyPostfix]`. Harmony patches only as fallback where no Hook exists.
- **MOD-004 (Observation-only):** All patches are `[HarmonyPostfix]` -- never modify game state, never use `[HarmonyPrefix]` with return value manipulation.
- **MOD-005 (Priority convention):** All patch classes use `[HarmonyPriority(Priority.HigherThanNormal)]` (600) to run after core game logic but before other mods.

## Key Decisions

- **Godot.NET.Sdk** over BepInEx -- STS2 uses Godot engine, not Unity
- **Harmony** for runtime patching -- works with any .NET runtime
- **Localhost-only servers** -- no auth needed, no network exposure
- **System.Text.Json** over Newtonsoft -- built into .NET 9, no extra dependency
- **Broadcast-only WebSocket** -- simpler protocol, web app never sends commands
- **Traverse reflection** -- avoids compile-time dependency on game assemblies
- **Immutable snapshots** -- serialized under lock, broadcast without holding locks
- **Source-file linking** in test project -- avoids Godot SDK dependency in CI

## CI Pipeline

- **MOD-001 (Traverse via helpers):** All Traverse operations via `GameStateApi.GetProp()`/`GetField()`/`GetCollection()` — no raw `Traverse.Create()` in Patches/Hooks. Traverse stays only in GameStateApi.cs implementation.
- **MOD-002 (TargetMethod logging):** When `AccessTools.TypeByName()` returns null in `[HarmonyTargetMethod]`, ALWAYS log with `GD.PrintErr("[SpireSense] PatchName: Could not resolve target type TypeName")`. Never silently return null.
- **MOD-003 (Disposed Guard):** Methods that can be called after `Dispose()` (event handlers, timer callbacks) MUST early-return when `_disposed` is true. Applied in `WebSocketServer.EnqueueEvent()` and `FlushBatchQueue()` to prevent post-shutdown queue operations and timer-fired broadcasts on a disposed listener.
- **MOD-004 (Reflection Cache):** All `PropertyInfo`/`FieldInfo` lookups MUST go through `ReflectionCache` (`ReflectionCache.cs`) — never call `Type.GetProperty()`/`Type.GetField()` directly in hot paths. `ConcurrentDictionary<(Type, string), *Info?>` keyed by (runtime type, member name). `GameStateApi.GetProp()`/`GetField()`/`GetCollection()` delegate to `ReflectionCache`. Cache is critical for per-frame game state extraction where the same types are queried repeatedly.

GitHub Actions (`.github/workflows/build.yml`):
- **test job** (ubuntu): xUnit tests via source-file linking
- **build job** (ubuntu): `dotnet restore` + `dotnet format` + `dotnet build -c Release`
- **release job** (on `v*` tags): Creates `SpireSense-v{version}.zip` with SHA256 checksums

## Known Decompiled Types

Reference for STS2 internals (verified via `ilspycmd`):
- **PileType:** None, Draw, Hand, Discard, Exhaust, Play, Deck
- **CardTag:** None, Strike, Defend, Minion, OstyAttack, Shiv
- **CardKeyword:** None, Exhaust, Ethereal, Innate, Unplayable, Retain, Sly, Eternal
- **CardEnergyCost:** `Canonical` (original), `_base` (current/upgraded)
