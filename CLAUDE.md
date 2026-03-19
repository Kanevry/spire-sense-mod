# SpireSense Mod

> Game state extraction mod for Slay the Spire 2 -- feeds real-time data to the SpireSense web companion app.

## Quick Facts

- **Game Engine:** Godot 4.5.1 (STS2 runs on Godot, NOT Unity)
- **SDK:** Godot.NET.Sdk 4.5.0, .NET 9.0, C# (latest LangVersion)
- **Patching:** Harmony 2.3.x (runtime method patching via `[HarmonyPatch]`)
- **Entry Point:** `[ModInitializer("Init")]` on `Plugin.Init()`, `Plugin.Unload()` for clean shutdown
- **Read-Only:** Never modifies game state. Observation only.
- **Status:** Early Access -- all Harmony patch targets are commented out (placeholder class/method names need verification via game DLL decompilation)
- **Debug Mode:** `Plugin.DebugMode = true` enables TypeDiscovery (runtime assembly scanning)
- **Source Control:** GitHub (public, MIT), GitLab mirror

## Commands

```bash
dotnet build                    # Build DLL (auto-deploys to mods folder if STS2GamePath set)
dotnet build -c Release         # Release build
```

No test framework is set up yet. No CI pipeline.

## Setup

1. Copy `local.props.example` to `local.props`
2. Set `<STS2GamePath>` to your STS2 install directory
3. `dotnet build` -- DLL is auto-copied to `$(STS2GamePath)/mods/SpireSense/`

## Architecture

```
SpireSenseMod/
  Plugin.cs              # Entry point: Init() + Unload(), inits Harmony, HTTP, WebSocket, Overlay
  GameStateTracker.cs    # Thread-safe central state (lock-based), event emitter
  Models/                # JSON-serializable data models (System.Text.Json)
    GameState.cs         # Full snapshot: screen, character, act, floor, deck, relics, combat, map, shop, events
    CardInfo.cs          # Card with id, name, type, rarity, cost, tags, upgraded
    CombatState.cs       # Combat + PlayerState + MonsterInfo + PowerInfo + PotionInfo + MapNode + EventOption + RelicInfo
  Patches/               # Harmony patches (all targets currently commented out)
    CardRewardPatch.cs   # Card reward shown/picked
    CombatPatch.cs       # Combat start/end, turn start, card played
    MapPatch.cs          # Floor/map navigation
    DeckPatch.cs         # Card added, relic obtained, run start/end
  Server/
    HttpServer.cs        # HttpListener on localhost:8080, CORS, 5s timeout, /api/version
    WebSocketServer.cs   # HttpListener on localhost:8081, broadcast with backpressure (max 10 pending, 5s send timeout)
    GameStateApi.cs      # Extracts game objects to models via Harmony Traverse (ExtractCards, ExtractPowers, ExtractPotions)
  Overlay/
    OverlayManager.cs    # Godot CanvasLayer (layer 100), lazy-inits on scene tree
    TierBadge.cs         # Custom Control -- draws colored S/A/B/C/D/F badges
  Data/
    CardTiers/           # Empty -- planned for static tier data
    TypeDiscovery.cs     # Runtime assembly scanner for STS2 class name verification (debug mode only)
```

## API Protocol

### HTTP (localhost:8080)

| Method | Path          | Description                           |
|--------|---------------|---------------------------------------|
| GET    | /api/state    | Full game state snapshot (JSON)       |
| GET    | /api/health   | Health check + version                |
| GET    | /api/version  | Mod version info                      |
| GET    | /api/deck     | Deck contents + count                 |
| GET    | /api/combat   | Combat state (404 if not in combat)   |

CORS: `Access-Control-Allow-Origin: *` on all responses.

### WebSocket (localhost:8081)

Broadcast-only (server never reads client messages). On connect, sends immediate `state_update`.

Event format: `{ "type": "event_type", "data": { ... } }`

Events: `state_update`, `card_rewards_shown`, `card_picked`, `card_played`, `relic_obtained`, `combat_start`, `combat_end`, `floor_changed`, `run_start`, `run_end`, `heartbeat`

Backpressure: max 10 pending sends per client, 5s send timeout. Slow clients are skipped and logged.

## Game Integration

- **Framework:** Godot.NET.Sdk -- uses `GD.Print()` for logging, `Engine.GetMainLoop()` for scene access
- **Patching:** Harmony `PatchAll()` on assembly load. Patches use `[HarmonyPostfix]` to observe without modifying.
- **State Extraction:** `GameStateApi` uses `Harmony.Traverse` to reflectively read private/internal fields from game objects
- **Overlay:** `CanvasLayer` at layer 100, attached to scene tree root via `CallDeferred`
- **Patch Status:** All `[HarmonyPatch]` attributes are commented out. Target class names (e.g., `CardRewardScreen`, `CombatManager`, `TurnManager`, `DeckManager`, `MapManager`, `RunManager`) are educated guesses that need verification against actual game DLLs.

## Connection to SpireSense Web App

```
STS2 Game Process
  -> Harmony patches observe game state changes
  -> GameStateTracker (in-process, thread-safe)
  -> HTTP Server (localhost:8080) -- polled by web app
  -> WebSocket Server (localhost:8081) -- real-time push to web app overlay

SpireSense Web App (spiresense.app/overlay)
  -> WebSocket client (src/hooks/use-game-state.ts) connects to ws://localhost:8081
  -> HTTP fallback polls localhost:8080/api/state
  -> Feeds data into game store (React context + reducer)
  -> Engine layer scores cards, detects synergies, calculates draw probability
  -> AI layer (Claude) provides coaching based on game state + RAG context
```

## Key Decisions

- **Godot.NET.Sdk** over BepInEx -- STS2 uses Godot engine, not Unity
- **Harmony** for runtime patching -- works with any .NET runtime, including Godot Mono
- **Localhost-only servers** -- no auth needed, no network exposure
- **System.Text.Json** over Newtonsoft -- built into .NET 9, no extra dependency
- **Broadcast-only WebSocket** -- simpler protocol, web app never sends commands to mod
- **Traverse reflection** -- avoids compile-time dependency on game assemblies for field access
- **Commented-out patches** -- game is Early Access, class names change frequently; targets must be re-verified per update
