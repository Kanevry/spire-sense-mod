# SpireSense Mod

> Game state extraction mod for Slay the Spire 2 -- feeds real-time data to the SpireSense web companion app.

## Quick Facts

- **Game Engine:** Godot 4.5.1 (STS2 runs on Godot, NOT Unity)
- **SDK:** Godot.NET.Sdk 4.5.1, .NET 9.0, C# (latest LangVersion)
- **Patching:** Harmony 2.4.2 (runtime method patching via `[HarmonyPatch]`, .NET 9 native)
- **Entry Point:** `[ModInitializer("Init")]` on `Plugin.Init()`, `Plugin.Unload()` for clean shutdown
- **Read-Only:** Never modifies game state. Observation only.
- **Status:** Production — Harmony patch targets verified via sts2.dll decompilation (5 patch files + 11 Hook subscriptions, observation-only)
- **Test count:** 176 tests across xUnit test project
- **Debug Mode:** `Plugin.DebugMode = true` enables TypeDiscovery (runtime assembly scanning)
- **Source Control:** GitHub (public, MIT), GitLab mirror

## Commands

```bash
dotnet build                    # Build DLL (auto-deploys to mods folder if STS2GamePath set)
dotnet build -c Release         # Release build
dotnet test SpireSenseMod.Tests # xUnit tests (176 tests, no Godot dependency)
```

## CI Pipeline

GitHub Actions (`.github/workflows/build.yml`):
- **test job** (ubuntu, no Godot): xUnit tests via source-file linking — all 176 tests must pass
- **build job** (ubuntu): `dotnet restore` + `dotnet format` + `dotnet build -c Release /p:TreatWarningsAsErrors=true`
- **release job** (on `v*` tags): Creates `SpireSense-v{version}.zip` with SHA256 checksums

## Setup

1. Copy `local.props.example` to `local.props`
2. Set `<STS2GamePath>` to your STS2 install directory
3. `dotnet build` -- DLL is auto-copied to `$(STS2GamePath)/mods/SpireSense/`

## Architecture

```
SpireSenseMod/
  Plugin.cs              # Entry point: Init() + Unload(), inits Harmony, HTTP, WebSocket, Overlay
  GameStateTracker.cs    # Thread-safe central state (serializes under lock, immutable snapshots), event emitter
  Models/                # JSON-serializable data models (System.Text.Json)
    GameState.cs         # Full snapshot: screen, character, act, floor, deck, relics, combat, map, shop, events
    CardInfo.cs          # Card with id, name, type, rarity, cost, tags, upgraded
    CombatState.cs       # Combat + PlayerState + MonsterInfo + PowerInfo + PotionInfo + MapNode + EventOption + RelicInfo
  Hooks/
    HookSubscriptions.cs # STS2 Hook system subscriptions (STD-003): 11 hooks — combat, turns, cards, potions, rooms, map, deck
    HookEventAdapter.cs  # Testable adapter: translates hook data into GameStateTracker mutations
  Patches/               # Harmony patches (fallback where no Hook exists) — all use [HarmonyPriority(Priority.HigherThanNormal)]
    CardRewardPatch.cs   # Card reward shown/picked (no Hook equivalent)
    DeckPatch.cs         # Relic obtained, run start/end (no Hook equivalent)
    ShopPatch.cs         # Shop exit + inventory extraction (entry detected via AfterRoomEntered)
    EventPatch.cs        # Event encounters, option extraction (no Hook equivalent)
    RestPatch.cs         # Rest choice tracking (entry detected via AfterRoomEntered)
  Server/
    HttpServer.cs        # HttpListener on localhost:8080, CORS, 5s timeout, /api/version
    WebSocketServer.cs   # HttpListener on localhost:8081, batched broadcast (50ms / 10 msgs), backpressure
    GameStateApi.cs      # Extracts game objects to models via Harmony Traverse (ExtractCards, ExtractPowers, ExtractPotions)
  Overlay/
    OverlayManager.cs    # Godot CanvasLayer (layer 100), lazy-inits on scene tree
    TierBadge.cs         # Custom Control -- draws colored S/A/B/C/D/F badges
  Data/
    CardTiers/           # Empty -- planned for static tier data
    TypeDiscovery.cs     # Runtime assembly scanner for STS2 class name verification (debug mode only)
```

## Thread-Safety Model

