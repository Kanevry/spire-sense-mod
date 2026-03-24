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
It provides **144 static methods** (78 async event hooks + 66 synchronous modifier/query hooks).
All hooks receive strongly-typed parameters (RunState, CombatState, Player, etc.).
Decompiled from sts2.dll on 2026-03-24.

### Complete Async Event Hooks (78 total)

All `public static async Task` methods on `Hook`. Grouped by category.

#### Already Subscribed Hooks (11 in HookSubscriptions.cs)

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
| `AfterDamageGiven` | PlayerChoiceContext, CombatState, Creature?, DamageResult, ValueProp, Creature, CardModel? | Outgoing damage tracking |
| `BeforeDamageReceived` | PlayerChoiceContext, IRunState, CombatState?, Creature, decimal, ValueProp, Creature?, CardModel? | Before incoming damage |
| `AfterDamageReceived` | PlayerChoiceContext, IRunState, CombatState?, Creature, DamageResult, ValueProp, Creature?, CardModel? | After incoming damage |
| `AfterCardDrawn` | CombatState, PlayerChoiceContext, CardModel, bool | Card drawn to hand |
| `AfterCardDiscarded` | CombatState, PlayerChoiceContext, CardModel | Card discarded |
| `AfterCardExhausted` | CombatState, PlayerChoiceContext, CardModel, bool | Card exhausted |
| `AfterCardRetained` | CombatState, CardModel | Card retained end of turn |
| `AfterCardEnteredCombat` | CombatState, CardModel | Card enters combat piles |
| `AfterCardGeneratedForCombat` | CombatState, CardModel, bool addedByPlayer | Card generated mid-combat |
| `AfterItemPurchased` | IRunState, Player, MerchantEntry, int goldSpent | Shop purchases |
| `AfterRestSiteHeal` | IRunState, Player, bool isMimicked | Healed at rest site |
| `AfterRestSiteSmith` | IRunState, Player | Smithed at rest site |
| `BeforeRewardsOffered` | IRunState, Player, IReadOnlyList\<Reward\> | Before rewards shown |
| `AfterRewardTaken` | IRunState, Player, Reward | After any reward taken |
| `AfterOrbChanneled` | CombatState, PlayerChoiceContext, Player, OrbModel | Defect orb channeled |
| `AfterOrbEvoked` | PlayerChoiceContext, CombatState, OrbModel, IEnumerable\<Creature\> | Defect orb evoked |
| `BeforeCardPlayed` | CombatState, CardPlay | Card about to be played |
| `BeforeCardAutoPlayed` | CombatState, CardModel, Creature?, AutoPlayType | Card auto-played |
| `BeforeRoomEntered` | IRunState, AbstractRoom | Before entering any room |
| `BeforeTurnEnd` | CombatState, CombatSide | Turn ending |
| `AfterTurnEnd` | CombatState, CombatSide | Turn ended |
| `BeforeSideTurnStart` | CombatState, CombatSide | Before side turn start |
| `AfterSideTurnStart` | CombatState, CombatSide | After side turn start |
| `BeforePlayPhaseStart` | CombatState, Player | Before play phase |
| `AfterGoldGained` | IRunState, Player | Gold changed |
| `AfterPotionDiscarded` | IRunState, CombatState?, PotionModel | Potion discarded |
| `AfterActEntered` | IRunState | New act entered |
| `BeforeAttack` | CombatState, AttackCommand | Before attack resolves |
| `AfterBlockBroken` | CombatState, Creature | Block broken to 0 |
| `AfterBlockCleared` | CombatState, Creature | Block cleared |
| `BeforeBlockGained` | CombatState, Creature, decimal, ValueProp, CardModel? | Before gaining block |
| `AfterBlockGained` | CombatState, Creature, decimal, ValueProp, CardModel? | After gaining block |
| `AfterCreatureAddedToCombat` | CombatState, Creature | Creature spawned |
| `BeforeDeath` | IRunState, CombatState?, Creature | Before creature dies |
| `AfterDeath` | IRunState, CombatState?, Creature, bool, float | After creature dies |
| `AfterDiedToDoom` | CombatState, IReadOnlyList\<Creature\> | Died to doom counter |
| `AfterEnergyReset` | CombatState, Player | Energy reset at turn start |
| `AfterEnergySpent` | CombatState, CardModel, int | Energy spent on card |
| `BeforeFlush` | CombatState, Player | Before flush (Regent) |
| `AfterForge` | CombatState, decimal, Player, AbstractModel? | Forge triggered |
| `BeforeHandDraw` | CombatState, Player, PlayerChoiceContext | Before hand draw |
| `AfterHandEmptied` | CombatState, PlayerChoiceContext, Player | Hand emptied |
| `AfterOstyRevived` | CombatState, Creature | Osty revived |
| `BeforePotionUsed` | IRunState, CombatState?, PotionModel, Creature? | Before potion use |
| `BeforePowerAmountChanged` | CombatState, PowerModel, decimal, Creature, Creature?, CardModel? | Before power change |
| `AfterPowerAmountChanged` | CombatState, PowerModel, decimal, Creature?, CardModel? | After power change |
| `AfterPreventingBlockClear` | CombatState, AbstractModel, Creature | Block clear prevented |
| `AfterPreventingDeath` | IRunState, CombatState?, AbstractModel, Creature | Death prevented |
| `AfterPreventingDraw` | CombatState, AbstractModel | Draw prevented |
| `AfterShuffle` | CombatState, PlayerChoiceContext, Player | Deck shuffled |
| `AfterStarsGained` | CombatState, int, Player | Stars gained (Regent) |
| `AfterStarsSpent` | CombatState, int, Player | Stars spent (Regent) |
| `AfterSummon` | CombatState, PlayerChoiceContext, Player, decimal | Summon (Necrobinder) |
| `AfterTakingExtraTurn` | CombatState, Player | Extra turn taken |

