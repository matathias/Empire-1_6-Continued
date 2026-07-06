# Getting Started

This guide covers Empire-specific conventions and patterns. It assumes familiarity with RimWorld modding fundamentals (assemblies, Harmony, XML patching, save/load).

---

## Namespace

All Empire code lives under the `FactionColonies` namespace:

```
FactionColonies          — Primary namespace (defs, comps, settlements, military, windows)
FactionColonies.util     — Utilities, extensions, registries
FactionColonies.PatchNote — Patch note subsystem (internal)
```

Your submod can use any namespace. Reference `FactionColonies` types directly.

---

## Key Access Points

| Accessor | Returns | Purpose |
|----------|---------|---------|
| `FindFC.FactionComp` | `FactionFC` | The singleton WorldComponent holding all faction state (settlements, policies, events, resources, military). |
| `FindFC.EmpireFaction` | `Faction` | The NPC faction object that represents the player's empire on the world map. |

`FindFC` is the `Verse.Find`-style accessor class for Empire — see `FindFC.cs` for the full set of accessors (`Military`, `EventManager`, `TaxLedger`, `RoadBuilder`, `TechLevel`, `FactionLevel`, etc.). All references are lazily cached and automatically invalidated on game load/dispose. The older `FactionCache` class is now narrowed to computed caches only.

---

## Extension Point Taxonomy

Empire provides three categories of extension points. Choose the right one based on your needs:

### 1. XML-Only Defs
Define new content purely in XML. No C# required.

**Use for**: new buildings, events, resources, policies, settlement types, biome resources, military jobs, stats.

All custom def types support `modExtensions` for attaching DefModExtension subclasses when you need C# behavior tied to a def. See [XML Def Types](xml-defs.md).

### 2. Comp-Based (Per-Settlement)
Attach a `WorldObjectComp` to `WorldSettlementFC` via XML patching. The comp can implement Empire interfaces to participate in per-settlement systems.

**Use for**: per-settlement UI tabs, dynamic stat/resource contributions, settlement-specific state.

Available comp interfaces:
- `ISettlementWindowOverview` — add a tab to the settlement window
- `IStatModifierProvider` — contribute to stat aggregation
- `IResourceProductionModifier` — contribute to resource production
- `ITitheBudgetModifier` — inject external tithe budget
- `IProfitContributor` — contribute upkeep or income to settlement economics
- `ISettlementPostLoadInit` — run initialization after settlement state is fully rebuilt on load

See [Settlement Comps](worldobject-comps.md).

### 3. Registry-Based (Global)
Register a class instance with a static registry. The base mod iterates registered instances at specific points.

**Use for**: global lifecycle hooks, tax interception, battle modification, defense/squad validation, threat scaling, silver payment interception, main tab UI tabs, building UI filters.

Register through the unified `EmpireRegistry` facade — a single call probes your instance for every supported interface and routes it to the matching domain registries (the per-domain `XxxRegistry.Register` methods are `internal` to the base mod):

```csharp
// Register (typically in a static constructor or comp Initialize).
// One call covers every Empire interface myInstance implements.
EmpireRegistry.Register(myInstance);

// Unregister (in cleanup, if needed)
EmpireRegistry.Unregister(myInstance);
```

Registries are not serialized. Your mod must re-register on game load. (`PsycastSystemRegistry` and `MilitaryWindowRegistry` are app-lifetime exceptions registered directly — see [Interfaces & Registries](interfaces-and-registries.md).)

See [Interfaces & Registries](interfaces-and-registries.md).

---

## Cache Invalidation

Empire caches stat values and resource production calculations per settlement. These caches are **automatically invalidated** after every lifecycle event (building constructed/deconstructed, settlement created/removed/upgraded, squad deployed/recalled, battle resolved, research completed, tax collected).

This means:
- If your submod changes values inside a lifecycle callback (e.g., `ISettlementListener.OnBuildingConstructed`), the caches are already dirty — your new values will be picked up on the next query.
- If your submod changes values **outside** a lifecycle callback (e.g., in response to a player action or a custom timer), you must manually invalidate:
  ```csharp
  // For stat changes:
  settlement.InvalidateStatCache();
  // For resource production changes:
  settlement.InvalidateResourceCaches();
  ```

---

## Suggested Reading Order

1. [Stat & Production System](stat-system.md) — foundational; many other docs reference it
2. [XML Def Types](xml-defs.md) — the most common extension path
3. [Interfaces & Registries](interfaces-and-registries.md) — C# hooks
4. [Settlement Comps](worldobject-comps.md) — per-settlement extensibility
5. The remaining guides as needed for your submod
