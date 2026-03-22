# SpireSense Mod for Slay the Spire 2

AI-powered companion mod that extracts game state from Slay the Spire 2 and streams it in real time to the [SpireSense](https://spiresense.app) web companion app. Provides a local HTTP API and WebSocket server for external tool integration, plus minimal in-game tier badges on card rewards.

**READ-ONLY** -- this mod never modifies game state or provides unfair advantages.

## Architecture

```
Plugin.Init()
  |
  +-- GameStateTracker  (thread-safe central state, lock + serialized snapshots)
  |     |
  |     +-- Harmony Patches (8 patch classes hook into STS2 internals)
  |     |     CardRewardPatch, CombatPatch, DeckPatch, MapPatch,
  |     |     ShopPatch, EventPatch, RestPatch, PotionPatch
  |     |
  |     +-- GameStateApi  (Traverse-based extraction from game objects)
  |
  +-- HttpServer         (REST API at localhost:8080, CORS enabled)
  +-- WebSocketServer    (real-time events at ws://localhost:8081, backpressure)
  +-- OverlayManager     (Godot CanvasLayer with TierBadge UI elements)

Plugin.Unload()
  -- stops servers, unpatches Harmony, releases resources
```

The mod runs on the Godot 4.5 + C# / .NET 9 engine that powers STS2. Harmony 2.4.2 (built into STS2) patches game methods to capture state changes without modifying gameplay.

## Supported Characters

`ironclad` | `silent` | `defect` | `regent` | `necrobinder` | `deprived`

Unrecognized character IDs are normalized to `"unknown"` by `CharacterValidator`.

## HTTP API

The mod runs a local HTTP server at `http://localhost:8080` with CORS headers for browser access. All responses are `application/json`. Requests that exceed 5 seconds are terminated with a `504` response.

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/state` | Full game state snapshot |
| GET | `/api/health` | Server health check |
| GET | `/api/version` | Mod and API version info |
| GET | `/api/deck` | Current deck contents |
| GET | `/api/combat` | Current combat state (404 if not in combat) |
| GET | `/api/relics` | Current relic collection |
| GET | `/api/map` | Current map state and floor |
| GET | `/api/events?since={timestamp}` | Buffered events since Unix ms timestamp |

### `GET /api/state`

Returns the complete game state snapshot.

```json
{
  "screen": "combat",
  "character": "ironclad",
  "act": 1,
  "floor": 6,
  "ascension": 5,
  "seed": "ABC123",
  "deck": [
    {
      "id": "strike",
      "name": "Strike",
      "character": "ironclad",
      "type": "attack",
      "rarity": "common",
      "cost": 1,
      "costUpgraded": 1,
      "description": "Deal 6 damage.",
      "descriptionUpgraded": "Deal 9 damage.",
      "upgraded": false,
      "tags": []
    }
  ],
  "relics": [
    {
      "id": "burning_blood",
      "name": "Burning Blood",
      "rarity": "starter",
      "description": "At the end of combat, heal 6 HP.",
      "character": "ironclad",
      "tags": []
    }
  ],
  "combat": {
    "turn": 2,
    "player": {
      "hp": 65,
      "maxHp": 80,
      "block": 5,
      "energy": 3,
      "maxEnergy": 3,
      "gold": 99,
      "powers": [{ "id": "strength", "name": "Strength", "amount": 2 }],
      "potions": [{ "id": "fire_potion", "name": "Fire Potion", "description": "Deal 20 damage.", "canUse": true }]
    },
    "monsters": [
      {
        "id": "jaw_worm",
        "name": "Jaw Worm",
        "hp": 30,
        "maxHp": 44,
        "block": 0,
        "intent": "attack",
        "intentDamage": 11,
        "powers": []
      }
    ],
    "hand": [],
    "drawPile": [],
    "discardPile": [],
    "exhaustPile": []
  },
  "map": [
    { "x": 0, "y": 0, "type": "monster", "connections": [1], "visited": true }
  ],
  "cardRewards": null,
  "shopCards": null,
  "shopRelics": null,
  "eventOptions": null,
  "restOptions": null
}
```

### `GET /api/health`

```json
{
  "status": "ok",
  "mod": "SpireSense",
  "version": "0.1.0",
  "port": 8080
}
```

### `GET /api/version`

```json
{
  "mod": "SpireSense",
  "version": "0.1.0",
  "api": "v1",
  "game": "Slay the Spire 2"
}
```

### `GET /api/deck`

```json
{
  "deck": [
    { "id": "strike", "name": "Strike", "character": "ironclad", "type": "attack", "rarity": "common", "cost": 1, "costUpgraded": 1, "description": "Deal 6 damage.", "descriptionUpgraded": "Deal 9 damage.", "upgraded": false, "tags": [] }
  ],
  "count": 10
}
```

### `GET /api/combat`

Returns `404` with `{ "error": "Not in combat" }` when not in combat. During combat:

```json
{
  "turn": 2,
  "player": { "hp": 65, "maxHp": 80, "block": 5, "energy": 3, "maxEnergy": 3, "gold": 99, "powers": [], "potions": [] },
  "monsters": [
    { "id": "jaw_worm", "name": "Jaw Worm", "hp": 30, "maxHp": 44, "block": 0, "intent": "attack", "intentDamage": 11, "powers": [] }
  ],
  "hand": [],
  "drawPile": [],
  "discardPile": [],
  "exhaustPile": []
}
```

### `GET /api/relics`

```json
{
  "relics": [
    { "id": "burning_blood", "name": "Burning Blood", "rarity": "starter", "description": "At the end of combat, heal 6 HP.", "character": "ironclad", "tags": [] }
  ],
  "count": 1
}
```

### `GET /api/map`

```json
{
  "map": [
    { "x": 0, "y": 0, "type": "monster", "connections": [1], "visited": true },
    { "x": 1, "y": 1, "type": "elite", "connections": [2], "visited": false }
  ],
  "currentFloor": 3
}
```

### `GET /api/events?since={timestamp}`

Returns buffered events (ring buffer, max 100) with timestamps strictly greater than the `since` parameter (Unix milliseconds). Omit `since` or pass `0` to get all buffered events.

```json
{
  "events": [
    { "type": "card_picked", "data": { "card": { "id": "bash" }, "alternatives": [] }, "timestamp": 1711100000000 },
    { "type": "floor_changed", "data": { "floor": 7, "node": { "x": 1, "y": 7, "type": "rest", "visited": true } }, "timestamp": 1711100001000 }
  ],
  "count": 2
}
```

## WebSocket Events

Connect to `ws://localhost:8081` for real-time game event streaming. On connect, the server immediately sends a `state_update` with the current full state. The server sends periodic `heartbeat` messages every 30 seconds to detect dead connections. Backpressure is enforced: clients with more than 10 pending sends are skipped.

All messages are JSON with the shape `{ "type": "<event_type>", "data": <payload> }`.

### Event Types (18 total)

| Type | Trigger | Payload |
|------|---------|---------|
| `state_update` | Any state mutation | Full `GameState` snapshot |
| `heartbeat` | Every 30s | `null` |
| `run_start` | New run begins | `{ character, ascension, seed }` |
| `run_end` | Run ends (victory/defeat) | `{ won, floor, score }` |
| `floor_changed` | Player moves to new floor / enters shop | `{ floor, node }` or `{ screen, shopCards, shopRelics }` |
| `combat_start` | Combat begins | `{ monsters }` |
| `combat_end` | Combat ends | `{ won, isBoss, floor }` |
| `card_played` | Card played in combat | `{ card, target }` |
| `card_rewards_shown` | Card rewards displayed | `{ cards }` |
| `card_picked` | Card selected from rewards | `{ card, alternatives }` |
| `card_removed` | Card removed from deck | `{ card }` |
| `relic_obtained` | Relic acquired | `{ relic }` |
| `event_started` | Event encounter begins | `{ name, description, options }` |
| `event_choice` | Player selects event option | `{ choiceId, choiceName }` |
| `rest_entered` | Rest site entered | `{ options }` |
| `rest_choice` | Rest site option selected | `{ choiceId, choiceName }` |
| `potion_used` | Potion consumed | `{ potion, target }` |
| `potion_obtained` | Potion acquired | `{ potion }` |

### Screen Types

The `screen` field in the game state uses these constants:

`main_menu` | `map` | `combat` | `card_reward` | `shop` | `rest` | `event` | `boss_reward` | `game_over` | `victory` | `chest` | `grid_select` | `hand_select`

## Installation

### Prerequisites

- Slay the Spire 2 (with mod support enabled)

### Steps

1. Download the latest release from [Releases](https://github.com/Kanevry/spire-sense-mod/releases)
2. Extract to your STS2 mods folder:
   ```
   %APPDATA%/Godot/app_userdata/Slay the Spire 2/mods/SpireSense/
   ```
3. Verify the file structure:
   ```
   mods/SpireSense/
   ├── SpireSenseMod.dll
   └── mod_manifest.json
   ```
4. Launch Slay the Spire 2 -- the mod loads automatically via `[ModInitializer]`
5. Open [spiresense.app/overlay](https://spiresense.app/overlay) for the full companion experience

## Development

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Slay the Spire 2 (with mod support, for reference assemblies)
- Godot 4.5.1 Mono (resolved via `Godot.NET.Sdk` NuGet package)

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/Kanevry/spire-sense-mod.git
   cd spire-sense-mod
   ```

2. Create `local.props` with your game path (optional, enables auto-deploy on build):
   ```xml
   <Project>
     <PropertyGroup>
       <STS2GamePath>C:\Program Files (x86)\Steam\steamapps\common\SlayTheSpire2</STS2GamePath>
     </PropertyGroup>
   </Project>
   ```

3. Build the mod:
   ```bash
   dotnet build SpireSenseMod/SpireSenseMod.csproj --configuration Release
   ```
   When `STS2GamePath` is set, the DLL and manifest are automatically copied to the mods folder.

4. Run the tests (no Godot/game dependency required):
   ```bash
   dotnet test SpireSenseMod.Tests/SpireSenseMod.Tests.csproj --configuration Release
   ```

### Project Structure

```
spire-sense-mod/
├── SpireSenseMod/
│   ├── Plugin.cs                    # Entry point ([ModInitializer] Init + Unload)
│   ├── GameStateTracker.cs          # Thread-safe state (lock + serialized snapshots + event ring buffer)
│   ├── Compat/
│   │   └── ModInitializerAttribute.cs
│   ├── Data/
│   │   └── TypeDiscovery.cs         # Debug-mode type introspection
│   ├── Models/
│   │   ├── GameState.cs             # Full state + GameEvent + BufferedEvent + all model classes
│   │   ├── CardInfo.cs              # Card data model
│   │   ├── CombatState.cs           # Combat + Player + Monster + Power + Potion + Map + Event + Relic + Rest + Shop models
│   │   ├── ScreenType.cs            # Screen type string constants
│   │   └── CharacterValidator.cs    # Character ID validation (6 known characters)
│   ├── Patches/
│   │   ├── CardRewardPatch.cs       # Card reward screen hooks
│   │   ├── CombatPatch.cs           # Combat start/turn/card-played/end
│   │   ├── DeckPatch.cs             # Deck/relic changes, run start/end
│   │   ├── MapPatch.cs              # Floor/map navigation
│   │   ├── ShopPatch.cs             # Shop entry/exit with cards and relics
│   │   ├── EventPatch.cs            # Event encounter start and choice
│   │   ├── RestPatch.cs             # Rest site entry and choice
│   │   └── PotionPatch.cs           # Potion usage and acquisition
│   ├── Server/
│   │   ├── HttpServer.cs            # REST API (localhost:8080, 5s timeout)
│   │   ├── WebSocketServer.cs       # Real-time events (localhost:8081, backpressure)
│   │   └── GameStateApi.cs          # Game object -> model extraction via Traverse
│   ├── Overlay/
│   │   ├── OverlayManager.cs        # Godot CanvasLayer management
│   │   └── TierBadge.cs             # Tier grade UI element (S/A/B/C/D/F)
│   ├── SpireSenseMod.csproj
│   └── mod_manifest.json
├── SpireSenseMod.Tests/
│   ├── GameStateTrackerTests.cs
│   ├── CharacterValidatorTests.cs
│   ├── Models/
│   │   ├── GameStateSerializationTests.cs
│   │   ├── CardInfoSerializationTests.cs
│   │   ├── CombatStateSerializationTests.cs
│   │   ├── BufferedEventSerializationTests.cs
│   │   ├── RestOptionSerializationTests.cs
│   │   └── ModelCompletenessTests.cs
│   └── SpireSenseMod.Tests.csproj
├── docs/                            # Additional documentation
├── global.json                      # .NET SDK + Godot.NET.Sdk pinning
├── local.props.example              # Template for local game path config
├── LICENSE                          # MIT
└── README.md
```

### CI

GitHub Actions runs on every push and PR to `main`:

- **test** -- Restores, builds, and runs xUnit tests (no Godot dependency; testable source files are linked directly)
- **build** -- Restores, checks formatting (`dotnet format --verify-no-changes`), builds with warnings as errors, verifies `mod_manifest.json`, uploads artifact
- **release** -- On `v*` tags: downloads artifact, creates a release `.zip` with SHA-256 checksums, publishes GitHub Release

### Decompilation Notes

STS2 uses Godot 4.5 + C# / .NET 9. Game classes live in `sts2.dll` under the `MegaCrit.Sts2.Core` namespace. Harmony patches target specific methods using `[HarmonyPatch("Namespace.Class", "Method")]` with `Traverse` for runtime reflection. Since STS2 is in Early Access, internal class names and structures may change with game updates.

## Early Access Notice

Slay the Spire 2 is in Early Access. Game class names and internal structures may change with updates. Harmony patch targets need to be verified against each game update.

## License

MIT -- See [LICENSE](LICENSE) for details.

## Related

- [SpireSense Web App](https://spiresense.app) -- Full companion with AI Coach, analytics, and community data
- [spire-codex](https://github.com/ptrlrd/spire-codex) -- STS2 game data API
- [BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2) -- STS2 mod framework