### Synchronous Modifier/Query Hooks (66 total)

These hooks modify values or answer queries synchronously (return values, not async):

| Hook | Return Type | Purpose |
|---|---|---|
| `ModifyAttackHitCount` | decimal | Modify number of attack hits |
| `ModifyBlock` | decimal | Modify block amount |
| `ModifyCardBeingAddedToDeck` | CardModel | Transform card before deck add |
| `ModifyCardPlayCount` | int | Modify card play repetitions |
| `ModifyCardPlayResultPileTypeAndPosition` | (PileType, CardPilePosition) | Where card goes after play |
| `ModifyCardRewardAlternatives` | IEnumerable\<AbstractModel\> | Alter card reward alternatives |
| `ModifyCardRewardCreationOptions` | CardCreationOptions | Modify card reward creation |
| `TryModifyCardRewardOptions` | bool | Modify card reward options list |
| `ModifyCardRewardUpgradeOdds` | decimal | Modify upgrade chance |
| `ModifyDamage` | decimal | Modify damage amount |
| `ModifyEnergyCostInCombat` | decimal | Modify card energy cost |
| `ModifyExtraRestSiteHealText` | IReadOnlyList\<LocString\> | Extra heal text |
| `ModifyGeneratedMap` | ActMap | Transform generated map |
| `ModifyHandDraw` | decimal | Modify cards drawn per turn |
| `ModifyHealAmount` | decimal | Modify healing |
| `ModifyHpLostBeforeOsty` | decimal | Modify HP loss (pre-Osty) |
| `ModifyHpLostAfterOsty` | decimal | Modify HP loss (post-Osty) |
| `ModifyMaxEnergy` | decimal | Modify max energy |
| `ModifyMerchantCardCreationResults` | void | Modify shop card options |
| `ModifyMerchantCardPool` | IEnumerable\<CardModel\> | Modify shop card pool |
| `ModifyMerchantCardRarity` | CardRarity | Modify shop card rarity |
| `ModifyMerchantPrice` | decimal | Modify shop prices |
| `ModifyNextEvent` | EventModel | Swap event model |
| `ModifyOddsIncreaseForUnrolledRoomType` | float | Map generation odds |
| `ModifyOrbPassiveTriggerCount` | int | Defect orb triggers |
| `ModifyOrbValue` | decimal | Defect orb value |
| `ModifyPowerAmountGiven` | decimal | Modify power applied |
| `ModifyPowerAmountReceived` | decimal | Modify power received |
| `ModifyRestSiteHealAmount` | decimal | Modify rest heal |
| `ModifyRestSiteOptions` | IEnumerable\<AbstractModel\> | Add/remove rest options |
| `ModifyRestSiteHealRewards` | IEnumerable\<AbstractModel\> | Modify rest heal rewards |
| `ModifyRewards` | IEnumerable\<AbstractModel\> | Modify room rewards |
| `ModifyShuffleOrder` | void | Modify shuffle order |
| `ModifyStarCost` | decimal | Modify star cost |
| `ModifySummonAmount` | decimal | Modify summon amount |
| `ModifyUnblockedDamageTarget` | Creature | Redirect unblocked damage |
| `ModifyUnknownMapPointRoomTypes` | IReadOnlySet\<RoomType\> | Unknown map point types |
| `ModifyXValue` | int | Modify X-cost value |
| `ShouldAddToDeck` | bool | Allow/prevent deck add |
| `ShouldAfflict` | bool | Allow affliction |
| `ShouldAllowAncient` | bool | Allow ancient event |
| `ShouldAllowHitting` | bool | Allow hitting creature |
| `ShouldAllowMerchantCardRemoval` | bool | Allow shop card removal |
| `ShouldAllowSelectingMoreCardRewards` | bool | Allow extra card picks |
| `ShouldAllowTargeting` | bool | Allow targeting creature |
| `ShouldClearBlock` | bool | Allow block clear |
| `ShouldCreatureBeRemovedFromCombatAfterDeath` | bool | Remove dead creature |
| `ShouldDie` | bool | Allow creature death |
| `ShouldDisableRemainingRestSiteOptions` | bool | Disable rest options |
| `ShouldDraw` | bool | Allow card draw |
| `ShouldEtherealTrigger` | bool | Allow ethereal exhaust |
| `ShouldFlush` | bool | Allow flush |
| `ShouldGainGold` | bool | Allow gold gain |
| `ShouldGenerateTreasure` | bool | Allow treasure generation |
| `ShouldGainStars` | bool | Allow star gain |
| `ShouldPayExcessEnergyCostWithStars` | bool | Pay energy with stars |
| `ShouldPlay` | bool | Allow card play |
| `ShouldPlayerResetEnergy` | bool | Allow energy reset |
| `ShouldProceedToNextMapPoint` | bool | Allow map progression |
| `ShouldProcurePotion` | bool | Allow potion procurement |
| `ShouldRefillMerchantEntry` | bool | Allow shop refill |
| `ShouldStopCombatFromEnding` | bool | Prevent combat end |
| `ShouldTakeExtraTurn` | bool | Allow extra turn |
| `ShouldForcePotionReward` | bool | Force potion reward |
| `ShouldPowerBeRemovedOnDeath` | bool | Keep power after death |

