# Stat & Resource Production System

The stat system is the backbone of Empire's economy. Buildings, policies, events, settlement types, and submod comps all contribute to stats, which drive resource production, military strength, social metrics, and more.

---

## FCStatDef

A named stat defined in XML. Referenced by `defName` in `FCStatModifier` entries — typos produce XML errors at startup.

**Class**: `FactionColonies.FCStatDef` (extends `Def`)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `aggregation` | `FCStatAggregation` | `Additive` | How multiple modifiers combine. `Additive`: values are summed. `Multiplicative`: values are multiplied. |
| `appliesToSettlements` | `bool` | `true` | If true, settlement-level sources (buildings, settlement type, events, comps) contribute. If false, only faction-level sources (policies, traits, edicts) apply. |
| `descriptionKey` | `string` | `null` | Translation key for UI display. Receives the formatted bonus value as `{0}`. |
| `invertedForDisplay` | `bool` | `false` | If true, lower values are shown as "good" in the UI (green). Use for costs, losses, penalties. |
| `displayDivisor` | `double` | `0` | If non-zero, divides the raw stat value before display. Used for tick-based stats (e.g., `2500` to convert ticks to in-game hours). |
| `linkedResource` | `ResourceTypeDef` | `null` | If set, this stat is a resource production stat. The UI uses the resource's name and icon instead of `descriptionKey`. |

**Identity value**: `0` for Additive stats, `1` for Multiplicative stats. This is the starting value before any modifiers are applied.

See [ExampleDefs/FCStatDef.xml](ExampleDefs/FCStatDef.xml) for an annotated XML example.

### Built-in Stats (FCStatDefOf)

The base mod defines many stats (see `1.6/Defs/FCStatDefs/`). The categories below are illustrative, not exhaustive:

| Category | Stats |
|----------|-------|
| **Military** | `militaryBaseLevel`, `militaryCombatEfficiency`, `militaryLevelBonusDefending/Attacking`, `militaryEfficiencyBonusAttacking/Defending`, `militaryCooldownOffset`, `raidCooldownOffset` |
| **Threat** | `threatScalingBase`, `threatScalingMultiplier` |
| **Battle penalties** | `battleProsperityLossMultiplier`, `battleHappinessLossMultiplier`, `battleLoyaltyLossMultiplier` |
| **Economy** | `taxBasePercentage`, `taxBaseRandomModifier`, `taxBonusFlat`, `titheValueMultiplier`, `lootMultiplier`, `settlementCostMultiplier`, `buildTimeMultiplier`, `createSettlementBaseCost`, `createSettlementMultiplier`, `researchContributionMultiplier` |
| **Workers** | `workerBaseCost`, `workerBaseMax`, `workerBaseOverMax`, `extraWorkersSoftcap`, `overMaxWorkersAdjustment` |
| **Social** | `happinessLostBase/GainedBase`, `happinessLostMultiplier/GainedMultiplier`, `loyaltyLostBase/GainedBase`, `loyaltyLostMultiplier/GainedMultiplier`, `unrestLostBase/GainedBase`, `unrestLostMultiplier/GainedMultiplier`, `prosperityGainedBase` |
| **Resource production** | 2 stats per resource type (additive + multiplier), linked via `ResourceTypeDef.productionAdditiveStat/productionMultiplierStat` |

---

## FCStatModifier

A `{stat, value}` pair used in lists on many def types. Not a Def itself.

**Class**: `FactionColonies.FCStatModifier`

| Field | Type | Description |
|-------|------|-------------|
| `stat` | `FCStatDef` | Reference to the stat being modified (by defName in XML). |
| `value` | `double` | The modifier value. For Additive stats, this is added. For Multiplicative stats, this is multiplied. |

**Used on**: `BuildingFCDef.statModifiers`, `FCEventDef.statModifiers`, `FCPolicyDef.statModifiers`, `WorldSettlementDef.statModifiers`.

```xml
<statModifiers>
    <li>
        <stat>taxBasePercentage</stat>
        <value>0.1</value>
    </li>
</statModifiers>
```

---

## Stat Aggregation Pipeline

`FactionFC.GetStatValue(FCStatDef stat, WorldSettlementFC settlement = null, MercenarySquadFC squad = null, Mercenary unit = null)` is the entry point. It folds together a **scope chain** — faction (cached) → settlement (cached) → squad-instance → unit-instance — where each scope beyond the faction contributes only if the stat opts into it (`appliesToSettlements` / `appliesToSquads` / `appliesToUnits`) *and* the matching context argument is supplied. On top of that sits an uncached behavior layer:

