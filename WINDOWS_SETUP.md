# Windows Setup — Quick Start

## Prerequisites
- .NET 9 SDK (`dotnet --version` should show 9.x)
- Slay the Spire 2 installed via Steam

## 1. Configure local.props

```bash
copy local.props.example local.props
```

Edit `local.props` — set your STS2 install path:
```xml
<Project>
  <PropertyGroup>
    <STS2GamePath>C:\Program Files (x86)\Steam\steamapps\common\SlayTheSpire2</STS2GamePath>
  </PropertyGroup>
</Project>
```

> Common paths: `C:\Program Files (x86)\Steam\steamapps\common\SlayTheSpire2`
> or `D:\SteamLibrary\steamapps\common\SlayTheSpire2`

## 2. Build & Deploy

```bash
dotnet build SpireSenseMod/SpireSenseMod.csproj
```

This auto-copies `SpireSenseMod.dll` + `mod_manifest.json` to `$(STS2GamePath)\mods\SpireSense\`.

## 3. Launch & Verify

1. Start STS2 via Steam
2. Check the game log for `[SpireSense] Initializing...`
3. Log location: `%APPDATA%\SlayTheSpire2\logs\godot.log`

## 4. Test the Overlay

1. Open browser: `https://spiresense.app/overlay`
2. Login with your SpireSense account
3. The overlay connects to `ws://localhost:8081` (mod's WebSocket server)
4. Green indicator = connected, start a run to see card data flowing

## 5. What to Test

- **Card Rewards**: Pick cards, check if tier badges appear
- **Combat**: Check deck tracker (draw/discard/exhaust piles)
- **AI Coach**: Click hint button (needs Premium or Credits)
- **Connection**: Status indicator in overlay header

## Troubleshooting

- **Mod not loading**: Check `%APPDATA%\SlayTheSpire2\logs\godot.log` for errors
- **No `[SpireSense]` in logs**: Verify DLL is in `mods\SpireSense\` folder
- **Overlay "disconnected"**: Is STS2 running with mod? Check `http://localhost:8080/api/health`
- **Build fails**: Run `dotnet restore` first, ensure .NET 9 SDK installed

## Known Issues

- macOS ARM: Mods don't load (STS2 Godot engine limitation)
- Mod is read-only: never modifies game state, observation only