### No Hook Available (requires Harmony)

These game events have no corresponding Hook method and require direct Harmony patches:

| Target | Method | Patch File | Migration Notes |
|---|---|---|---|
| `RelicCmd.Obtain` | Relic obtained | DeckPatch.cs | NO HOOK -- no `AfterRelicObtained` exists. Must keep Harmony. |
| `RunManager.Launch` | Run start | DeckPatch.cs | NO HOOK -- no `AfterRunStart`/`BeforeRunStart` exists. Must keep Harmony. |
| `RunManager.OnEnded` | Run end | DeckPatch.cs | NO HOOK -- no `AfterRunEnd` exists. Must keep Harmony. |
| `MerchantRoom.Exit` | Shop exit | ShopPatch.cs | NO HOOK -- no `AfterShopExit` exists. Must keep Harmony. |
| `EventModel.BeginEvent` | Event start | EventPatch.cs | NO HOOK -- no `AfterEventStart` exists. Must keep Harmony. |
| `NEventOptionButton.OnRelease` | Event choice | EventPatch.cs | NO HOOK -- no `AfterEventChoice` exists. Must keep Harmony. |
| `NRestSiteButton.SelectOption` | Rest choice | RestPatch.cs | PARTIAL -- `AfterRestSiteHeal` and `AfterRestSiteSmith` cover heal/smith only. No generic `AfterRestSiteOptionSelected` for Dig/Cook/Lift/Mend/Clone/Hatch. |
| `NCardRewardSelectionScreen.ShowScreen` | Card rewards shown | CardRewardPatch.cs | PARTIAL -- `BeforeRewardsOffered` fires before ALL rewards (not card-specific). Could work if we filter for CardReward type. |
| `NCardRewardSelectionScreen.SelectCard` | Card picked | CardRewardPatch.cs | PARTIAL -- `AfterRewardTaken` fires for any reward. Could work if we filter for card rewards. |

