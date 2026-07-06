# Abstract Base Classes

Empire provides three abstract base classes that submods extend for custom procedural logic: policy behaviors, building comps, and military job handlers. Each is instantiated automatically by the base mod when the corresponding def specifies the class.

---

## FCPolicyBehavior

**Purpose**: Add procedural logic to a policy — ticking, stat modification, lifecycle hooks, UI actions, abilities.
**Instantiation**: Created per-policy when the `FCPolicyDef` has a `FCPolicyBehaviorExtension` in its `modExtensions`. Automatically saved/loaded via `Scribe_Deep`.

**Class**: `FactionColonies.FCPolicyBehavior` (implements `IExposable`)

### How to Use

1. Create a class extending `FCPolicyBehaviorExtension` with your XML-configurable parameters
2. Create a class extending `FCPolicyBehavior` with your runtime logic
3. Add the extension to your `FCPolicyDef`'s `modExtensions`:

```xml
<FactionColonies.FCPolicyDef>
    <defName>MyPolicy</defName>
    <modExtensions>
        <li Class="YourNamespace.MyPolicyBehaviorExtension">
            <behaviorClass>YourNamespace.MyPolicyBehavior</behaviorClass>
            <myParam>42</myParam>
        </li>
    </modExtensions>
    <!-- ... -->
</FactionColonies.FCPolicyDef>
```

Your behavior instance has access to:
- `this.policy` — the `FCPolicy` wrapper (holds `policy.def`, `policy.timeEnacted`)
- `this.extension` — the `FCPolicyBehaviorExtension` with XML parameters
- `Ext<T>()` — typed access to your extension subclass (e.g. `Ext<MyPolicyBehaviorExtension>().myParam`)

Override `PostInitialize()` to wire up `[Unsaved]` fields from extension parameters (called on both creation and load).

### Virtual Methods

#### Lifecycle

| Method | Signature | Description |
|--------|-----------|-------------|
| `OnEnacted` | `void OnEnacted(FactionFC faction)` | Called when the policy is enacted. |
| `OnRemoved` | `void OnRemoved(FactionFC faction)` | Called when the policy is removed. |
| `Tick` | `void Tick(FactionFC faction)` | Called every game tick while the policy is active. |

**A Note on Ticks**: Convention is to gate your tick code behind a modulus check so the code only runs every *n* ticks, instead of every tick. Only run code on every tick if you really, *really* have to (which is usually never).

#### Stat Modification

| Method | Signature | Description |
|--------|-----------|-------------|
| `ModifyStat` | `double ModifyStat(FCStatDef stat, double currentValue, WorldSettlementFC settlement)` | Modify a stat value at runtime. Called after all cached modifiers. Return `currentValue` for no effect. Respects the stat's aggregation type — for Additive stats, add to `currentValue`; for Multiplicative, multiply it. |
| `GetStatDescription` | `string GetStatDescription(FCStatDef stat, WorldSettlementFC settlement)` | Return tooltip text for your stat contribution. Return `null` for no tooltip. |
| `ModifyBuildingUpkeep` | `double ModifyBuildingUpkeep(BuildingFCDef building, double currentUpkeep, WorldSettlementFC settlement)` | Modify a building's upkeep cost. Return `currentUpkeep` for no change. |

#### Settlement Events

| Method | Signature | Description |
|--------|-----------|-------------|
| `OnSettlementCreated` | `void OnSettlementCreated(FactionFC faction, WorldSettlementFC settlement)` | A new settlement was created. |
| `OnSettlementRemoved` | `void OnSettlementRemoved(FactionFC faction, WorldSettlementFC settlement)` | A settlement was removed. |
| `OnSettlementCostPaid` | `void OnSettlementCostPaid(FactionFC faction)` | Silver cost for a new settlement was paid. |
| `OnSettlementUpgraded` | `void OnSettlementUpgraded(FactionFC faction, WorldSettlementFC settlement, int newLevel)` | A settlement leveled up. |
| `OnSettlementTypeChanged` | `void OnSettlementTypeChanged(FactionFC faction, WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef)` | A settlement changed type. |

#### Building Events

| Method | Signature | Description |
|--------|-----------|-------------|
| `OnBuildingConstructed` | `void OnBuildingConstructed(FactionFC faction, WorldSettlementFC settlement, BuildingFCDef building, int slot)` | A building was constructed. |
| `OnBuildingDeconstructed` | `void OnBuildingDeconstructed(FactionFC faction, WorldSettlementFC settlement, BuildingFCDef building, int slot)` | A building was deconstructed. |

