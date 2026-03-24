# STS2 Class Mapping

Decompiled from `sts2.dll` (8.5MB, 6128 classes) using `ilspycmd` on 2026-03-22.
Game: Slay the Spire 2 (Godot 4.5 + C#/.NET 9, HarmonyLib 2.4.2 built-in).

---

## Summary: Guessed vs Actual

| Patch File | Guessed Class | Actual Class | Guessed Method | Actual Method(s) |
|---|---|---|---|---|
| CardRewardPatch | `CardRewardScreen` | `NCardRewardSelectionScreen` | `ShowRewards` | `ShowScreen()` (static) |
| CardRewardPatch | `CardRewardScreen` | `NCardRewardSelectionScreen` | `OnCardSelected` | `SelectCard()` |
| CombatPatch | `CombatManager` | `CombatManager` (correct!) | `StartCombat` | `SetUpCombat()` + `StartCombatInternal()` |
| CombatPatch | `TurnManager` | `CombatManager` | `StartPlayerTurn` | `SetupPlayerTurn()` |
| CombatPatch | `CardManager` | `PlayCardAction` | `PlayCard` | `ExecuteAction()` (via GameAction) |
| CombatPatch | `CombatManager` | `CombatManager` | `EndCombat` | `EndCombatInternal()` |
| DeckPatch | `DeckManager` | `CardPileCmd` | `AddCard` | `Add()` (static) |
| DeckPatch | `DeckManager` | `CardPileCmd` | `RemoveCard` | `RemoveFromDeck()` (static) |
| DeckPatch | `RelicManager` | `RelicCmd` | `ObtainRelic` | `Obtain()` (static) |
| DeckPatch | `RunManager` | `RunManager` (correct!) | `StartRun` | `Launch()` |
| DeckPatch | `RunManager` | `RunManager` | `EndRun` | `OnEnded()` |
| MapPatch | `MapManager` | `RunManager` | `TravelToNode` | `EnterMapCoordInternal()` |
| ShopPatch | `ShopScreen` | `MerchantRoom` / `NMerchantInventory` | `OnEnter` | `Enter()` / `Open()` |
| ShopPatch | `ShopScreen` | `MerchantRoom` / `NMerchantInventory` | `OnExit` | `Exit()` / `Close()` |
| EventPatch | `EventScreen` | `EventModel` | `ShowEvent` | `BeginEvent()` |
| EventPatch | `EventScreen` | `NEventOptionButton` | `OnChoiceSelected` | `OnRelease()` |
| RestPatch | `RestScreen` | `RestSiteRoom` | `OnEnter` | `Enter()` |
| RestPatch | `RestScreen` | `NRestSiteButton` / `RestSiteOption` | `OnOptionSelected` | `SelectOption()` / `OnSelect()` |
| PotionPatch | `PotionManager` | `UsePotionAction` | `UsePotion` | `ExecuteAction()` |
| PotionPatch | `PotionManager` | `PotionCmd` | `ObtainPotion` | `TryToProcure()` |

---

## Detailed Class Reference

### Namespace Root: `MegaCrit.Sts2.Core`

All game classes live under this root namespace.

---

### 1. Card Rewards

**Guessed:** `CardRewardScreen.ShowRewards()` / `CardRewardScreen.OnCardSelected()`

**Actual:**
- **`MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen`**
  - `static ShowScreen(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> extraOptions)` -- shows reward screen
  - `SelectCard(NCardHolder cardHolder)` -- player picks a card
  - `CardsSelected()` -- async, returns selected cards
  - `RefreshOptions(...)` -- updates displayed options
  - `AfterOverlayClosed()` -- cleanup
- **`MegaCrit.Sts2.Core.Rewards.CardReward`** -- data model for card reward
- **`MegaCrit.Sts2.Core.Commands.RewardsCmd`**
  - `static OfferForRoomEnd(Player player, AbstractRoom room)` -- triggers reward offering
- **`MegaCrit.Sts2.Core.Hooks.Hook`**
  - `AfterRewardTaken(IRunState, Player, Reward)` -- hook after any reward taken
  - `BeforeRewardsOffered(IRunState, Player, IReadOnlyList<Reward>)` -- hook before rewards shown

**Best patch targets:**
- `NCardRewardSelectionScreen.ShowScreen` (Postfix) -- card rewards displayed
- `NCardRewardSelectionScreen.SelectCard` (Postfix) -- card picked
- `Hook.AfterRewardTaken` (Postfix) -- any reward taken

---

### 2. Combat

**Guessed:** `CombatManager.StartCombat` / `TurnManager.StartPlayerTurn` / `CardManager.PlayCard` / `CombatManager.EndCombat`

**Actual:**
- **`MegaCrit.Sts2.Core.Combat.CombatManager`** (Singleton: `CombatManager.Instance`)
  - `SetUpCombat(CombatState state)` -- initializes combat
  - `StartCombatInternal()` -- async, starts combat loop
  - `SetupPlayerTurn(Player, HookPlayerChoiceContext)` -- private, sets up player turn
  - `EndCombatInternal()` -- async, ends combat
  - `EndPlayerTurnPhaseOneInternal()` -- async
  - `EndPlayerTurnPhaseTwoInternal()` -- async
  - `SwitchFromPlayerToEnemySide(...)` -- async
  - `Reset(bool graceful)` -- cleanup
  - Events: `CombatSetUp`, `CreaturesChanged`, `TurnStarted`, `TurnEnded`, `PlayerEndedTurn`
- **`MegaCrit.Sts2.Core.Combat.CombatState`**
  - `AddCard(CardModel, Player)` -- card enters combat
  - `RemoveCard(CardModel)` -- card leaves combat
  - `AddPlayer(Player)` -- player joins combat
  - `CreateCreature(MonsterModel, CombatSide, string?)` -- spawns creature
  - Properties: `Allies`, `Enemies`, `RoundNumber`
- **`MegaCrit.Sts2.Core.Combat.CombatStateTracker`** -- tracks changes, fires `CombatStateChanged` event
- **`MegaCrit.Sts2.Core.GameActions.PlayCardAction`** -- action for playing a card
  - Constructor: `PlayCardAction(CardModel cardModel, Creature? target)`
  - Properties: `Player`, `CardModelId`, `TargetId`
- **`MegaCrit.Sts2.Core.Rooms.CombatRoom`**
  - `Enter(IRunState?, bool)` -- enters combat room
  - `Exit(IRunState?)` -- exits combat room
  - `OnCombatEnded()` -- callback after combat

**Hooks (recommended):**
- `Hook.BeforeCombatStart(IRunState, CombatState?)` -- combat starting
- `Hook.AfterCombatEnd(IRunState, CombatState?, CombatRoom)` -- combat ended
- `Hook.AfterCombatVictory(IRunState, CombatState?, CombatRoom)` -- victory only
- `Hook.AfterPlayerTurnStart(CombatState, PlayerChoiceContext, Player)` -- turn started
- `Hook.BeforeCardPlayed(CombatState, CardPlay)` -- card about to be played
- `Hook.AfterCardPlayed(CombatState, PlayerChoiceContext, CardPlay)` -- card played
- `Hook.AfterCardDrawn(CombatState, PlayerChoiceContext, CardModel, bool)` -- card drawn
- `Hook.AfterCardDiscarded(CombatState, PlayerChoiceContext, CardModel)` -- card discarded
- `Hook.AfterCardExhausted(CombatState, PlayerChoiceContext, CardModel, bool)` -- card exhausted
- `Hook.BeforeTurnEnd(CombatState, CombatSide)` -- turn ending
- `Hook.AfterTurnEnd(CombatState, CombatSide)` -- turn ended

**Best patch targets:**
- `CombatManager.SetUpCombat` (Postfix) -- combat starts
- `CombatManager.EndCombatInternal` (Postfix) -- combat ends
- `Hook.AfterPlayerTurnStart` (Postfix) -- turn starts (gets Player and CombatState)
- `Hook.AfterCardPlayed` (Postfix) -- card played (gets CardPlay with full info)

---

### 3. Deck / Relics / Run

**Guessed:** `DeckManager.AddCard` / `DeckManager.RemoveCard` / `RelicManager.ObtainRelic` / `RunManager.StartRun` / `RunManager.EndRun`

**Actual:**
- **`MegaCrit.Sts2.Core.Commands.CardPileCmd`** (static)
  - `Add(CardModel card, PileType, ...)` -- add card to pile (deck/hand/draw/etc)
  - `RemoveFromDeck(CardModel card, ...)` -- remove card from deck
  - `Draw(PlayerChoiceContext, Player)` -- draw a card
  - `Shuffle(PlayerChoiceContext, Player)` -- shuffle
- **`MegaCrit.Sts2.Core.Commands.RelicCmd`** (static)
  - `Obtain(RelicModel relic, Player player, ...)` -- obtain relic
  - `Obtain<T>(Player player)` -- obtain relic by type
  - `Remove(RelicModel relic)` -- remove relic
  - `Replace(RelicModel original, RelicModel replace)` -- swap relic
- **`MegaCrit.Sts2.Core.Entities.Players.Player`**
  - `AddRelicInternal(RelicModel relic, ...)` -- internal relic add
  - `RemoveRelicInternal(RelicModel relic, ...)` -- internal relic remove
  - `AddPotionInternal(PotionModel potion, ...)` -- internal potion add
  - `DiscardPotionInternal(PotionModel potion, ...)` -- internal potion discard
  - Properties: `Deck` (CardPile), `Relics` (IReadOnlyList), `PotionSlots`, `Gold`, `MaxEnergy`
  - Events: `RelicObtained`, `RelicRemoved`, `PotionProcured`, `PotionDiscarded`
- **`MegaCrit.Sts2.Core.Runs.RunManager`** (Singleton: `RunManager.Instance`)
  - `Launch()` -- returns RunState, starts the run
  - `SetUpNewSinglePlayer(RunState, bool, ...)` -- configures new run
  - `OnEnded(bool isVictory)` -- run ended
  - `Abandon()` -- player abandons run
  - `CleanUp(bool graceful)` -- cleanup
  - `EnterMapCoord(MapCoord coord)` -- travel to map point
  - `EnterMapCoordInternal(MapCoord, AbstractRoom?, bool)` -- internal travel
  - `EnterRoom(AbstractRoom room)` -- enter a room
  - `GenerateMap()` -- generate act map
  - Event: `RunStarted`
- **`MegaCrit.Sts2.Core.Runs.RunState`**
  - `CurrentActIndex`, `ActFloor`, `TotalFloor`, `CurrentRoom`, `CurrentMapPoint`
  - `CurrentLocation` (RunLocation), `MapPointHistory`
  - `Players`, `Acts`, `Map`

**Hooks (recommended):**
- `Hook.AfterCardChangedPiles(IRunState, CombatState?, CardModel, PileType, AbstractModel?)` -- card moved
- `Hook.BeforeCardRemoved(IRunState, CardModel)` -- card about to be removed
- `Hook.BeforeRoomEntered(IRunState, AbstractRoom)` -- entering a room
- `Hook.AfterRoomEntered(IRunState, AbstractRoom)` -- room entered
- `Hook.AfterGoldGained(IRunState, Player)` -- gold changed

**Best patch targets:**
- `CardPileCmd.Add` (Postfix) -- card added to deck
- `CardPileCmd.RemoveFromDeck` (Postfix) -- card removed from deck
- `RelicCmd.Obtain` (Postfix) -- relic obtained
- `RunManager.Launch` (Postfix) -- run starts
- `RunManager.OnEnded` (Postfix) -- run ends

---

### 4. Map / Navigation

**Guessed:** `MapManager.TravelToNode`

**Actual:**
- **`MegaCrit.Sts2.Core.Runs.RunManager`**
  - `EnterMapCoord(MapCoord coord)` -- travel to map coordinate
  - `EnterMapCoordInternal(MapCoord, AbstractRoom?, bool)` -- internal
  - `EnterMapPointInternal(int actFloor, MapPointType, MapCoord?, AbstractRoom?, bool)` -- internal
- **`MegaCrit.Sts2.Core.GameActions.MoveToMapCoordAction`**
  - Constructor: `MoveToMapCoordAction(Player, MapCoord destination)`
  - `ExecuteAction()` -- performs the move
- **`MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen`** (Singleton: `NMapScreen.Instance`)
  - `OnMapPointSelectedLocally(NMapPoint point)` -- user clicks map point
  - `SetMap(ActMap, uint, bool)` -- loads map data
  - `SetTravelEnabled(bool)` -- enable/disable travel
- **`MegaCrit.Sts2.Core.Map.MapPoint`** -- map data model
- **Enums:**
  - `MapPointType`: Unassigned, Unknown, Shop, Treasure, RestSite, Monster, Elite, Boss, Ancient
  - `RoomType`: Unassigned, Monster, Elite, Boss, Treasure, Shop, Event, RestSite, Map

**Best patch targets:**
- `RunManager.EnterMapCoord` (Postfix) -- map travel
- `MoveToMapCoordAction.ExecuteAction` (Postfix) -- map movement action
- `Hook.AfterRoomEntered` -- entering any room (most versatile)

---

### 5. Shop / Merchant

**Guessed:** `ShopScreen.OnEnter()` / `ShopScreen.OnExit()`

**Actual:**
- **`MegaCrit.Sts2.Core.Rooms.MerchantRoom`** (extends `AbstractRoom`)
  - `Enter(IRunState?, bool)` -- enters shop room
  - `Exit(IRunState?)` -- exits shop room
- **`MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory`**
  - `Initialize(MerchantInventory, MerchantDialogueSet)` -- sets up UI
  - `Open()` -- opens shop UI
  - `Close()` -- closes shop UI (private)
  - `OnPurchaseCompleted(PurchaseStatus, MerchantEntry)` -- item purchased
- **`MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory`** -- data model
  - `MerchantCardEntry`, `MerchantRelicEntry`, `MerchantPotionEntry` -- item types
- **`MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry`** -- base entry

**Hooks:**
- `Hook.AfterItemPurchased(IRunState, Player, MerchantEntry, int goldSpent)` -- after purchase

**Best patch targets:**
- `MerchantRoom.Enter` (Postfix) -- shop room entered
- `MerchantRoom.Exit` (Postfix) -- shop room exited
- `NMerchantInventory.Open` (Postfix) -- shop UI opened
- `Hook.AfterItemPurchased` (Postfix) -- item purchased

---

### 6. Events

**Guessed:** `EventScreen.ShowEvent()` / `EventScreen.OnChoiceSelected()`

**Actual:**
- **`MegaCrit.Sts2.Core.Models.EventModel`** (abstract)
  - `BeginEvent(Player player, bool isPreFinished)` -- starts event
  - `GenerateInitialOptions()` -- generates options (abstract)
  - `CurrentOptions` -- current available options
  - `IsFinished` -- event completed
  - Event: `StateChanged` (fires when options/state change)
- **`MegaCrit.Sts2.Core.Events.EventOption`** -- option data model
- **`MegaCrit.Sts2.Core.Rooms.EventRoom`** (extends `AbstractRoom`)
  - `Enter(IRunState?, bool)` -- enters event room
  - `Exit(IRunState?)` -- exits event room
  - `OnEventStateChanged(EventModel)` -- state change callback
- **`MegaCrit.Sts2.Core.Nodes.Events.NEventLayout`**
  - `SetEvent(EventModel)` -- sets the event model
  - `AddOptions(IEnumerable<EventOption>)` -- adds option buttons
  - `DisableEventOptions()` -- disables all options
- **`MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton`**
  - `OnRelease()` -- button clicked (choice made)
  - `FlashConfirmation()` -- visual feedback
  - Properties: `Event`, `Option`

**Best patch targets:**
- `EventModel.BeginEvent` (Postfix) -- event started
- `EventRoom.Enter` (Postfix) -- event room entered
- `NEventOptionButton.OnRelease` (Postfix) -- choice made (access `.Event` and `.Option`)

---

### 7. Rest Sites

**Guessed:** `RestScreen.OnEnter()` / `RestScreen.OnOptionSelected()`

**Actual:**
- **`MegaCrit.Sts2.Core.Rooms.RestSiteRoom`** (extends `AbstractRoom`)
  - `Enter(IRunState?, bool)` -- enters rest site
  - `Exit(IRunState?)` -- exits rest site
- **`MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption`** (abstract)
  - `OnSelect()` -- async, executes the option (returns bool)
  - `DoLocalPostSelectVfx()` / `DoRemotePostSelectVfx()` -- visual feedback
  - Properties: `OptionId`, `IsEnabled`, `Title`, `Description`
  - Subclasses: `HealRestSiteOption`, `SmithRestSiteOption`, `DigRestSiteOption`, `CookRestSiteOption`, `LiftRestSiteOption`, `MendRestSiteOption`, `CloneRestSiteOption`, `HatchRestSiteOption`
  - `static Generate(Player)` -- generates available options
- **`MegaCrit.Sts2.Core.Nodes.RestSite.NRestSiteButton`**
  - `SelectOption(RestSiteOption option)` -- async, selects option
  - `OnRelease()` -- button clicked

**Hooks:**
- `Hook.AfterRestSiteHeal(IRunState, Player, bool)` -- healed at rest
- `Hook.AfterRestSiteSmith(IRunState, Player)` -- smithed at rest

**Best patch targets:**
- `RestSiteRoom.Enter` (Postfix) -- rest site entered
- `RestSiteOption.OnSelect` (Postfix) -- option selected (access OptionId)
- `NRestSiteButton.SelectOption` (Postfix) -- button clicked

---

### 8. Potions

**Guessed:** `PotionManager.UsePotion()` / `PotionManager.ObtainPotion()`

**Actual:**
- **`MegaCrit.Sts2.Core.GameActions.UsePotionAction`** (extends `GameAction`)
  - Constructor: `UsePotionAction(PotionModel potion, Creature? target, bool isCombatInProgress)`
  - Properties: `Player`, `PotionIndex`, `TargetId`
- **`MegaCrit.Sts2.Core.Commands.PotionCmd`** (static)
  - `TryToProcure(PotionModel, Player, int)` -- obtain potion
  - `TryToProcure<T>(Player)` -- obtain potion by type
  - `Discard(PotionModel)` -- discard potion
- **`MegaCrit.Sts2.Core.Entities.Players.Player`**
  - `AddPotionInternal(PotionModel, int, bool)` -- internal add
  - `DiscardPotionInternal(PotionModel, bool)` -- internal discard
  - `RemoveUsedPotionInternal(PotionModel)` -- remove after use
  - Events: `PotionProcured`, `PotionDiscarded`, `UsedPotionRemoved`

**Hooks:**
- `Hook.BeforePotionUsed(IRunState, CombatState?, PotionModel, Creature?)` -- before use
- `Hook.AfterPotionUsed(IRunState, CombatState?, PotionModel, Creature?)` -- after use
- `Hook.AfterPotionProcured(IRunState, CombatState?, PotionModel)` -- obtained
- `Hook.AfterPotionDiscarded(IRunState, CombatState?, PotionModel)` -- discarded

**Best patch targets:**
- `Hook.AfterPotionUsed` (Postfix) -- potion used
- `Hook.AfterPotionProcured` (Postfix) -- potion obtained
- `PotionCmd.TryToProcure` (Postfix) -- potion obtained (via command)

---

## Key Singletons

| Class | Access |
|---|---|
| `CombatManager` | `CombatManager.Instance` |
| `RunManager` | `RunManager.Instance` |
| `NMapScreen` | `NMapScreen.Instance` |
| `NRun` | `NRun.Instance` |
| `NCombatRoom` | `NCombatRoom.Instance` |

## Key Enums

```csharp
// MegaCrit.Sts2.Core.Rooms.RoomType
enum RoomType { Unassigned, Monster, Elite, Boss, Treasure, Shop, Event, RestSite, Map }

// MegaCrit.Sts2.Core.Map.MapPointType
enum MapPointType { Unassigned, Unknown, Shop, Treasure, RestSite, Monster, Elite, Boss, Ancient }

// MegaCrit.Sts2.Core.Entities.Cards.PileType
enum PileType { None, Draw, Hand, Discard, Exhaust, Play, Deck }

// MegaCrit.Sts2.Core.Combat.CombatSide
enum CombatSide { ... } // Player vs Enemy
```

## Characters

```csharp
// MegaCrit.Sts2.Core.Models.Characters.*
Ironclad, Silent, Defect, Regent, Necrobinder, Deprived, RandomCharacter
```
Note: **Deprived** is a 6th character not in our current data.

## Hooks System

The `Hook` class (`MegaCrit.Sts2.Core.Hooks.Hook`) is the BEST approach for most patches.
It provides ~90 static async methods that fire at well-defined game events.
All hooks receive strongly-typed parameters (RunState, CombatState, Player, etc.).

### Already Subscribed Hooks (11 in HookSubscriptions.cs)

| Hook | Parameters | Use Case |
|---|---|---|
| `AfterPlayerTurnStart` | CombatState, PlayerChoiceContext, Player | Turn started — extract hand, piles, monsters |
| `BeforeCombatStart` | IRunState, CombatState? | Combat starting — init combat state |
| `AfterCombatEnd` | IRunState, CombatState?, CombatRoom | Combat ended — record result |
| `AfterMapGenerated` | IRunState, ActMap, int actIndex | Map generated — extract map nodes |
| `AfterCardPlayed` | CombatState, PlayerChoiceContext, CardPlay | Card played — emit event, refresh state |
| `AfterAttack` | CombatState, AttackCommand | Attack resolved — refresh HP post-damage |
| `AfterPotionUsed` | IRunState, CombatState?, PotionModel, Creature? | Potion used — emit event, update inventory |
| `AfterPotionProcured` | IRunState, CombatState?, PotionModel | Potion obtained — update inventory |
| `AfterRoomEntered` | IRunState, AbstractRoom | Room entered — update floor, screen, detect Shop/Rest rooms |
| `AfterCardChangedPiles` | IRunState, CombatState?, CardModel, PileType, AbstractModel? | Card moved piles — deck add (NEW Session 20) |
| `BeforeCardRemoved` | IRunState, CardModel | Card about to be removed from deck (NEW Session 20) |

### Available but Not Used (notable)

| Hook | Parameters | Use Case |
|---|---|---|
| `AfterCombatVictory` | IRunState, CombatState?, CombatRoom | Victory only (we use AfterCombatEnd instead) |
| `AfterCurrentHpChanged` | IRunState, CombatState?, Creature, decimal | HP change tracking |
| `AfterDamageGiven` | IRunState, CombatState?, Creature, Creature, decimal | Outgoing damage tracking |
| `AfterDamageReceived` | IRunState, CombatState?, Creature, decimal | Incoming damage tracking |
| `AfterCardDrawn` | CombatState, PlayerChoiceContext, CardModel, bool | Card drawn to hand |
| `AfterCardDiscarded` | CombatState, PlayerChoiceContext, CardModel | Card discarded |
| `AfterCardExhausted` | CombatState, PlayerChoiceContext, CardModel, bool | Card exhausted |
| `AfterItemPurchased` | IRunState, Player, MerchantEntry, int goldSpent | Shop purchases |
| `AfterRestSiteHeal` | IRunState, Player, bool | Healed at rest site |
| `AfterRestSiteSmith` | IRunState, Player | Smithed at rest site |
| `BeforeRewardsOffered` | IRunState, Player, IReadOnlyList\<Reward\> | Before rewards shown |
| `AfterRewardTaken` | IRunState, Player, Reward | After any reward taken |
| `AfterOrbChanneled` | IRunState, CombatState?, OrbModel | Defect orb channeled |
| `AfterOrbEvoked` | IRunState, CombatState?, OrbModel | Defect orb evoked |
| `BeforeCardPlayed` | CombatState, CardPlay | Card about to be played |
| `BeforeRoomEntered` | IRunState, AbstractRoom | Before entering any room |
| `BeforeTurnEnd` | CombatState, CombatSide | Turn ending |
| `AfterTurnEnd` | CombatState, CombatSide | Turn ended |
| `AfterGoldGained` | IRunState, Player | Gold changed |
| `AfterPotionDiscarded` | IRunState, CombatState?, PotionModel | Potion discarded |

### No Hook Available (requires Harmony)

These game events have no corresponding Hook method and require direct Harmony patches:

| Target | Method | Patch File |
|---|---|---|
| `RelicCmd.Obtain` | Relic obtained | DeckPatch.cs |
| `RunManager.Launch` | Run start | DeckPatch.cs |
| `RunManager.OnEnded` | Run end | DeckPatch.cs |
| `MerchantRoom.Exit` | Shop exit | ShopPatch.cs |
| `EventModel.BeginEvent` | Event start | EventPatch.cs |
| `NEventOptionButton.OnRelease` | Event choice | EventPatch.cs |
| `NRestSiteButton.SelectOption` | Rest choice | RestPatch.cs |
| `NCardRewardSelectionScreen.ShowScreen` | Card rewards shown | CardRewardPatch.cs |
| `NCardRewardSelectionScreen.SelectCard` | Card picked | CardRewardPatch.cs |

Note: Shop/Rest room *entry* detection is handled by `AfterRoomEntered` (checks `AbstractRoom.RoomType`).
`MerchantRoom.Enter` and `RestSiteRoom.Enter` patches were removed in Session 20.

## Architecture Notes

1. **No "Manager" classes for deck/relics/potions** -- STS2 uses static `Cmd` classes instead:
   - `CardPileCmd` for deck operations
   - `RelicCmd` for relic operations
   - `PotionCmd` for potion operations
   - `PlayerCmd` for energy/gold/stars

2. **GameAction pattern** -- Player actions (play card, use potion, move on map) are wrapped in `GameAction` subclasses that go through an `ActionExecutor`.

3. **Hook system** -- The `Hook` class provides the cleanest integration points. Consider subscribing to hooks instead of patching individual methods.

4. **Room lifecycle** -- All rooms extend `AbstractRoom` with `Enter()`, `Exit()`, `Resume()`. The `RunManager` manages room transitions.

5. **Event-driven architecture** -- Many classes expose C# events (e.g., `Player.RelicObtained`, `CombatManager.TurnStarted`, `EventModel.StateChanged`).

6. **Godot Nodes** -- UI classes prefixed with `N` (NMapScreen, NCombatRoom, etc.) are Godot `Node`/`Control` subclasses.