### Migration Analysis (Session 21 — 2026-03-24)

**CAN migrate (with caveats):**

1. **CardRewardPatch.OnCardRewardsShown** -- Use `BeforeRewardsOffered(IRunState, Player, IReadOnlyList<Reward>)`.
   - Fires before rewards are shown. Filter `rewards` list for `CardReward` instances.
   - Caveat: fires for ALL reward types (gold, relic, card, potion). Need type check.
   - Alternative: `TryModifyCardRewardOptions` fires during card reward generation.

2. **CardRewardPatch.OnCardPicked** -- Use `AfterRewardTaken(IRunState, Player, Reward)`.
   - Fires after any reward is taken. Filter for `CardReward` type.
   - Caveat: fires for ALL reward types, not just cards. Need `reward is CardReward` check.
   - Alternative: `AfterCardChangedPiles` (already subscribed) detects deck adds, but misses the "which reward screen" context.

3. **RestPatch (heal/smith only)** -- Use `AfterRestSiteHeal` + `AfterRestSiteSmith`.
   - Only covers Heal and Smith options. Other rest options (Dig, Cook, Lift, Mend, Clone, Hatch) have no specific hook.
   - For our use case (tracking rest choice for game state), this may be sufficient if we only need heal/smith detection.

**CANNOT migrate (no hook exists):**

4. **DeckPatch.OnRelicObtained** -- No `AfterRelicObtained` hook. Must keep `RelicCmd.Obtain` Harmony patch.
   - Searched for: RelicObtained, RelicAdded, RelicGained, AfterRelic -- none found in Hook class.
   - `Player.RelicObtained` is a C# event (not a Hook), unusable via [HarmonyPostfix].

5. **DeckPatch.OnRunStart** -- No `AfterRunStart`/`BeforeRunStart` hook. Must keep `RunManager.Launch` Harmony patch.
   - Searched for: RunStart, RunLaunch, RunBegin, AfterRun -- none found in Hook class.
   - `RunManager.RunStarted` is a C# event, not a Hook.

6. **DeckPatch.OnRunEnd** -- No `AfterRunEnd`/`BeforeRunEnd` hook. Must keep `RunManager.OnEnded` Harmony patch.
   - Searched for: RunEnd, RunComplete, RunFinish -- none found in Hook class.

7. **EventPatch.OnEventStarted** -- No `AfterEventStart` hook. Must keep `EventModel.BeginEvent` Harmony patch.
   - Searched for: EventBegin, EventStart, AfterEvent -- none found in Hook class.
   - `ModifyNextEvent` exists but modifies the event model, does not notify on event start.

8. **EventPatch.OnEventChoiceMade** -- No `AfterEventChoice` hook. Must keep `NEventOptionButton.OnRelease` Harmony patch.
   - Searched for: EventChoice, EventOption, AfterEventOption -- none found in Hook class.

9. **ShopPatch.OnShopExit** -- No `AfterShopExit` hook. Must keep `MerchantRoom.Exit` Harmony patch.
   - `AfterItemPurchased` exists but only fires on purchase, not on exit.

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

---

## Available STS2 Hooks (v0.99.1)