- **GameStateTracker:** Central state protected by `lock(_lock)`. All mutations serialize under the lock and produce an immutable JSON snapshot string. Readers get the pre-serialized snapshot — no deserialization race conditions. `CurrentCombatState` is `volatile` for lock-free reads.
- **Event Buffer:** Separate `_eventLock` protects the ring buffer (max 100 events). Events are appended under lock, reads return snapshot lists.
- **WebSocket Broadcast:** Events are queued in a `ConcurrentQueue<GameEvent>` and flushed by a 50ms `Timer` (or immediately when the queue reaches 10 messages). An `Interlocked` guard prevents concurrent flushes.
- **Patch Thread:** Harmony postfix patches run on the game's main thread. They call `GameStateTracker.UpdateState()` which acquires the lock, mutates, serializes, and emits events.
- **HttpListener Disposal:** Both `HttpServer` and `WebSocketServer` properly dispose `HttpListener` via `Close()` in their `Stop()` methods — no leaked listeners on mod unload.
- **Rule:** Never hold `_lock` and `_eventLock` simultaneously. Never modify `GameStateTracker._currentState` outside the lock.

## Patch Priorities

All Harmony patch classes (11 Hook subscriptions + 10 direct patches) use `[HarmonyPriority(Priority.HigherThanNormal)]` (600). This ensures SpireSense patches run after core game logic completes but before any other third-party mod patches that might transform the data. The priority is applied at the class level, affecting all methods within.

## WebSocket Batching

Events are not sent immediately. Instead:
1. `EnqueueEvent()` adds to a `ConcurrentQueue<GameEvent>`
2. A `System.Threading.Timer` fires every 50ms to flush the queue
3. If the queue reaches 10 messages before the timer fires, an immediate flush is triggered
4. Initial `state_update` on client connect bypasses the batch queue for instant delivery
5. Existing backpressure (max 10 pending sends per client, 5s send timeout) is preserved

This reduces network overhead during rapid combat events where multiple state changes happen within milliseconds.

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

Batching: events queued and flushed every 50ms or at 10-message threshold. Initial state bypasses queue.

## Game Integration

- **Framework:** Godot.NET.Sdk -- uses `GD.Print()` for logging, `Engine.GetMainLoop()` for scene access
- **Patching:** Harmony `PatchAll()` on assembly load. 11 Hook subscriptions + 9 direct patches, all `[HarmonyPostfix]` to observe without modifying. All patches at `Priority.HigherThanNormal` (600).
- **State Extraction:** `GameStateApi` uses `Harmony.Traverse` to reflectively read private/internal fields from game objects
- **Overlay:** `CanvasLayer` at layer 100, attached to scene tree root via `CallDeferred`
- **Patch Status:** All `[HarmonyPatch]` attributes target decompiled class names from sts2.dll (MegaCrit.Sts2.Core namespace). Targets need re-verification per game update.

## Connection to SpireSense Web App

```
STS2 Game Process
  -> Harmony patches observe game state changes (HigherThanNormal priority)
  -> GameStateTracker (in-process, thread-safe, lock + immutable snapshots)
  -> HTTP Server (localhost:8080) -- polled by web app
  -> WebSocket Server (localhost:8081) -- batched real-time push (50ms / 10 msgs)

SpireSense Web App (spiresense.app/overlay)
  -> WebSocket client (src/hooks/use-game-state.ts) connects to ws://localhost:8081
  -> HTTP fallback polls localhost:8080/api/state
  -> Feeds data into game store (React context + reducer)
  -> Engine layer scores cards, detects synergies, calculates draw probability
  -> AI layer (Claude) provides coaching based on game state + RAG context
```

## Session 18 Fixes

### CardRewardPatch
- `ShowScreen` is a **static** method — Harmony postfix uses `__result` and `__0` parameters (no `__instance`)
- Correct property for card data is `Card` (not `CardModel`) on reward screen items

### DeckPatch PileType Resilience
- PileType matching uses `.ToString() == "Deck" || .EndsWith(".Deck")` for resilience against namespace changes

### Data Extraction Improvements
- **costUpgraded:** `CardEnergyCost.Canonical` = base/original cost, `_base` = current/upgraded cost
- **Tags:** Extracted from `CardModel.Tags` (CardTag flags) + `Keywords` (CardKeyword flags)
- **DescriptionUpgraded:** Retrieved via `CardModel.GetDescriptionForUpgradePreview()`

### Thread Safety Improvements
- `CurrentCombatState` field is now `volatile` for lock-free reads
- `_dumpedTypes` in TypeDiscovery uses `ConcurrentDictionary` instead of `Dictionary`

## Known Decompiled Types

Reference for STS2 internal types (verified via `ilspycmd` against sts2.dll):

### PileType Enum
`None`, `Draw`, `Hand`, `Discard`, `Exhaust`, `Play`, `Deck`

