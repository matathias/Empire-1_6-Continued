# Interfaces & Registries

Empire provides C# interfaces for submod extensibility across several domains — lifecycle, economy/tax, military/battle, raid targeting, roads, and UI. Some use static registries (global hooks); others are discovered on WorldObjectComps (per-settlement hooks) or DefModExtensions. The authoritative, always-current list of registry-probed interfaces is the body of `EmpireRegistry.Register(object)` in `util/Registries/EmpireRegistry.cs`.

All registry-based interfaces follow the same pattern — register an instance, and the base mod invokes it at the appropriate time.

Registries are not serialized. Your mod must re-register on game load.

---

## Registration: `EmpireRegistry`

`EmpireRegistry` is the unified entry point for every registry-based interface (except `MilitaryWindowRegistry`, see below). One `Register(object)` call probes the participant for every supported interface and forwards it to the matching domain registry. Symmetric `Unregister(object)` and `ClearAll()` are provided.

```csharp
public class MyExtension : ISettlementListener, ITaxTickParticipant, ISilverPaymentModifier
{
    // ... interface implementations ...
}

// Anywhere init runs (WorldComponent.FinalizeInit, [StaticConstructorOnStartup], etc.):
EmpireRegistry.Register(new MyExtension());
```

The facade routes that single call to `LifecycleRegistry`, `TaxTickRegistry`, and `SilverPaymentRegistry` simultaneously. There's no need to know which interface belongs to which registry. A participant that matches no registered interface logs `EmpireRegistry.Register: <type> matched no registry; ignored`.

**`EmpireRegistry` is the only public registration path.** The per-domain typed `XxxRegistry.Register(IXxx)` methods that documented this in the past are now `internal` to the base mod — submods cannot reach them. Any registration from a submod must go through `EmpireRegistry.Register(object)`. (The base mod itself and `RegistryTests` still see the typed methods because they share an assembly.)

`EmpireRegistry.ClearAll()` is called from `EmpireCacheUtil.InvalidateAll` (on `Game.Dispose` / `Game.ClearCaches`). Submods needing to re-register after invalidation should hook `EmpireCacheUtil.RegisterCacheInvalidator(key, callback)` and call `EmpireRegistry.Register(...)` from the callback.

**Exception — `MilitaryWindowRegistry`**: its slot-keyed `Register(SlotKey, factory)` API doesn't fit the interface-probe pattern. Its `Register` stays `public` and is called directly. `EmpireRegistry.ClearAll()` does not clear it.

---

## Registry-Based Interfaces

### Lifecycle listener interfaces

**Registry**: `LifecycleRegistry`
**Purpose**: Hook into settlement, military operation, mercenary squad, and research events.

Four domain-specific listener interfaces. Implement only the ones you care about — a single class can implement multiple, and a single `EmpireRegistry.Register(this)` call adds it to every matching dispatch list.

```csharp
public interface ISettlementListener
{
    void OnSettlementCreated(WorldSettlementFC settlement);
    void OnSettlementRemoved(WorldSettlementFC settlement);
    void OnSettlementUpgraded(WorldSettlementFC settlement, int oldLevel, int newLevel);
    void OnSettlementTypeChanged(WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef);
    void OnBuildingConstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot);
    void OnBuildingDeconstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot);
}

public interface IMilitaryOperationListener
{
    void OnOperationCreated(MilitaryOperation op);
    void OnOperationResolved(MilitaryOperation op);
    void OnBattleResolved(MilitaryOperation op, bool victory, BattleResult result);
}

public interface IMercenarySquadListener
{
    void OnMercenaryDeath(MercenaryDeathEvent evt);
    void OnSquadHired(MercenarySquadFC squad);
    void OnSquadDismissed(MercenarySquadFC squad);
    void OnSquadUpgraded(MercenarySquadFC squad);
}

public interface IResearchListener
{
    void OnResearchCompleted(ResearchProjectDef project);
}
```

`OnMercenaryDeath` is a notification fired when a mercenary is killed. There is no built-in auto-replacement to gate — refilling empty slots is a player-driven action via `MercenarySquadFC.FillEmptySlots`.

**Usage**:

```csharp
public class MyLifecycleHook : ISettlementListener, IResearchListener
{
    public void OnSettlementCreated(WorldSettlementFC settlement) { }
    public void OnSettlementRemoved(WorldSettlementFC settlement) { }
    public void OnSettlementUpgraded(WorldSettlementFC settlement, int oldLevel, int newLevel) { }
    public void OnSettlementTypeChanged(WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef) { }

    public void OnBuildingConstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot)
    {
        // Your code here — caches are already dirty
    }
    public void OnBuildingDeconstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot) { }

    public void OnResearchCompleted(ResearchProjectDef project)
    {
        // Your code here
    }
}

// In your mod's static constructor:
EmpireRegistry.Register(new MyLifecycleHook());
```