Complete list of Hook methods from `MegaCrit.Sts2.Core.Hooks.Hook`.
Decompiled from sts2.dll on 2026-03-24. 78 async event hooks + 66 synchronous modifier/query hooks = 144 total.

### Currently Used by SpireSense (11):
- `AfterPlayerTurnStart(CombatState, PlayerChoiceContext, Player)`
- `BeforeCombatStart(IRunState, CombatState?)`
- `AfterCombatEnd(IRunState, CombatState?, CombatRoom)`
- `AfterCardPlayed(CombatState, PlayerChoiceContext, CardPlay)`
- `AfterCardChangedPiles(IRunState, CombatState?, CardModel, PileType, AbstractModel?)`
- `BeforeCardRemoved(IRunState, CardModel)`
- `AfterAttack(CombatState, AttackCommand)`
- `AfterPotionUsed(IRunState, CombatState?, PotionModel, Creature?)`
- `AfterPotionProcured(IRunState, CombatState?, PotionModel)`
- `AfterRoomEntered(IRunState, AbstractRoom)`
- `AfterMapGenerated(IRunState, ActMap, int)`

### Migration Candidates (investigated, non-trivial):
- `BeforeRewardsOffered(IRunState, Player, IReadOnlyList<Reward>)` -- could replace CardRewardPatch.OnCardRewardsShown
- `AfterRewardTaken(IRunState, Player, Reward)` -- could replace CardRewardPatch.OnCardPicked
- `AfterRestSiteHeal(IRunState, Player, bool)` -- partial replacement for RestPatch
- `AfterRestSiteSmith(IRunState, Player)` -- partial replacement for RestPatch
- `AfterItemPurchased(IRunState, Player, MerchantEntry, int)` -- supplements ShopPatch

### No Equivalent Hook Available:
- `RelicCmd.Obtain` (DeckPatch.OnRelicObtained) -- no AfterRelicObtained hook
- `RunManager.Launch` (DeckPatch.OnRunStart) -- no AfterRunStart hook
- `RunManager.OnEnded` (DeckPatch.OnRunEnd) -- no AfterRunEnd hook
- `EventModel.BeginEvent` (EventPatch.OnEventStarted) -- no AfterEventStart hook
- `NEventOptionButton.OnRelease` (EventPatch.OnEventChoiceMade) -- no AfterEventChoice hook
- `MerchantRoom.Exit` (ShopPatch.OnShopExit) -- no AfterShopExit hook
- `NRestSiteButton.SelectOption` (RestPatch, non-heal/smith options) -- only AfterRestSiteHeal/Smith exist

### All 78 Async Event Hooks (alphabetical):

