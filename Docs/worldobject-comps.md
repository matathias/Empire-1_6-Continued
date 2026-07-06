# Settlement Comps in Empire

Empire settlements (`WorldSettlementFC`) use the standard RimWorld `WorldObjectComp` pattern. This guide focuses on what's specific to Empire: how to attach your comp, which Empire interfaces your comp can implement, and how the base mod discovers and invokes them.

---

## Attaching a Comp to Settlements

Empire defines its settlement types as `WorldSettlementDef` defs (which extend `WorldObjectDef`). Comps are listed in the `<comps>` block. To add your comp to all settlements of a given type, use an XML patch:

```xml
<!-- Add your comp to surface settlements -->
<Operation Class="PatchOperationAdd">
    <xpath>/Defs/FactionColonies.WorldSettlementDef[@Name="WorldSettlementDef_Surface"]/comps</xpath>
    <value>
        <li Class="YourNamespace.MyCompProperties" />
    </value>
</Operation>
```

The base settlement types are:

| defName | Type |
|---------|------|
| `WorldSettlementDef_Surface` | Standard surface settlements |
| `WorldSettlementDef_Orbital` | Orbital settlements (Odyssey DLC) |

---

## Empire Interfaces for Comps

Your `WorldObjectComp` can implement any combination of these Empire interfaces. The base mod discovers them by iterating `settlement.AllComps` and checking for interface implementation.

### ISettlementWindowOverview

Adds a tab to the settlement window.

```csharp
public class MyComp : WorldObjectComp, ISettlementWindowOverview
{
    public void PreOpenWindow(WorldSettlementFC settlement) { /* initialize UI state */ }
    public void OnTabSwitch() { /* user clicked your tab */ }
    public void DrawOverviewTab(Rect boundingBox) { /* draw your tab content */ }
    public void PostCloseWindow() { /* cleanup */ }
    public string OverviewTabName() => "My Tab";
}
```

**Discovery**: `SettlementWindowFc.PreOpen()` iterates `settlement.AllComps`, casts each to `ISettlementWindowOverview`, and adds non-null results as additional tabs. Tabs appear after the built-in Overview and Tithing tabs.

### IStatModifierProvider

Contributes dynamic values to the [stat aggregation pipeline](stat-system.md#stat-aggregation-pipeline).

```csharp
public class MyComp : WorldObjectComp, IStatModifierProvider
{
    public double GetStatModifier(FCStatDef stat)
    {
        if (stat == FCStatDefOf.militaryBaseLevel)
            return 2.0; // +2 for Additive stats
        return 0; // no-op for Additive (use 1 for Multiplicative)
    }

    public string GetStatModifierDesc(FCStatDef stat)
    {
        if (stat == FCStatDefOf.militaryBaseLevel)
            return "+2 - My Comp\n";
        return null;
    }
}
```

**Discovery**: `WorldSettlementFC.GetSettlementStatValue()` iterates `AllComps` and aggregates values from any `IStatModifierProvider`.

**Caching**: Results are cached per settlement per stat. Automatically invalidated after lifecycle events. If your values change outside a lifecycle callback, call:
```csharp
((WorldSettlementFC)parent).InvalidateStatCache();
```

### IResourceProductionModifier

Contributes dynamic bonuses to [resource production](stat-system.md#resource-production-formula).

```csharp
public class MyComp : WorldObjectComp, IResourceProductionModifier
{
    public double GetResourceAdditiveModifier(ResourceFC resource)
    {
        // Add +1 base production to food
        if (resource.def.defName == "RTD_Food")
            return 1.0;
        return 0; // no-op
    }

    public double GetResourceMultiplierModifier(ResourceFC resource)
    {
        return 1; // no-op (multiplicative)
    }

    public string GetResourceAdditiveDesc(ResourceFC resource)
    {
        if (resource.def.defName == "RTD_Food")
            return "+1 - My Comp\n";
        return null;
    }

    public string GetResourceMultiplierDesc(ResourceFC resource)
    {
        return null; // no-op
    }
}
```

**Discovery**: `ResourceFC.CalculateProductionBase()` and `CalculateProductionMult()` iterate `settlement.AllComps` for `IResourceProductionModifier`.

**Caching**: Results are lazily cached by `ResourceFC`'s dirty flags. Automatically invalidated after lifecycle events. For changes outside lifecycle callbacks:
```csharp
((WorldSettlementFC)parent).InvalidateResourceCaches();
```

### ITitheBudgetModifier

Injects external tithe budget into a settlement's resource production. The additional budget increases how many (or how valuable) tithe items are generated, without penalizing the settlement's `actualIncome` for externally-sourced goods.

```csharp
public class MyComp : WorldObjectComp, ITitheBudgetModifier
{
    public double GetDailyExternalTitheBudget(ResourceFC resource)
    {
        // Add 50 silver worth of tithe budget to food
        if (resource.def.defName == "RTD_Food")
            return 50.0;
        return 0;
    }

    public string GetExternalTitheBudgetDesc(ResourceFC resource)
    {
        if (resource.def.defName == "RTD_Food")
            return "+50 - My Comp\n";
        return null;
    }
}
```

**Discovery**: `ResourceFC.externalTitheBudget` iterates `settlement.AllComps` for `ITitheBudgetModifier`.

**Caching**: Results are cached per settlement. Automatically invalidated after lifecycle events. For changes outside lifecycle callbacks:
```csharp
((WorldSettlementFC)parent).InvalidateStatCache();
```

### IProfitContributor

Contributes upkeep or income to a settlement's economic calculations (displayed in the settlement profit breakdown).

```csharp
public class MyComp : WorldObjectComp, IProfitContributor
{
    public double GetDailyUpkeepContribution()
    {
        return 25; // +25 silver daily upkeep
    }

    public string GetDailyUpkeepContributionDesc()
    {
        return "+25 - My Comp\n";
    }

    public double GetDailyIncomeContribution()
    {
        return 0; // no income contribution
    }

    public string GetDailyIncomeContributionDesc()
    {
        return null;
    }
}
```

**Discovery**: `WorldSettlementFC.GetTotalUpkeep()` and `GetTotalIncome()` iterate `AllComps` for `IProfitContributor`.

**Caching**: Results are cached per settlement. Automatically invalidated after lifecycle events. For changes outside lifecycle callbacks:
```csharp
((WorldSettlementFC)parent).DirtyProfitCache();
```

### ISettlementPostLoadInit

Runs initialization that depends on fully-rebuilt settlement state after a save is loaded.

```csharp
public class MyComp : WorldObjectComp, ISettlementPostLoadInit
{
    public void PostSettlementLoadInit(WorldSettlementFC settlement)
    {
        // Safe to read computed stats, resource production, etc.
    }
}
```

**Discovery**: Called during `FinalizeInit` after stat modifiers and resource caches are rebuilt. Use this when your comp needs to read computed values that aren't available in `PostExposeData`.

---

## Existing Base Mod Comps

The base mod attaches three comps to each settlement. Be aware of what they already provide:

| Comp | Purpose |
|------|---------|
| `WorldObjectComp_SettlementOpenWindow` | Adds the gizmo to open the settlement window. |
| `WorldObjectComp_SettlementMilitary` | All military operations — battles, squad deployment, defense. Provides military gizmos and caravan interactions. |
| `WorldObjectComp_SettlementBuildings` | Manages all buildings in the settlement — construction, demolition, upkeep, building comps. |