### CardTag Enum
`None`, `Strike`, `Defend`, `Minion`, `OstyAttack`, `Shiv`

### CardKeyword Enum
`None`, `Exhaust`, `Ethereal`, `Innate`, `Unplayable`, `Retain`, `Sly`, `Eternal`

### CardEnergyCost
- `Canonical` — original/base cost (unmodified)
- `_base` — current cost (reflects upgrades)
- `UpgradeBy(int)` — modifies `_base` by delta

### CardModel
- `GetDescriptionForUpgradePreview()` — returns upgraded card description text
- `Tags` — `CardTag` flags enum
- `Keywords` — `CardKeyword` flags enum

## Session 19 Changes

### Hook Migration (STD-003)
- Migrated `PotionPatch` (OnPotionUsed, OnPotionObtained) to Hook system: `Hook.AfterPotionUsed`, `Hook.AfterPotionProcured`
- Migrated `MapPatch` (OnFloorChanged) to Hook system: `Hook.AfterRoomEntered`
- Deleted `PotionPatch.cs` and `MapPatch.cs` — replaced by `HookSubscriptions.cs`
- Decompiled Hook signatures from sts2.dll: AfterPotionUsed(IRunState, CombatState?, PotionModel, Creature?), AfterPotionProcured(IRunState, CombatState?, PotionModel), AfterRoomEntered(IRunState, AbstractRoom)

### Resource & Logging Improvements
- `HttpServer.cs`: Response OutputStream now explicitly disposed via `using` block
- `GameStateApi.cs`: All empty catch blocks now log errors via `GD.PrintErr()` for visibility

## Session 20 Changes

### Hook Migrations
- Migrated CardAdded (DeckPatch.OnCardAdded) to `Hook.AfterCardChangedPiles` — filters for PileType.Deck
- Migrated CardRemoved (DeckPatch.OnCardRemoved) to `Hook.BeforeCardRemoved` — fires before card is removed
- Extended AfterRoomEntered for Shop/Rest room type detection (replaces separate ShopPatch.OnShopEntered, RestPatch.OnRestEntered)
- Decompiled ~90 Hook methods — documented which are available vs. which need Harmony

### Patch File Changes
- DeckPatch: removed OnCardAdded/OnCardRemoved, kept RelicObtained/RunStart/RunEnd (no Hook equivalent)
- ShopPatch: removed OnShopEntered (room type detected via AfterRoomEntered), kept OnShopExited (no Hook)
- RestPatch: removed OnRestEntered (room type detected via AfterRoomEntered), kept OnRestChoice (no Hook)
- CardRewardPatch + EventPatch: kept as Harmony (no equivalent hooks)
- 4 Hook migrations + 9 Harmony fallbacks documented in `docs/sts2-class-mapping.md`

### Hook Subscription Count
- HookSubscriptions.cs: 11 hooks (was 9 in Session 19, added AfterCardChangedPiles + BeforeCardRemoved)
- Harmony patches: 5 files with 9 direct patches (was 5 files with 13 patches — removed 4)
- Remaining patches need Harmony — no equivalent hooks available
- **Non-trivial hook candidates:** `BeforeRewardsOffered`, `AfterRewardTaken`, `AfterRestSiteHeal`, `AfterRestSiteSmith` hooks exist in STS2 but migration is non-trivial due to data signature mismatches with current patch implementations

## Key Decisions

- **Godot.NET.Sdk** over BepInEx -- STS2 uses Godot engine, not Unity
- **Harmony** for runtime patching -- works with any .NET runtime, including Godot Mono
- **Localhost-only servers** -- no auth needed, no network exposure
- **System.Text.Json** over Newtonsoft -- built into .NET 9, no extra dependency
- **Broadcast-only WebSocket** -- simpler protocol, web app never sends commands to mod
- **Traverse reflection** -- avoids compile-time dependency on game assemblies for field access
- **HigherThanNormal priority** -- ensures SpireSense reads stable game state before other mods transform it
- **50ms batch interval** -- balances latency vs. network overhead during rapid combat events
- **Immutable snapshots** -- serialized under lock, broadcast without holding locks

## Standards & Patterns

- **MOD-001 (Traverse via helpers):** All Traverse operations via `GameStateApi.GetProp()`/`GetField()`/`GetCollection()` — no raw `Traverse.Create()` in Patches/Hooks. Traverse stays only in GameStateApi.cs implementation.
- **MOD-002 (TargetMethod logging):** When `AccessTools.TypeByName()` returns null in `[HarmonyTargetMethod]`, ALWAYS log with `GD.PrintErr("[SpireSense] PatchName: Could not resolve target type TypeName")`. Never silently return null.