```
AfterActEntered(IRunState runState)
AfterAttack(CombatState combatState, AttackCommand command)
AfterBlockBroken(CombatState combatState, Creature creature)
AfterBlockCleared(CombatState combatState, Creature creature)
AfterBlockGained(CombatState combatState, Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
AfterCardChangedPiles(IRunState runState, CombatState? combatState, CardModel card, PileType oldPile, AbstractModel? source)
AfterCardDiscarded(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
AfterCardDrawn(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
AfterCardEnteredCombat(CombatState combatState, CardModel card)
AfterCardExhausted(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
AfterCardGeneratedForCombat(CombatState combatState, CardModel card, bool addedByPlayer)
AfterCardPlayed(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
AfterCardRetained(CombatState combatState, CardModel card)
AfterCombatEnd(IRunState runState, CombatState? combatState, CombatRoom room)
AfterCombatVictory(IRunState runState, CombatState? combatState, CombatRoom room)
AfterCreatureAddedToCombat(CombatState combatState, Creature creature)
AfterCurrentHpChanged(IRunState runState, CombatState? combatState, Creature creature, decimal delta)
AfterDamageGiven(PlayerChoiceContext choiceContext, CombatState combatState, Creature? dealer, DamageResult results, ValueProp props, Creature target, CardModel? cardSource)
AfterDamageReceived(PlayerChoiceContext choiceContext, IRunState runState, CombatState? combatState, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
AfterDeath(IRunState runState, CombatState? combatState, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
AfterDiedToDoom(CombatState combatState, IReadOnlyList<Creature> creatures)
AfterEnergyReset(CombatState combatState, Player player)
AfterEnergySpent(CombatState combatState, CardModel card, int amount)
AfterForge(CombatState combatState, decimal amount, Player forger, AbstractModel? source)
AfterGoldGained(IRunState runState, Player player)
AfterHandEmptied(CombatState combatState, PlayerChoiceContext choiceContext, Player player)
AfterItemPurchased(IRunState runState, Player player, MerchantEntry itemPurchased, int goldSpent)
AfterMapGenerated(IRunState runState, ActMap map, int actIndex)
AfterModifyingBlockAmount(CombatState combatState, decimal modifiedBlock, CardModel? cardSource, CardPlay? cardPlay, IEnumerable<AbstractModel> modifiers)
AfterModifyingCardPlayCount(CombatState combatState, CardModel card, IEnumerable<AbstractModel> modifiers)
AfterModifyingCardRewardOptions(IRunState runState, IEnumerable<AbstractModel> modifiers)
AfterModifyingDamageAmount(IRunState runState, CombatState? combatState, CardModel? cardSource, IEnumerable<AbstractModel> modifiers)
AfterModifyingHandDraw(CombatState combatState, IEnumerable<AbstractModel> modifiers)
AfterModifyingHpLostAfterOsty(IRunState runState, CombatState? combatState, IEnumerable<AbstractModel> modifiers)
AfterModifyingHpLostBeforeOsty(IRunState runState, CombatState? combatState, IEnumerable<AbstractModel> modifiers)
AfterModifyingOrbPassiveTriggerCount(CombatState combatState, OrbModel orb, IEnumerable<AbstractModel> modifiers)
AfterModifyingPowerAmountGiven(CombatState combatState, IEnumerable<AbstractModel> modifiers, PowerModel modifiedPower)
AfterModifyingPowerAmountReceived(CombatState combatState, IEnumerable<AbstractModel> modifiers, PowerModel modifiedPower)
AfterModifyingRewards(IRunState runState, IEnumerable<AbstractModel> modifiers)
AfterOrbChanneled(CombatState combatState, PlayerChoiceContext choiceContext, Player player, OrbModel orb)
AfterOrbEvoked(PlayerChoiceContext choiceContext, CombatState combatState, OrbModel orb, IEnumerable<Creature> targets)
AfterOstyRevived(CombatState combatState, Creature osty)
AfterPlayerTurnStart(CombatState combatState, PlayerChoiceContext choiceContext, Player player)
AfterPotionDiscarded(IRunState runState, CombatState? combatState, PotionModel potion)
AfterPotionProcured(IRunState runState, CombatState? combatState, PotionModel potion)
AfterPotionUsed(IRunState runState, CombatState? combatState, PotionModel potion, Creature? target)
AfterPowerAmountChanged(CombatState combatState, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
AfterPreventingBlockClear(CombatState combatState, AbstractModel preventer, Creature creature)
AfterPreventingDeath(IRunState runState, CombatState? combatState, AbstractModel preventer, Creature creature)
AfterPreventingDraw(CombatState combatState, AbstractModel modifier)
AfterRestSiteHeal(IRunState runState, Player player, bool isMimicked)
AfterRestSiteSmith(IRunState runState, Player player)
AfterRewardTaken(IRunState runState, Player player, Reward reward)
AfterRoomEntered(IRunState runState, AbstractRoom room)
AfterShuffle(CombatState combatState, PlayerChoiceContext choiceContext, Player shuffler)
AfterSideTurnStart(CombatState combatState, CombatSide side)
AfterStarsGained(CombatState combatState, int amount, Player gainer)
AfterStarsSpent(CombatState combatState, int amount, Player spender)
AfterSummon(CombatState combatState, PlayerChoiceContext choiceContext, Player summoner, decimal amount)
AfterTakingExtraTurn(CombatState combatState, Player player)
AfterTurnEnd(CombatState combatState, CombatSide side)
BeforeAttack(CombatState combatState, AttackCommand command)
BeforeBlockGained(CombatState combatState, Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
BeforeCardAutoPlayed(CombatState combatState, CardModel card, Creature? target, AutoPlayType type)
BeforeCardPlayed(CombatState combatState, CardPlay cardPlay)
BeforeCardRemoved(IRunState runState, CardModel card)
BeforeCombatStart(IRunState runState, CombatState? combatState)
BeforeDamageReceived(PlayerChoiceContext choiceContext, IRunState runState, CombatState? combatState, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
BeforeDeath(IRunState runState, CombatState? combatState, Creature creature)
BeforeFlush(CombatState combatState, Player player)
BeforeHandDraw(CombatState combatState, Player player, PlayerChoiceContext playerChoiceContext)
BeforePlayPhaseStart(CombatState combatState, Player player)
BeforePotionUsed(IRunState runState, CombatState? combatState, PotionModel potion, Creature? target)
BeforePowerAmountChanged(CombatState combatState, PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)
BeforeRewardsOffered(IRunState runState, Player player, IReadOnlyList<Reward> rewards)
BeforeRoomEntered(IRunState runState, AbstractRoom room)
BeforeSideTurnStart(CombatState combatState, CombatSide side)
BeforeTurnEnd(CombatState combatState, CombatSide side)
```

