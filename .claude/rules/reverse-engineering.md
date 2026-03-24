# Reverse Engineering Rule (Always-on)

## STS2 API Discovery (RE-001)

When you need to find property names, method signatures, enum values, or class structure from Slay the Spire 2:

1. **ALWAYS decompile `sts2.dll` first** — never guess property names
2. **Tool:** `ilspycmd` (install via `dotnet tool install -g ilspycmd` if needed)
3. **DLL location:** `"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll"`
4. **Commands:**
   ```bash
   # Decompile a specific type
   ilspycmd "path/to/sts2.dll" -t "MegaCrit.Sts2.Core.Full.TypeName"

   # Search for types/members
   ilspycmd "path/to/sts2.dll" -t "TypeName" 2>&1 | grep -i "propertyName"
   ```

5. **After decompiling:** Update `docs/sts2-class-mapping.md` with the new findings
6. **Never assume** property names from similar games, conventions, or debug dumps alone
7. **Debug dumps (`DumpObjectOnce`) supplement but don't replace decompilation** — they show runtime values but miss method signatures, delegate types, private implementations

## Verified Patterns from Decompilation

### Intent System
- `Creature.Monster` → `MonsterModel`
- `MonsterModel.NextMove` → `MoveState`
- `MoveState.Intents` → `IReadOnlyList<AbstractIntent>`
- `AbstractIntent.IntentType` → `IntentType` enum
- `AttackIntent.DamageCalc` → `Func<decimal>` (base damage calculator)
- `IntentType` enum: `Attack, Buff, Debuff, DebuffStrong, Defend, Escape, Heal, Hidden, Summon, Sleep, Stun, StatusCard, CardDebuff, DeathBlow, Unknown`

### Map System
- `StandardActMap.Grid` → `MapPoint[,]` (2D array, NOT IEnumerable)
- `MapPoint.coord` → `MapCoord` (public field, not property)
- `MapPoint.PointType` → `MapPointType` enum (not "Type")
- `MapPoint.Children` → `HashSet<MapPoint>`
- `MapCoord.col`, `MapCoord.row` → `int` (public fields)
- `MapPointType` enum: `Unassigned, Unknown, Shop, Treasure, RestSite, Monster, Elite, Boss, Ancient`

### Relic System
- `RelicModel.CanonicalInstance` → `RelicModel` (string repr: `"RELIC.NAME (id)"`)
- `RelicModel.Id` → `ModelId` (string repr: `"RELIC.NAME"`)
- `RelicModel.HoverTip` → `HoverTip` with `.Title` (string) and `.Description` (string, BBCode)
- `RelicModel.Pool` → `RelicPoolModel` (string repr contains character name)
- No `Name` or `RelicId` property exists — use `HoverTip.Title` for display name

### Card System
- `CardModel.CanonicalInstance` → `CardModel` (string repr: `"CARD.NAME (id)"`)
- `CardModel.Title` → `string` (display name)
- `CardModel.Description` → `LocString` (needs TranslationServer)
- `CardModel.EnergyCost.Canonical` → `int` (base cost)
- `CardModel.Pool` → card pool with character name

### Reflection Rules
- **Traverse.Property().GetValue<T>()** returns null for `IReadOnlyList<T>`, `IEnumerable<T>`, and enum types
- **Always use `GameStateApi.GetProp()`** (direct reflection) for: collections, enums, complex types
- **Traverse is OK only for:** primitives (int, bool, string)
- **Public fields** (like `MapCoord.col`) need `GetField()` with `BindingFlags.Public | BindingFlags.Instance`

## See Also
docs/sts2-class-mapping.md — Full decompiled class reference