#### Military Events

| Method | Signature | Description |
|--------|-----------|-------------|
| `OnSquadDeployed` | `void OnSquadDeployed(FactionFC faction, MilitaryOperation op, WorldSettlementFC settlement, bool isExtraSquad)` | A squad was deployed from a settlement. Fires once per home settlement involved in the op (foreign-defender ops fire twice). Compare `settlement` to `op.aggressor.homeSettlement` / `op.defender.homeSettlement` if side matters. |
| `OnSquadRecalled` | `void OnSquadRecalled(FactionFC faction, MilitaryOperation op, WorldSettlementFC settlement)` | A squad was recalled. Symmetric with `OnSquadDeployed`. |
| `OnBattleResolved` | `void OnBattleResolved(FactionFC faction, WorldSettlementFC settlement, MilitaryJobDef job, bool victory, BattleResult result)` | A battle was resolved. |

#### Other Events

| Method | Signature | Description |
|--------|-----------|-------------|
| `OnResearchCompleted` | `void OnResearchCompleted(FactionFC faction, ResearchProjectDef project)` | A research project was completed. |
| `OnTaxCollected` | `void OnTaxCollected(FactionFC faction, WorldSettlementFC settlement)` | Taxes were collected from a settlement. |
| `ShouldRerollEvent` | `bool ShouldRerollEvent(FCEventDef eventDef)` | Return `true` to request that this random event be rerolled (picked again). Default: `false`. |

#### Diplomacy

| Method | Signature | Return | Description |
|--------|-----------|--------|-------------|
| `HandleDiplomaticEnvoy` | `bool HandleDiplomaticEnvoy(FactionFC faction, Faction targetFaction)` | `false` | Return `true` to handle the diplomatic envoy (prevents default behavior). |

#### UI

| Method | Signature | Return | Description |
|--------|-----------|--------|-------------|
| `GetMainTabActionButtons` | `IEnumerable<(TaggedString label, Action onClick)> GetMainTabActionButtons(FactionFC faction)` | `null` | Return action buttons for the main Empire tab. |
| `GetSettlementActions` | `IEnumerable<FloatMenuOption> GetSettlementActions(FactionFC faction, WorldSettlementFC settlement)` | `null` | Return float menu options per settlement. |
| `GetExtraDeploymentOptions` | `IEnumerable<FloatMenuOption> GetExtraDeploymentOptions(FactionFC faction, WorldSettlementFC settlement, WorldObjectComp_SettlementMilitary milComp)` | `null` | Return extra squad deployment options. |
| `GetDescription` | `TaggedString GetDescription()` | `TaggedString.Empty` | Return a description for this behavior (shown in policy UI). |

#### Save/Load

Override `ExposeData()` to save/load custom state. Uses standard `Scribe_*` methods.

---

## SettlementBuildingComp

**Purpose**: Attach custom per-building behavior — ticking, construction/demolition hooks, gizmos, save/load.
**Instantiation**: Created when a building with a `BuildingFCExtension.compClass` is constructed. Managed by `WorldObjectComp_SettlementBuildings`.

**Class**: `FactionColonies.SettlementBuildingComp`

### How to Use

1. Create a class extending `SettlementBuildingComp`
2. Override the virtual methods you need
3. Set `compClass` on your building's `BuildingFCExtension`:

```xml
<FactionColonies.BuildingFCDef>
    <defName>MyBuilding</defName>
    <modExtensions>
        <li Class="FactionColonies.BuildingFCExtension">
            <compClass>YourNamespace.MyBuildingComp</compClass>
        </li>
    </modExtensions>
    <!-- ... -->
</FactionColonies.BuildingFCDef>
```

### Available Properties

| Property | Type | Description |
|----------|------|-------------|
| `settlement` | `WorldSettlementFC` | The parent settlement. |
| `buildingSlots` | `List<int>` | Which building slots this comp occupies. |
| `CanDestroy` | `bool` | Read-only; `true` when the comp occupies no building slots (`buildingSlots.Count == 0`). |
| `parentComp` | `WorldObjectComp_SettlementBuildings` | Read-only; the parent buildings comp (resolved from the settlement). |

### Virtual Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `Tick` | `void Tick()` | Called every game tick while the building exists. |
| `OnConstruct` | `void OnConstruct(int buildingSlot)` | Called when the building finishes construction. |
| `OnDeconstruct` | `void OnDeconstruct(int buildingSlot)` | Called when the building is demolished. |
| `GetGizmos` | `IEnumerable<Gizmo> GetGizmos()` | Return gizmos to display when the settlement is selected. |
| `ExposeData` | `void ExposeData()` | Save/load custom state via `Scribe_*` methods. |