### All 66 Synchronous Modifier/Query Hooks (alphabetical):

```
ModifyAttackHitCount(CombatState, AttackCommand, int originalHitCount) -> decimal
ModifyBlock(CombatState, Creature, decimal, ValueProp, CardModel?, CardPlay?, out IEnumerable<AbstractModel>) -> decimal
ModifyCardBeingAddedToDeck(IRunState, CardModel, out List<AbstractModel>) -> CardModel
ModifyCardPlayCount(CombatState, CardModel, int, Creature?, out List<AbstractModel>) -> int
ModifyCardPlayResultPileTypeAndPosition(CombatState, CardModel, bool, ResourceInfo, PileType, CardPilePosition, out IEnumerable<AbstractModel>) -> (PileType, CardPilePosition)
ModifyCardRewardAlternatives(IRunState, Player, CardReward, List<CardRewardAlternative>) -> IEnumerable<AbstractModel>
ModifyCardRewardCreationOptions(IRunState, Player, CardCreationOptions) -> CardCreationOptions
ModifyCardRewardUpgradeOdds(IRunState, Player, CardModel, decimal) -> decimal
ModifyDamage(IRunState, CombatState?, Creature?, Creature?, decimal, ValueProp, CardModel?, ModifyDamageHookType, CardPreviewMode, out IEnumerable<AbstractModel>) -> decimal
ModifyEnergyCostInCombat(CombatState, CardModel, decimal) -> decimal
ModifyExtraRestSiteHealText(IRunState, Player, IReadOnlyList<LocString>) -> IReadOnlyList<LocString>
ModifyGeneratedMap(IRunState, ActMap, int) -> ActMap
ModifyGeneratedMapLate(IRunState, ActMap, int) -> ActMap
ModifyHandDraw(CombatState, Player, decimal, out IEnumerable<AbstractModel>) -> decimal
ModifyHealAmount(IRunState, CombatState?, Creature, decimal) -> decimal
ModifyHpLostAfterOsty(IRunState, CombatState?, Creature, decimal, ValueProp, Creature?, CardModel?, out IEnumerable<AbstractModel>) -> decimal
ModifyHpLostBeforeOsty(IRunState, CombatState?, Creature, decimal, ValueProp, Creature?, CardModel?, out IEnumerable<AbstractModel>) -> decimal
ModifyMaxEnergy(CombatState, Player, decimal) -> decimal
ModifyMerchantCardCreationResults(IRunState, Player, List<CardCreationResult>) -> void
ModifyMerchantCardPool(IRunState, Player, IEnumerable<CardModel>) -> IEnumerable<CardModel>
ModifyMerchantCardRarity(IRunState, Player, CardRarity) -> CardRarity
ModifyMerchantPrice(IRunState, Player, MerchantEntry, decimal) -> decimal
ModifyNextEvent(IRunState, EventModel) -> EventModel
ModifyOddsIncreaseForUnrolledRoomType(IRunState, RoomType, float) -> float
ModifyOrbPassiveTriggerCount(CombatState, OrbModel, int, out List<AbstractModel>) -> int
ModifyOrbValue(CombatState, Player, decimal) -> decimal
ModifyPowerAmountGiven(CombatState, PowerModel, Creature, decimal, Creature?, CardModel?, out IEnumerable<AbstractModel>) -> decimal
ModifyPowerAmountReceived(CombatState, PowerModel, Creature, decimal, Creature?, out IEnumerable<AbstractModel>) -> decimal
ModifyRestSiteHealAmount(IRunState, Creature, decimal) -> decimal
ModifyRestSiteHealRewards(IRunState, Player, List<Reward>, bool) -> IEnumerable<AbstractModel>
ModifyRestSiteOptions(IRunState, Player, ICollection<RestSiteOption>) -> IEnumerable<AbstractModel>
ModifyRewards(IRunState, Player, List<Reward>, AbstractRoom?) -> IEnumerable<AbstractModel>
ModifyShuffleOrder(CombatState, Player, List<CardModel>, bool) -> void
ModifyStarCost(CombatState, CardModel, decimal) -> decimal
ModifySummonAmount(CombatState, Player, decimal, AbstractModel?) -> decimal
ModifyUnblockedDamageTarget(CombatState, Creature, decimal, ValueProp, Creature?) -> Creature
ModifyUnknownMapPointRoomTypes(IRunState, IReadOnlySet<RoomType>) -> IReadOnlySet<RoomType>
ModifyXValue(CombatState, CardModel, int) -> int
ShouldAddToDeck(IRunState, CardModel, out AbstractModel?) -> bool
ShouldAfflict(CombatState, CardModel, AfflictionModel) -> bool
ShouldAllowAncient(IRunState, Player, AncientEventModel) -> bool
ShouldAllowHitting(CombatState, Creature) -> bool
ShouldAllowMerchantCardRemoval(IRunState, Player) -> bool
ShouldAllowSelectingMoreCardRewards(IRunState, Player, CardReward) -> bool
ShouldAllowTargeting(CombatState, Creature, out AbstractModel?) -> bool
ShouldClearBlock(CombatState, Creature, out AbstractModel?) -> bool
ShouldCreatureBeRemovedFromCombatAfterDeath(CombatState, Creature) -> bool
ShouldDie(IRunState, CombatState?, Creature, out AbstractModel?) -> bool
ShouldDisableRemainingRestSiteOptions(IRunState, Player) -> bool
ShouldDraw(CombatState, Player, bool, out AbstractModel?) -> bool
ShouldEtherealTrigger(CombatState, CardModel) -> bool
ShouldFlush(CombatState, Player) -> bool
ShouldForcePotionReward(IRunState, Player, RoomType) -> bool
ShouldGainGold(IRunState, CombatState?, decimal, Player) -> bool
ShouldGainStars(CombatState, decimal, Player) -> bool
ShouldGenerateTreasure(IRunState, Player) -> bool
ShouldPayExcessEnergyCostWithStars(CombatState, Player) -> bool
ShouldPlay(CombatState, CardModel, out AbstractModel?, AutoPlayType) -> bool
ShouldPlayerResetEnergy(CombatState, Player) -> bool
ShouldPowerBeRemovedOnDeath(PowerModel) -> bool
ShouldProceedToNextMapPoint(IRunState) -> bool
ShouldProcurePotion(IRunState, CombatState?, PotionModel, Player) -> bool
ShouldRefillMerchantEntry(IRunState, MerchantEntry, Player) -> bool
ShouldStopCombatFromEnding(CombatState) -> bool
ShouldTakeExtraTurn(CombatState, Player) -> bool
TryModifyCardRewardOptions(IRunState, Player, List<CardCreationResult>, CardCreationOptions, out List<AbstractModel>) -> bool
```