### 1. Faction-Level Partial (cached)
Starts from `stat.IdentityValue`, then applies modifiers from:
1. **Active policies** (`FactionFC.policies`)
2. **Faction traits** (`FactionFC.factionTraits`)
3. **Active edicts** (`FactionFC.edicts`)

All use their `def.statModifiers` lists.

### 2. Settlement-Level Partial (cached, only if `stat.appliesToSettlements`)
Starts from `stat.IdentityValue`, then applies modifiers from:
1. **Tagged stat modifiers** — a merged list of modifiers from buildings, events, and the settlement type, each tagged with a source label for tooltip display
2. **`IStatModifierProvider` comps** — any WorldObjectComp on the settlement that implements this interface

### 3. Combination
Each scope is combined into the running value using the stat's aggregation:
- **Additive stats**: scopes are summed (`factionPartial + settlementPartial + …`)
- **Multiplicative stats**: scopes are multiplied (`factionPartial * settlementPartial * …`)

### 4. Behavior Adjustment (uncached)
Each active `FCPolicyBehavior` gets a chance to modify the result via `ModifyStat(stat, currentValue, settlement)`. This is called last and can depend on runtime state.

### Shortcut
`WorldSettlementFC.GetStatValue(stat)` delegates to `FactionFC.GetStatValue(stat, this)` — use either, but always pass in the relevant WorldSettlementFC. Only use null if there is none.

---

## Resource Production Formula

Each settlement has one `ResourceFC` per resource type. Production is calculated as:

```
production = productionBase * productionMult
rawTotalProduction = production * assignedWorkers
```

### productionBase
Sum of three sources:
1. **Dictionary additives** — biome base, hilliness extension, settlement type base (static, environmental)
2. **Stat system** — `settlement.GetStatValue(def.productionAdditiveStat)` (buildings, policies, events, settlement type stat modifiers)
3. **Comp additives** — `IResourceProductionModifier.GetResourceAdditiveModifier(resource)` on all comps

### productionMult
Product of four sources:
1. **Dictionary multipliers** — biome multiplier, hilliness extension, settlement type multiplier (static, environmental)
2. **Stat system** — `settlement.GetStatValue(def.productionMultiplierStat)` (buildings, policies, events, settlement type stat modifiers)
3. **Comp multipliers** — `IResourceProductionModifier.GetResourceMultiplierModifier(resource)` on all comps
4. **Tax bonus** — `settlement.GetSettlementTaxBonus()`

### How to Contribute to Production

| Approach | Mechanism | When to use |
|----------|-----------|-------------|
| **FCStatModifier on a def** | Add `{stat: RTD_Food_ProductionAdditive, value: 2}` to a `BuildingFCDef.statModifiers` | Static bonuses from buildings, policies, events, settlement types |
| **IResourceProductionModifier on a comp** | Implement the interface on a WorldObjectComp | Dynamic bonuses that depend on runtime state (e.g., governor skill, weather, custom conditions) |

---

## Cache Invalidation

Both stat and production caches are lazily computed and automatically invalidated after lifecycle events. The invalidation order is:

1. Lifecycle event fires (e.g., building constructed)
2. Settlement stat/resource caches are marked dirty
3. `LifecycleRegistry` hooks are invoked (your code runs here — caches are already dirty)
4. Next stat/production query triggers recalculation with fresh values

**Manual invalidation** is only needed if you change values outside a lifecycle callback:
```csharp
settlement.InvalidateStatCache();       // for stat changes
settlement.InvalidateResourceCaches();  // for resource production changes
```

---

## Defining Production Stats for a New Resource

When creating a new `ResourceTypeDef`, you must also create two `FCStatDef` entries for its production stats:

```xml
<!-- 1. Additive production stat -->
<FactionColonies.FCStatDef>
    <defName>RTD_MyResource_ProductionAdditive</defName>
    <label>My Resource production (additive)</label>
    <aggregation>Additive</aggregation>
    <linkedResource>RTD_MyResource</linkedResource>
</FactionColonies.FCStatDef>

<!-- 2. Multiplicative production stat -->
<FactionColonies.FCStatDef>
    <defName>RTD_MyResource_ProductionMultiplier</defName>
    <label>My Resource production (multiplier)</label>
    <aggregation>Multiplicative</aggregation>
    <linkedResource>RTD_MyResource</linkedResource>
</FactionColonies.FCStatDef>
```

Then reference them from your ResourceTypeDef:
```xml
<productionAdditiveStat>RTD_MyResource_ProductionAdditive</productionAdditiveStat>
<productionMultiplierStat>RTD_MyResource_ProductionMultiplier</productionMultiplierStat>
```

Buildings, policies, and events can then boost your resource by including these stats in their `statModifiers`.
