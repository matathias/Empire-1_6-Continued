# Event System

Empire's event system handles both scripted events (triggered by code at specific moments) and random events (selected periodically based on weighted conditions). Events can chain together, present player choices, give rewards, apply temporary stat modifiers, and trigger custom C# logic.

---

## Event Lifecycle

```
Creation → Added to faction.events → Tick down → Trigger → Resolution → Cleanup
```

### 1. Creation

Events are created via `FCEventMaker`:

| Method | Purpose |
|--------|---------|
| `MakeEvent(FCEventDef)` | Create a basic event instance from a def. Sets `timeTillTrigger` from the def. |
| `MakeRandomEvent(FCEventDef, settlements)` | Create an event with random settlement selection based on `rangeSettlementsAffected`. |
| `ReturnRandomEvent()` | Select a random event def from all valid random events, weighted by `weight`. |

### 2. Ticking

Events are stored in `FactionFC.events`. Each game tick, `ProcessEvents()` checks if any event's `timeTillTrigger` has been reached. Events with `timeTillTrigger = -1` resolve immediately on creation.

### 3. Resolution

When an event's timer expires, `ProcessEvents()` runs the following sequence:

1. **Handler extension check**: If the event def has an `FCEventHandlerExtension`, calls `ResolveEvent(evt, faction)`. If it returns `true`, the built-in switch-case resolution is skipped.
2. **Built-in resolution**: For base mod events, a switch on `def.defName` handles specific logic (settle colony, deliver taxes, construct building, etc.).
3. **Stat modifier cleanup**: Stat modifiers from the event are removed from affected settlements.
4. **Loot distribution**: If `def.loot` is set, items are given to the player.
5. **Random rewards**: If `def.randomThingValue > 0`, random items are generated using the `def.randomThingRewardDef` configuration.
6. **Prosperity loss**: `def.prosperityLost` is applied.
7. **Options window**: If `def.options` is non-empty, the options window opens for player choice.
8. **Event chains**: If `def.eventFollows` is true, the follow-up event is created and added.
9. **RunAction**: `def.GetModExtension<FCEventHandlerExtension>()?.OnEventTriggered(evt)` is called. This always runs, regardless of whether `ResolveEvent` returned true.

---

## Random Event Selection

Random events are a subset of `FCEventDef` where `isRandomEvent = true`. The random event system periodically calls `ReturnRandomEvent()`, which:

1. Filters all `FCEventDef`s through `IsValidRandomEvent()`:
   - `isRandomEvent` must be true
   - `weight` must be > 0
   - Player wealth must meet `requiredWealth`
   - Faction average happiness must be within `minimumHappiness`–`maximumHappiness`
   - Same for loyalty, unrest, and prosperity ranges
   - If `requiredResource` is set, at least one settlement must produce it
   - If `applicableBiomes` or `restrictedBiomes` is set, at least one settlement must have an allowed biome
   - No `incompatibleEvents` can be currently active
2. Builds a weighted list (each def appears `weight` times)
3. Picks a random element

### Random Event Fields

| Field | Type | Description |
|-------|------|-------------|
| `isRandomEvent` | `bool` | Must be `true` for random selection. |
| `weight` | `int` | Selection weight. Higher = more likely. |
| `requiredWealth` | `int` | Minimum player wealth. |
| `minimumHappiness` / `maximumHappiness` | `int` | Required happiness range (0-100). |
| `minimumLoyalty` / `maximumLoyalty` | `int` | Required loyalty range (0-100). |
| `minimumUnrest` / `maximumUnrest` | `int` | Required unrest range (0-100). |
| `minimumProsperity` / `maximumProsperity` | `int` | Required prosperity range (0-100). |
| `requiredResource` | `ResourceTypeDef` | At least one settlement must produce this. |
| `applicableBiomes` | `List<string>` | Biome allowlist (BiomeDef defNames). Only settlements in these biomes are eligible. Empty = all. |
| `restrictedBiomes` | `List<string>` | Biome blocklist (BiomeDef defNames). Settlements in these biomes are excluded. Ignored if `applicableBiomes` is set. |
| `incompatibleEvents` | `List<FCEventDef>` | Cannot fire while these events are active. |
| `rangeSettlementsAffected` | `IntRange` | How many settlements are affected. `(0,0)` = faction-wide. |

---

## Event Chains

Events can link to follow-up events:

| Field | Type | Description |
|-------|------|-------------|
| `eventFollows` | `bool` | If true, a follow-up event fires on resolution. |
| `followingEvent` | `FCEventDef` | The primary follow-up event. |
| `followingEvent2` | `FCEventDef` | Alternative follow-up (used with `splitEventFollows`). |
| `splitEventFollows` | `bool` | If true, randomly chooses between `followingEvent` and `followingEvent2`. |
| `splitEventChance` | `int` | Percent chance (0-100) of `followingEvent`. Remainder goes to `followingEvent2`. |
| `settlementsCarryOver` | `bool` | If true, affected settlements carry over to the follow-up event. |

---

## Player Options

Events can present choices to the player via `FCOptionDef`:

| Field | Type | Description |
|-------|------|-------------|
| `baseChanceOfSuccess` | `float` | Success probability (0-100). |
| `silverCost` | `int` | Silver cost to choose this option. |
| `parentEvent` | `FCEventDef` | The event this option belongs to. |
| `successEvent` | `FCEventDef` | Event fired on success (null = nothing). |
| `failEvent` | `FCEventDef` | Event fired on failure (null = nothing). |

When an event with options resolves, it opens the options window. The player picks an option, rolls against `baseChanceOfSuccess`, and the corresponding success/fail event fires.

---

## FCEventHandlerExtension

The primary C# hook for custom event behavior. Attach it to an `FCEventDef` via `modExtensions`.

```xml
<modExtensions>
    <li Class="YourNamespace.MyEventHandler" />
</modExtensions>
```

### Virtual Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `OnEventQueued` | `void OnEventQueued(FCEvent evt, FactionFC faction)` | Called once when the event is enqueued. The default applies the def's temporary + permanent stat modifiers to the targeted settlements (or all settlements if untargeted). |
| `OnEventExpired` | `void OnEventExpired(FCEvent evt, FactionFC faction)` | Called once when the event leaves the queue. The default removes the temporary modifiers added at queue time and subtracts `def.prosperityLost` (permanent modifiers are kept). |
| `ResolveEvent` | `bool ResolveEvent(FCEvent evt, FactionFC faction)` | Called when the event triggers. Return `true` to skip built-in resolution. Standard post-processing (loot, stat cleanup, chains, options) still runs regardless. |
| `OnEventTriggered` | `void OnEventTriggered(FCEvent evt)` | Called after **all** processing is complete (loot, stats, chains). Always called, even if `ResolveEvent` returned true. |
| `ShouldCancelOnSettlementRemoval` | `bool ShouldCancelOnSettlementRemoval(FCEvent evt, WorldSettlementFC settlement)` | Called when a settlement is removed. Return `true` to cancel this event. Default: `false`. |

`OnEventQueued`/`OnEventExpired` bracket the event's queued lifetime (stat-modifier apply/remove), whereas `ResolveEvent`/`OnEventTriggered` fire at the moment the timer expires. See the Option Display Hooks in [DefModExtensions](def-mod-extensions.md#fceventhandlerextension) for the dynamic option label/chance/availability methods.

### Resolution Flow Diagram

```
Event timer expires
    │
    ▼
handler.ResolveEvent(evt, faction)?
    │
    ├── true:  skip built-in resolution
    │
    ├── false: run built-in resolution (switch on defName)
    │
    ▼
Remove stat modifiers from affected settlements
    │
    ▼
Distribute loot (def.loot)
    │
    ▼
Generate random rewards (def.randomThingValue + def.randomThingRewardDef)
    │
    ▼
Apply prosperity loss (def.prosperityLost)
    │
    ▼
Show options window (if def.options is non-empty)
    │
    ▼
Create follow-up event (if def.eventFollows)
    │
    ▼
handler.OnEventTriggered(evt)  ← always called
```

---

## Event Categories

`FCEventCategoryDef` assigns a color and sort order to events for UI display. Define your own to group your events visually.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `color` | `Color` | `(0.65, 0.65, 0.65, 1)` | UI accent color. |
| `displayOrder` | `int` | `100` | Sort order in filter lists. |

See [ExampleDefs/FCEventCategoryDef.xml](ExampleDefs/FCEventCategoryDef.xml).

---

## Creating Events from Code

To create and fire a scripted event from your submod:

```csharp
FCEventDef myEventDef = DefDatabase<FCEventDef>.GetNamed("MyCustomEvent");
FCEvent evt = FCEventMaker.MakeEvent(myEventDef);
if (evt != null)
{
    // Optionally set settlement locations
    evt.settlementTraitLocations = new List<WorldSettlementFC> { targetSettlement };

    FindFC.EventManager.AddEvent(evt);
}
```

The event will tick down and resolve normally through the standard pipeline.