A class implementing none of the four listener interfaces logs a warning and is ignored.

**Invocation timing**: Each hook fires after the corresponding action completes. For settlement, building, and military-operation events, caches are invalidated between participants so later participants see changes made by earlier ones. Research completion invalidates all-settlement stat caches between participants.

---

### IMercAutoTendProvider

**Registry**: `MercAutoTendRegistry`
**Purpose**: Influence the auto-tending of off-map (injured) mercenary pawns.

```csharp
public interface IMercAutoTendProvider
{
    Pawn ProvideTendingDoctor(Mercenary patient, WorldSettlementFC settlement);
    ThingDef OverrideTendingMedicine(Mercenary patient, WorldSettlementFC settlement, ThingDef currentChoice);
}
```

| Method | Description |
|--------|-------------|
| `ProvideTendingDoctor` | Return a pawn to act as the tending doctor (vanilla only reads its `MedicalTendQuality` stat — it need not be spawned), or `null` to defer. **First non-null wins**; later providers don't run. |
| `OverrideTendingMedicine` | Override the medicine `ThingDef`. Receives the running choice (initially the base mod's tech-level pick); return it to defer, another `ThingDef` to override, or `null` to force no-medicine tending. **Chains** across all providers. |

---

### ITaxTickParticipant

**Registry**: `TaxTickRegistry`
**Purpose**: Hook into the tax/tithe collection lifecycle.

```csharp
public interface ITaxTickParticipant
{
    void PreTaxResolution(FactionFC faction);
    void PostTaxResolution(FactionFC faction);
    void PreSettlementCreateTax(WorldSettlementFC settlement);
    void PostSettlementCreateTax(WorldSettlementFC settlement, ref int silverAmount, List<Thing> titheThings);
}
```

| Method | When it fires |
|--------|--------------|
| `PreTaxResolution` | Once per tax tick, before any settlement is processed. |
| `PostTaxResolution` | Once per tax tick, after all settlements are processed. |
| `PreSettlementCreateTax` | Per settlement, after `SettlementTypeExtension.PreTax`. Caches are invalidated after each participant's callback. |
| `PostSettlementCreateTax` | Per settlement, after all calculations. You can modify `silverAmount` (ref) and `titheThings`. Caches are invalidated after each participant's callback. |

---

### IDailyAccrualParticipant

**Registry**: `DailyAccrualRegistry`
**Purpose**: Run faction-wide logic once per day, immediately after every settlement has accrued the day's production/upkeep and stockpile deposits have landed.

```csharp
public interface IDailyAccrualParticipant
{
    void PostDailyAccrual(FactionFC faction);
}
```

Fires once for the whole faction (not per settlement or per allocation) after the daily accrual loop completes. This is the place for daily consumption that must resolve across every settlement (e.g., Routes &amp; Resources' produce-then-consume pass).

---

### ITaxDeliveryInterceptor

**Registry**: `TaxDeliveryRegistry`
**Purpose**: Intercept and redirect tax delivery (`taxColony`) events — both when the event is queued and when it fires.

```csharp
public interface ITaxDeliveryInterceptor
{
    void OnTaxEventCreated(TaxDeliveryContext context);
    bool TryDeliverGoods(TaxDeliveryContext context);
}
```

| Method | When it fires |
|--------|--------------|
| `OnTaxEventCreated` | When a `taxColony` event is about to be queued (after goods consolidation). Mutate `context.Event.location` / `timeTillTrigger` to redirect the destination; set `context.Redirected = true` to stop further interceptors. |
| `TryDeliverGoods` | When the event fires and goods are about to be delivered. Return `true` to consume the delivery (you handled the goods); `false` to fall through to the next interceptor or the default delivery. |

---

### IFactionPowerModifier / ISettlementPowerModifier / IBattleModifier

**Registry**: `BattleModifierRegistry`
**Owner of invocation**: `WorldComponent_EnemyPower` (accessed via `FactionCache.EnemyPower`). No code outside the worldcomp invokes the registry.
**Purpose**: Three modifier sites covering the lifecycle of an enemy power value.

```csharp
// (1) Cache-time, faction-level. Mutates the deterministic EnemyPower baseline
//     (from the faction's EnemyPowerTechDef + EnemyPowerFactionDef override).
//     Use for faction-wide effects.
public interface IFactionPowerModifier
{
    void ModifyFactionPower(Faction faction, EnemyPower power);
}

// (2) Cache-time, per-settlement. Mutates a settlement's mirrored entry. Use
//     for settlement-attribute-derived effects (e.g. read CompViralSpread,
//     RimWarSettlementComp). The WD/WDExp/RW patches live here.
public interface ISettlementPowerModifier
{
    void ModifySettlementPower(Settlement settlement, EnemyPower power);
}

// (3) Attack-time. Mutates a force snapshot at engagement or display. Use for
//     battle-context effects: terrain, fortification at the battle tile,
//     traveling fatigue, weather, defensive artillery. Per-settlement static
//     properties belong in (2) so they cache.
public interface IBattleModifier
{
    void ModifyForce(BattleForceContext ctx, MilitaryForce force, bool isAttacker);
}

public class BattleForceContext
{
    public MilitaryJobDef kind;            // raid / capture / enslave / etc.
    public PlanetTile targetTile;
    public WorldObject targetObject;
    public MilitaryOperationParticipant aggressor;
    public MilitaryOperationParticipant defender;
}
```

All three are pure transformations: read the input, mutate the value argument, no side effects. The same modifier may be invoked from a real engagement OR from the squad-attack picker's estimate display, so persistence/logging/notify work belongs in `IMilitaryOperationListener` op hooks instead. A modifier may implement multiple of these interfaces if its effect spans phases; register each instance via the matching `BattleModifierRegistry.Register(...)` overload.

---

### IDefenseValidator

**Registry**: `DefenseValidatorRegistry`
**Purpose**: Veto defense assignments.

```csharp
public interface IDefenseValidator
{
    bool CanDefend(WorldSettlementFC defender, WorldSettlementFC target);
}
```

Called when a settlement is considered as a defender for another settlement (both manual selection and auto-defend). Return `false` to exclude it. **Short-circuits**: if any validator returns false, the settlement cannot defend.

---

### ISquadAssignmentValidator

**Registry**: `SquadAssignmentRegistry`
**Purpose**: Veto squad assignments with reason feedback.

```csharp
public interface ISquadAssignmentValidator
{
    bool CanAssign(WorldSettlementFC settlement, MercenarySquadFC squad, out string reason);
}
```

Called before a squad loadout is assigned to a settlement. Return `false` with a `reason` string to show the player why. **Short-circuits** on first rejection.

---

### ISquadPowerModifier

**Registry**: `SquadPowerRegistry`
**Purpose**: Compose adjustments to a mercenary squad's projected combat power (veterancy bonuses, specialist multipliers, augmentations, etc.).

```csharp
public interface ISquadPowerModifier
{
    int Priority { get; }
    SquadPower ModifyPower(MercenarySquadFC squad, SquadPower currentPower);
}

public struct SquadPower
{
    public double militaryLevel;       // same scale as WorldSettlementFC.settlementMilitaryLevel
    public double militaryEfficiency;  // multiplier, typical range 0.5-1.5
}
```

Modifiers **chain**: each receives the running `SquadPower` (initially the base computed from loadout cost + settlement efficiency) and returns the modified value. **Higher `Priority` runs first.** To no-op in a given case, return `currentPower` unchanged. Throw-safe — an exception is logged and the running power preserved.

---

### IAnimalPickerFilter

**Registry**: `AnimalPickerFilterRegistry`
**Purpose**: Gate which animal kinds appear in the military unit designer's companion-animal and mount pickers.

```csharp
public interface IAnimalPickerFilter
{
    bool IsAnimalAllowed(PawnKindDef animal);
}
```

**AND semantics**: every registered filter must return `true` for a kind for it to be offered. With no filters registered, every kind is allowed (base behavior unchanged). Called per-kind during picker redraw, so keep it cheap (back it with a cached set).

---

### IPsycastSystemProvider

**Registry**: `PsycastSystemRegistry` (registered directly — app-lifetime, **not** facade-managed, like `MilitaryWindowRegistry`)
**Purpose**: Wrap a psycast system (base-game Royalty vs. Vanilla Psycasts Expanded) for the unit designer's Psycasts tab.

The base-game provider is built in; an external system registers a higher-priority provider from its compat assembly's `[StaticConstructorOnStartup]`. Exactly one provider is "active" at a time (`PsycastSystemRegistry.Active`); saved picks carry their provider's `Key` so they apply through the right system on load. This is a broad interface (editor UI, psylink cost/grant, budget clamping) — see `Interfaces/Military/IPsycastSystemProvider.cs` for the full member set. Register via `PsycastSystemRegistry.Register(provider)`.

---

### IThreatScalingContributor

**Registry**: `ThreatScalingRegistry`
**Purpose**: Contribute additive/multiplicative modifiers to the Empire's composite "scale" measures in `ThreatScalingUtil`.

```csharp
public interface IThreatScalingContributor
{
    double GetAdditiveContribution(FactionFC faction);
    double GetMultiplicativeContribution(FactionFC faction);
}
```

| Method | Effect | No-op value |
|--------|--------|-------------|
| `GetAdditiveContribution` | Added to the raw composite before multiplication | `0` |
| `GetMultiplicativeContribution` | Multiplied into the final composite | `1.0` |

All additive contributions are summed, then all multiplicative contributions are multiplied together.

> **Note — mostly dormant.** These contributions feed the Empire Threat Level (`ComputeEmpireThreatLevel`), which is **no longer used by the live raid path** — incoming raids scale off the attacking faction's own power (`EnemyPowerTechDef` + `EnemyPowerFactionDef`) and an early-game cap, not ETL. The contributions still affect the live uncapped empire-scale measure (`ComputeEmpireScaleUncapped`, used for things like policy re-pick cost). The interface and registry are retained for that use and for a future threat-scaling submod. Don't rely on this to influence raid strength.

---

### ISettlementFoundingValidator

**Registry**: `FoundingValidatorRegistry`
**Purpose**: Validate, restrict, or add side effects when settlements are founded.

```csharp
public interface ISettlementFoundingValidator
{
    bool CanFoundSettlement(PlanetTile tile, WorldSettlementDef type, out string reason, float costMultiplier);
    string GetAdditionalCostDescription(PlanetTile tile, WorldSettlementDef type, float costMultiplier);
    void OnSettlementFounded(PlanetTile tile, WorldSettlementDef type, float costMultiplier);
}
```

| Method | Description |
|--------|-------------|
| `CanFoundSettlement` | Return `false` to prevent founding. Set `reason` for player feedback. |
| `GetAdditionalCostDescription` | Return additional cost text for the founding UI (e.g., "100 Steel"). Return `null` for no extra text. |
| `OnSettlementFounded` | Called after silver payment succeeds. Consume custom resources or perform side effects here. |

`costMultiplier` is the founding-cost scale in effect for this attempt (e.g. from a VOE outpost->settlement conversion discount); factor it into any custom cost you compute or charge.

---

### IRaidWeightProvider

**Registry**: `RaidWeightRegistry`
**Purpose**: Influence which settlement is selected as a raid target by enemy factions.

```csharp
public interface IRaidWeightProvider
{
    float GetSettlementRaidWeight(WorldSettlementFC settlement, Faction attackingFaction);
}
```

| Method | Description | No-op return |
|--------|-------------|-------------|
| `GetSettlementRaidWeight` | Weight multiplier for raid targeting. `>1` = more likely, `<1` = less likely, `0` = excluded. | `1.0f` |

All provider weights are multiplied together per settlement. A settlement's final targeting weight is `baseWeight * product(allProviderWeights)`.

---

### IRoadNodeProvider

**Registry**: `RoadNodeProviderRegistry`
**Purpose**: Contribute extra world tiles that should participate in Empire's road-network MST (e.g., VOE outposts), alongside Empire settlements.

```csharp
public interface IRoadNodeProvider
{
    IEnumerable<PlanetTile> GetRoadNodeTiles();
}
```

Called on the main thread during road-queue recalculation. The road system is surface-only, but you may yield freely — non-surface or invalid tiles are filtered out by `RoadNodeProviderRegistry.CollectInto`, so implementations don't need to enforce the invariant themselves.

---

### ISilverPaymentModifier

**Registry**: `SilverPaymentRegistry`
**Purpose**: Intercept and modify silver payments before processing.

```csharp
public interface ISilverPaymentModifier
{
    void ModifyPayment(SilverPaymentContext context);
}
```

**SilverPaymentContext fields:**

| Field | Type | Description |
|-------|------|-------------|
| `Amount` | `int` | The payment amount. Modify this to change how much is charged. |
| `Reason` | `string` | What the payment is for (e.g., building cost, worker upkeep). |
| `Settlement` | `WorldSettlementFC` | The settlement involved (may be null for faction-level payments). |

Modifiers are called sequentially — each sees the previous modifier's changes to `Amount`.

---

### IMainTabWindowOverview

**Registry**: `MainTableRegistry`
**Purpose**: Add tabs to the main Empire faction window.

```csharp
public interface IMainTabWindowOverview
{
    void PreOpenWindow(FactionFC faction);
    void OnTabSwitch();
    void DrawOverviewTab(Rect boundingBox);
    void PostCloseWindow();
    string TabName();
}
```

| Method | When it fires |
|--------|--------------|
| `PreOpenWindow` | When the main tab window opens. |
| `OnTabSwitch` | When the user switches to your tab. |
| `DrawOverviewTab` | Every frame while your tab is active. `boundingBox` is the drawable area. |
| `PostCloseWindow` | When the main tab window closes. |
| `TabName` | Returns the tab label string. |

Tabs from this registry appear in a separate "Empire Extensions" tab window, which is only visible when at least one tab is registered (via a custom `MainButtonWorker`).

---

### BuildingFilter + BuildingFilterRegistry

**Registry**: `BuildingFilterRegistry`
**Purpose**: Add filter buttons to the building construction UI.

`BuildingFilter` is a simple class (not an interface):

```csharp
public class BuildingFilter
{
    public string label;
    public Texture2D icon;
    public Func<BuildingFCDef, bool> predicate;
}
```

```csharp
BuildingFilterRegistry.Register(new BuildingFilter
{
    label = "Military",
    icon = myMilitaryIcon,
    predicate = def => def.statModifiers.Any(m => m.stat == FCStatDefOf.militaryBaseLevel)
});
```

Filters are cleared on cache invalidation — re-register them as needed (typically in a static constructor, since the base mod re-invokes them).

---

### IRaidTarget

**Registry**: `RaidTargetRegistry`
**Purpose**: Make external world objects (e.g., outposts from other mods) available as raid targets for Empire's military system.

Registered targets appear in the attack target pool alongside Empire settlements, receive the same 24-hour warning, and auto-resolve via the standard battle simulation.

```csharp
public interface IRaidTarget
{
    WorldObject WorldObject { get; }
    string Name { get; }
    PlanetTile Tile { get; }
    int MilitaryLevel { get; }
    bool IsUnderAttack { get; set; }
    void OnRaidWon(BattleResult result);
    void OnRaidLost(BattleResult result);
}
```

| Property/Method | Description |
|-----------------|-------------|
| `WorldObject` | The world object this target wraps (for serialization and `LookTargets`). |
| `Name` | Display name in the military UI. |
| `Tile` | World tile for targeting weight and distance calculations. |
| `MilitaryLevel` | Virtual military level used for targeting weight and auto-defend comparison. |
| `IsUnderAttack` | Set by the attack system to prevent duplicate attacks. Cleared on resolution. |
| `OnRaidWon` | Called when Empire wins the battle against this target. |
| `OnRaidLost` | Called when Empire loses the battle against this target. |

---

### IAutoDefender

**Registry**: `AutoDefenderRegistry`
**Purpose**: Register external world objects as auto-defenders for Empire settlements (and other `IRaidTarget`s).

When a settlement is attacked, the registry searches for the best available defender within range. The defender creates a `MilitaryForce` and is placed on cooldown after battle resolution.

```csharp
public interface IAutoDefender
{
    WorldObject WorldObject { get; }
    int MilitaryLevel { get; }
    int Range { get; }
    bool CanAutoDefend { get; }
    MilitaryForce CreateDefendingForce();
    void OnDefensePledged(WorldObject target);
    void OnDefenseStarted(WorldObject target);
    void OnDefenseComplete(bool won, BattleResult result);
    void OnDefenseReplaced();
    List<Pawn> GetDefendingPawns();
    void ReturnDefendingPawns(List<Pawn> pawns);
}
```

| Property/Method | Description |
|-----------------|-------------|
| `WorldObject` | The world object this defender wraps. |
| `MilitaryLevel` | Military strength for comparison when selecting the best defender. |
| `Range` | Maximum tile distance for auto-defense eligibility. |
| `CanAutoDefend` | True if the defender is available (enabled, not busy, etc.). |
| `CreateDefendingForce` | Generate a `MilitaryForce` to defend with. |
| `OnDefensePledged` | Called when this defender is chosen to protect a target (pledge time, before engagement). |
| `OnDefenseStarted` | Called when this defender is assigned to protect a target. |
| `OnDefenseComplete` | Called when the battle resolves. |
| `OnDefenseReplaced` | Called when this defender is replaced by another force (not defeated). |
| `GetDefendingPawns` | Returns pawns for a manual battle, or null to generate from force points. Implementations should remove pawns from their source before returning. |
| `ReturnDefendingPawns` | Called after a manual battle ends to return surviving pawns (already despawned from the battle map). |

---

### ISettlementWindowButton

**Registry**: `SettlementButtonRegistry`
**Purpose**: Add buttons to the settlement window's left panel.

Buttons are drawn uniformly as text buttons between the built-in buttons (Upgrade, Special Actions, Prisoners, Military) and the Delete button. The window handles all rendering — implementations only provide label, click behavior, and visibility/enabled state.

```csharp
public interface ISettlementWindowButton
{
    string Label(WorldSettlementFC settlement);
    void OnClick(WorldSettlementFC settlement);
    bool IsEnabled(WorldSettlementFC settlement);
    bool IsVisible(WorldSettlementFC settlement);
}
```

| Method | Description |
|--------|-------------|
| `Label` | Translated button text. Called every frame, so dynamic labels (e.g., with counts) work. |
| `OnClick` | Called when the button is clicked. Open windows, show float menus, etc. |
| `IsEnabled` | Return `true` if clickable. Disabled buttons are drawn grayed out. |
| `IsVisible` | Return `true` to show the button. Hidden buttons take no space. |

**Usage example**:

```csharp
public class MySettlementButton : ISettlementWindowButton
{
    public string Label(WorldSettlementFC settlement) => "MyButtonLabel".Translate();
    public void OnClick(WorldSettlementFC settlement) => Find.WindowStack.Add(new MyWindow(settlement));
    public bool IsEnabled(WorldSettlementFC settlement) => true;
    public bool IsVisible(WorldSettlementFC settlement) => settlement.HasMyComp();
}

// In your mod's initialization:
SettlementButtonRegistry.Register(new MySettlementButton());
```

---

### IMilitaryTabEntry

**Registry**: `MilitaryTabRegistry`
**Purpose**: Display external entries in Empire's military tab alongside settlements.

Entries appear as simplified cards showing name, military level, status, and an auto-defend toggle.

```csharp
public interface IMilitaryTabEntry
{
    WorldObject WorldObject { get; }
    string Name { get; }
    int MilitaryLevel { get; }
    bool AutoDefend { get; set; }
    bool IsUnderAttack { get; }
    bool IsBusy { get; }
    string StatusLabel { get; }
    Color AccentColor { get; }
}
```

| Property | Description |
|----------|-------------|
| `WorldObject` | The world object this entry represents. |
| `Name` | Display name in the military tab. |
| `MilitaryLevel` | Military level shown on the card. |
| `AutoDefend` | Whether auto-defend is enabled. Toggled by the player via the UI. |
| `IsUnderAttack` | Whether this entry is currently under attack. |
| `IsBusy` | Whether this entry is currently busy with a military operation. |
| `StatusLabel` | Status text shown on the card (e.g., "Idle", "Defending"). |
| `AccentColor` | UI accent color for the card. |

---

### ISquadInspectionSection

**Registry**: `SquadInspectionRegistry`
**Purpose**: Add sections to the squad inspection window (below the per-pawn rows).

```csharp
public interface ISquadInspectionSection
{
    string SectionLabel { get; }
    float GetSectionHeight(MercenarySquadFC squad, float width);
    void DrawSection(MercenarySquadFC squad, Rect contentRect);
    int Order { get; }
}
```

| Member | Description |
|--------|-------------|
| `SectionLabel` | Header label for the section (the caller draws the header). |
| `GetSectionHeight` | Total height needed below the header. Return `0` to hide the section entirely. |
| `DrawSection` | Draw the section content into `contentRect` (below the caller-drawn header). |
| `Order` | Lower values render earlier; ties broken by registration order. |

---

### IFoundingCompanionWindow

**Registry**: none — implemented by a `Window` subclass; the base mod discovers open windows implementing it.
**Purpose**: Dock a companion window beside the settlement Found screen (`CreateColonyWindowFc`).

```csharp
public interface IFoundingCompanionWindow
{
    int CompanionOrder { get; }
}
```

The base mod lays all companion windows out in a horizontal cascade via `FoundingScreenHooks.ReflowCompanions` — implementers must **not** set their own `windowRect.x/y`. Lower `CompanionOrder` sits closer to the main window (rightmost).

---

## Comp-Based Interfaces

These interfaces are implemented on `WorldObjectComp` classes attached to `WorldSettlementFC`. They are discovered by iterating `settlement.AllComps` — no registry needed. See [Settlement Comps](worldobject-comps.md) for how to attach a comp.

### ISettlementWindowOverview

**Purpose**: Add a tab to an individual settlement's window.

```csharp
public interface ISettlementWindowOverview
{
    void PreOpenWindow(WorldSettlementFC settlement);
    void OnTabSwitch();
    void DrawOverviewTab(Rect boundingBox);
    void PostCloseWindow();
    string OverviewTabName();
}
```

Must be implemented by a `WorldObjectComp` attached to the settlement. The settlement window discovers it at open time and adds the tab.

---

### IStatModifierProvider

**Purpose**: Contribute dynamic stat values per settlement.

```csharp
public interface IStatModifierProvider
{
    double GetStatModifier(FCStatDef stat);
    string GetStatModifierDesc(FCStatDef stat);
}
```

| Method | Description | No-op return |
|--------|-------------|-------------|
| `GetStatModifier` | Returns a value that is added (Additive) or multiplied (Multiplicative) into the settlement's stat | `0` (Additive) or `1` (Multiplicative) |
| `GetStatModifierDesc` | Returns a tooltip description string for your contribution | `null` or `""` |

**Caching**: Results are cached per settlement. Caches invalidate automatically after lifecycle events. For changes outside lifecycle callbacks, call `settlement.InvalidateStatCache()`.

---

### IResourceProductionModifier

**Purpose**: Contribute dynamic resource production bonuses per settlement.

```csharp
public interface IResourceProductionModifier
{
    double GetResourceAdditiveModifier(ResourceFC resource);
    double GetResourceMultiplierModifier(ResourceFC resource);
    string GetResourceAdditiveDesc(ResourceFC resource);
    string GetResourceMultiplierDesc(ResourceFC resource);
}
```

| Method | Description | No-op return |
|--------|-------------|-------------|
| `GetResourceAdditiveModifier` | Added to the production base | `0` |
| `GetResourceMultiplierModifier` | Multiplied into the production multiplier | `1` |
| `GetResourceAdditiveDesc` | Tooltip description for additive contribution | `null` or `""` |
| `GetResourceMultiplierDesc` | Tooltip description for multiplier contribution | `null` or `""` |

**Caching**: Results are lazily cached by `ResourceFC`'s dirty flags. Invalidated automatically after lifecycle events. For changes outside lifecycle callbacks, call `settlement.InvalidateResourceCaches()`.

See [Stat System — Resource Production Formula](stat-system.md#resource-production-formula) for how these values fit into the full calculation.

---

### ITitheBudgetModifier

**Purpose**: Inject external tithe budget into a settlement's resource production. The additional budget increases how many (or how valuable) tithe items are generated, without penalizing the settlement's `actualIncome` for externally-sourced goods.

```csharp
public interface ITitheBudgetModifier
{
    double GetDailyExternalTitheBudget(ResourceFC resource);
    string GetExternalTitheBudgetDesc(ResourceFC resource);
}
```

| Method | Description | No-op return |
|--------|-------------|-------------|
| `GetDailyExternalTitheBudget` | Returns additional daily tithe budget (in silver value) for the given resource. | `0` |
| `GetExternalTitheBudgetDesc` | Tooltip description for the tithe budget breakdown. | `null` or `""` |

Must be implemented by a `WorldObjectComp` attached to the settlement. Queried during tithe budget calculation via `ResourceFC.externalTitheBudget`.

**Caching**: Results are cached per settlement. Automatically invalidated after lifecycle events. For changes outside lifecycle callbacks, call `settlement.InvalidateStatCache()`.

---

### IProfitContributor

**Purpose**: Contribute upkeep or income to a settlement's economic calculations.

```csharp
public interface IProfitContributor
{
    double GetDailyUpkeepContribution();
    string GetDailyUpkeepContributionDesc();
    double GetDailyIncomeContribution();
    string GetDailyIncomeContributionDesc();
}
```

| Method | Description | No-op return |
|--------|-------------|-------------|
| `GetDailyUpkeepContribution` | Additional daily upkeep cost added to the settlement's total. | `0` |
| `GetDailyUpkeepContributionDesc` | Tooltip line for the upkeep breakdown. | `null` or `""` |
| `GetDailyIncomeContribution` | Additional daily income added to the settlement's total. | `0` |
| `GetDailyIncomeContributionDesc` | Tooltip line for the income breakdown. | `null` or `""` |

Must be implemented by a `WorldObjectComp` attached to the settlement.

**Caching**: Results are cached per settlement. Automatically invalidated after lifecycle events. For changes outside lifecycle callbacks, call `settlement.DirtyProfitCache()`.

---

### ISettlementPostLoadInit

**Purpose**: Run initialization that depends on fully-rebuilt settlement state after a save is loaded.

```csharp
public interface ISettlementPostLoadInit
{
    void PostSettlementLoadInit(WorldSettlementFC settlement);
}
```

Called after stat modifiers and resource caches are rebuilt during `FinalizeInit`. Use this when your comp needs to read computed values (stat totals, resource production, etc.) that aren't available in `PostExposeData`.

Must be implemented by a `WorldObjectComp` attached to the settlement.

---

## Extension-Based Interfaces

### IBuildingDetailSection

**Purpose**: Add custom sections to the building detail panel in the building construction window.

This is an interface implemented on a `DefModExtension` attached to a `BuildingFCDef`. The window discovers implementors via `def.modExtensions.OfType<IBuildingDetailSection>()`. Sections render between the Modifiers block and the Settlement Impact block.

See [DefModExtensions — IBuildingDetailSection](def-mod-extensions.md#ibuildingdetailsection) for the full interface and usage.

---

## Registry API Summary

All facade-managed registries share the same API:

```csharp
// Register (preferred: unified entry point)
EmpireRegistry.Register(instance);

// Register (typed alternative — explicit, narrower)
MyRegistry.Register(instance);

// Unregister
EmpireRegistry.Unregister(instance);   // or MyRegistry.Unregister(instance)

// Clear all facade-managed registries (called from EmpireCacheUtil.InvalidateAll)
EmpireRegistry.ClearAll();

// Read-only access to registered items (most registries)
IReadOnlyList<T> items = MyRegistry.Items;  // property name varies
```

Every facade-managed registry is cleared by `EmpireRegistry.ClearAll()` on cache invalidation. `MilitaryWindowRegistry` is the one exception (slot-keyed, outside the facade).

| Registry | Public collection |
|----------|-------------------|
| `LifecycleRegistry` | (none) |
| `TaxTickRegistry` | `.Taxers` |
| `DailyAccrualRegistry` | `.Participants` |
| `TaxDeliveryRegistry` | `.Interceptors` |
| `BattleModifierRegistry` | `.Modifiers` |
| `DefenseValidatorRegistry` | (none) |
| `SquadAssignmentRegistry` | (none) |
| `FoundingValidatorRegistry` | (none) |
| `RaidWeightRegistry` | `.Providers` |
| `RoadNodeProviderRegistry` | `.Providers` |
| `ThreatScalingRegistry` | `.Contributors` |
| `SilverPaymentRegistry` | `.Modifiers` |
| `MercAutoTendRegistry` | `.Providers` |
| `SquadPowerRegistry` | `.Modifiers` |
| `AnimalPickerFilterRegistry` | `.Filters` |
| `RaidTargetRegistry` | `.Targets` |
| `AutoDefenderRegistry` | `.Defenders` |
| `MilitaryTabRegistry` | `.Entries` |
| `MainTableRegistry` | `.Tabs` |
| `SettlementButtonRegistry` | `.Entries` |
| `SquadInspectionRegistry` | `.Sections` |
| `BuildingFilterRegistry` | `.Filters` |

`PsycastSystemRegistry` and `MilitaryWindowRegistry` are app-lifetime and **not** facade-managed (registered directly, not cleared on game dispose).

---

## Internal: registry implementation pattern

This section is for base-mod contributors adding or modifying a registry. Submods don't need it.

Per-domain registries share two helpers in `util/Registries/_Internal/`:

- **`RegistryList<T>`** — encapsulates `Register`/`Unregister`/`ClearAll`/`Items`/`Count`. Use as a `private static readonly` field; the registry's public methods are thin delegating wrappers (marked `internal` after the visibility lockdown).
- **`RegistryDispatch`** — static helpers for the dispatch idioms, each wrapping `try/catch + LogUtil.Error` with a consistent format:
  - `Each(items, action, call)` — iterate, no aggregation
  - `EachInvalidating(items, action, invalidate, call)` — iterate + invalidate-between (e.g. settlement-cache invalidation)
  - `All(items, predicate, call)` — short-circuit on first false
  - `Aggregate(items, seed, reducer, call)` — fold (product, sum, etc.)
  - `First(items, predicate, call)` — first item matching predicate, or null
  - `FirstNonNull(items, selector, call)` — first item whose selector yields non-null

A standard registry then looks like:

```csharp
public static class FooRegistry
{
    private static readonly RegistryList<IFoo> _list = new RegistryList<IFoo>();

    internal static void Register(IFoo p) => _list.Register(p);
    internal static void Unregister(IFoo p) => _list.Unregister(p);
    internal static void ClearAll() => _list.ClearAll();
    public static IReadOnlyList<IFoo> Items => _list.Items;

    public static void InvokeDoSomething(...)
        => RegistryDispatch.Each(_list.Items, p => p.DoSomething(...), nameof(IFoo.DoSomething));
}
```

Then add the new interface to the probe in `EmpireRegistry.Register / Unregister` and to the fan-out in `EmpireRegistry.ClearAll`. That's the only place outside the registry file that needs editing.

**Edge cases:** registries with non-list storage (e.g. `MilitaryWindowRegistry`'s slot-keyed dictionary), priority sorts (`SquadPowerRegistry`, `SquadInspectionRegistry`), or unusual semantics keep their custom storage and dispatch — the helpers are a convenience, not a requirement.