### Helper Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `RefreshBuildingSlots` | `void RefreshBuildingSlots()` | Recalculates which slots this comp occupies. |

**Base mod example**: `SettlementBuildingComp_Shuttles` — manages shuttle uses per building, refreshes on construction.

---

## MilitaryJobHandler

**Purpose**: Implement the behavior for a custom military operation.
**Instantiation**: Created once per `MilitaryJobDef` when `handlerClass` is set. Cached by the def.

**Class**: `FactionColonies.MilitaryJobHandler`

### How to Use

1. Create a class extending `MilitaryJobHandler`
2. Implement the abstract methods
3. Set `handlerClass` on your `MilitaryJobDef`:

```xml
<FactionColonies.MilitaryJobDef>
    <defName>MyMilitaryJob</defName>
    <handlerClass>YourNamespace.MyJobHandler</handlerClass>
    <!-- ... -->
</FactionColonies.MilitaryJobDef>
```

### Virtual Methods

| Method | Signature | Default | Description |
|--------|-----------|---------|-------------|
| `OnAutoResolve` | `void OnAutoResolve(MilitaryOperation op)` | calls `op.BeginAutoResolveProgress()` | Kick off auto-resolution. The default drives the per-round engine: one round per in-game hour via `SimulateBattleFc.ResolveOneRound`, ending in `op.CompleteBattle`. Override to short-circuit with an instant result (compute a `BattleResult` and call `op.CompleteBattle(result)` directly). |
| `OnOpCreated` | `void OnOpCreated(MilitaryOperation op)` | no-op | Called immediately after `MilitaryOperationManager` registers the op. Use it to schedule the arrival event (`op.ScheduleEvent(...)`) and send the player a "we're sending forces" letter. |
| `ApplyResult` | `void ApplyResult(MilitaryOperation op, BattleResult result)` | no-op | Side effects after a battle resolves: loot, prisoners, settlement capture, faction XP, delivery events. Called by `op.CompleteBattle(result)` regardless of whether the battle was auto-resolved or manually played, so the same outcome handling applies to both. |
| `ResolvesManually` | `bool ResolvesManually(MilitaryOperation op)` | `false` | Return true to delegate resolution to `OnManualResolve` instead of `OnAutoResolve`. The handler is then responsible for calling `op.CompleteBattle(result)` when the player-driven battle resolves. |
| `OnManualResolve` | `void OnManualResolve(MilitaryOperation op)` | no-op | Spawn pawns / lords on the op's `BattlefieldContext` (typically via `op.AttachToBattlefield()` and `bf.SpawnParticipantOnMap(op, ParticipantSide.X)`). Submods own when `op.CompleteBattle` fires. |
| `IsValidTarget` | `bool IsValidTarget(Faction targetFaction)` | `true` | Return false to exclude a faction from valid targets for this job. Used to filter hostile menu options. |

**Base mod examples**: `MilitaryJobHandler_Raid` (loot + prisoners), `MilitaryJobHandler_Capture` (converts settlement), `MilitaryJobHandler_Enslave` (1-3 prisoners), `MilitaryJobHandler_Raze` (destroys settlement), `MilitaryJobHandler_Defend` (settlement defense, routes through `BattlefieldContext.StartDefense`).

### MilitaryJobHandler_Offensive

Offensive operations that can be played out as a manual battle (Raid, Capture, Enslave, Raze) extend `MilitaryJobHandler_Offensive` rather than `MilitaryJobHandler` directly. It adds the manual-battle seam: when the target resolves to a map-gennable enemy settlement, `ResolvesManually` returns true and `OnManualResolve` delegates to the tile's `BattlefieldContext.StartOffense` (which re-checks the manual-offense setting and the concurrent-map cap, falling back to auto-resolve when appropriate). Subclasses keep their own `ApplyResult` for loot/capture/enslave. Handlers that extend the raw `MilitaryJobHandler` stay auto-resolve only.

---

## Lifecycle listeners (no base class)

The previous single `ILifecycleParticipant` + `LifecycleParticipantBase` pair was split into four domain-specific interfaces (`ISettlementListener`, `IMilitaryOperationListener`, `IMercenarySquadListener`, `IResearchListener`) and the base class was removed. Implementers declare only the interfaces they actually care about and stub any unused methods inline — no inheritance required. See [Lifecycle listener interfaces](interfaces-and-registries.md#lifecycle-listener-interfaces) for the full contracts and registration instructions.
