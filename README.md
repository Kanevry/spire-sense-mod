# SpireSense Mod for Slay the Spire 2

AI-powered companion mod that extracts game state and provides real-time data to the [SpireSense](https://spiresense.app) web companion app.

## Features

- **Game State Extraction** — Reads card rewards, combat state, deck, relics, and map via Harmony patches
- **HTTP API** — REST endpoints at `localhost:8080` for external tool integration
- **WebSocket Events** — Real-time game event streaming for live overlays
- **In-Game Tier Badges** — Minimal S/A/B/C/D/F tier indicators on card rewards (CanvasLayer)
- **READ-ONLY** — Never modifies game state or provides unfair advantages

## Installation

1. Download the latest release from [Releases](https://github.com/Kanevry/spire-sense-mod/releases)
2. Extract to your STS2 mods folder:
   ```
   %APPDATA%/Godot/app_userdata/Slay the Spire 2/mods/SpireSense/
   ```
3. Files should be:
   ```
   mods/SpireSense/
   ├── SpireSenseMod.dll
   ├── SpireSenseMod.pck (if applicable)
   └── mod_manifest.json
   ```
4. Launch Slay the Spire 2 — the mod loads automatically
5. Open [spiresense.app/overlay](https://spiresense.app/overlay) for the full companion experience

## HTTP API

The mod runs a local HTTP server at `http://localhost:8080`.

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/state` | Full game state snapshot |
| GET | `/api/health` | Server health check |
| GET | `/api/deck` | Current deck contents |
| GET | `/api/combat` | Current combat state (404 if not in combat) |

### Example Response (`/api/state`)

```json
{
  "screen": "combat",
  "character": "ironclad",
  "act": 1,
  "floor": 6,
  "ascension": 5,
  "seed": "ABC123",
  "deck": [
    { "id": "strike", "name": "Strike", "type": "attack", "cost": 1, "upgraded": false }
  ],
  "relics": [
    { "id": "burning_blood", "name": "Burning Blood", "rarity": "starter" }
  ],
  "combat": {
    "turn": 2,
    "player": { "hp": 65, "maxHp": 80, "block": 5, "energy": 3 },
    "monsters": [
      { "id": "jaw_worm", "name": "Jaw Worm", "hp": 30, "maxHp": 44, "intent": "attack" }
    ],
    "hand": ["..."],
    "drawPile": ["..."],
    "discardPile": ["..."]
  }
}
```

## WebSocket Events

Connect to `ws://localhost:8081` for real-time events.

### Event Types

| Type | Description | Data |
|------|-------------|------|
| `state_update` | Full state snapshot | `GameState` |
| `card_picked` | Card selected from rewards | `{ card, alternatives }` |
| `card_played` | Card played in combat | `{ card, target }` |
| `relic_obtained` | Relic acquired | `{ relic }` |
| `combat_start` | Combat begins | `{ monsters }` |
| `combat_end` | Combat ends | `{ won, floor }` |
| `floor_changed` | Player moved to new floor | `{ floor, node }` |
| `run_start` | New run begins | `{ character, ascension, seed }` |
| `run_end` | Run ends | `{ won, floor, score }` |

## Development

### Prerequisites

- .NET 9.0 SDK
- Slay the Spire 2 (with mod support)
- Godot 4.5.1 Mono (optional, for `.pck` creation)

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/Kanevry/spire-sense-mod.git
   ```
2. Create `local.props` with your game path:
   ```xml
   <Project>
     <PropertyGroup>
       <STS2GamePath>C:\Program Files (x86)\Steam\steamapps\common\SlayTheSpire2</STS2GamePath>
     </PropertyGroup>
   </Project>
   ```
3. Build:
   ```bash
   dotnet build
   ```
   The DLL is automatically copied to the mods folder if `STS2GamePath` is set.

### Project Structure

```
SpireSenseMod/
├── Plugin.cs              # Entry point ([ModInitializer])
├── GameStateTracker.cs    # Central state management (thread-safe)
├── Patches/               # Harmony patches
│   ├── CardRewardPatch.cs # Card reward screen hooks
│   ├── CombatPatch.cs     # Combat state tracking
│   ├── MapPatch.cs        # Floor/map navigation
│   └── DeckPatch.cs       # Deck/relic changes, run start/end
├── Server/
│   ├── HttpServer.cs      # REST API (localhost:8080)
│   ├── WebSocketServer.cs # Real-time events (localhost:8081)
│   └── GameStateApi.cs    # Game object → model extraction
├── Overlay/
│   ├── OverlayManager.cs  # Godot CanvasLayer management
│   └── TierBadge.cs       # Tier grade UI element
├── Models/                # Data models (JSON-serializable)
│   ├── GameState.cs
│   ├── CardInfo.cs
│   └── CombatState.cs
└── mod_manifest.json
```

## Early Access Notice

Slay the Spire 2 is in Early Access. Game class names and internal structures may change with updates. Harmony patch targets (currently commented out) need to be verified against each game update.

## License

MIT — See [LICENSE](LICENSE) for details.

## Related

- [SpireSense Web App](https://spiresense.app) — Full companion with AI Coach, analytics, and community data
- [spire-codex](https://github.com/ptrlrd/spire-codex) — STS2 game data API
- [BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2) — STS2 mod framework
