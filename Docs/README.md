# Empire Submod Documentation

Empire exposes a layered extensibility system designed for submods to add content without Harmony patches in most cases. This documentation covers every extension point available.

---

## Documentation Index

| Guide | What it covers                                                                                  |
|-------|-------------------------------------------------------------------------------------------------|
| [Getting Started](getting-started.md) | Empire-specific conventions, access points, registration patterns                               |
| [XML Def Types](xml-defs.md) | The custom def types — fields, defaults, cross-references                                    |
| [Stat & Production System](stat-system.md) | FCStatDef, aggregation pipeline, resource production formula                                    |
| [Interfaces & Registries](interfaces-and-registries.md) | C# extension interfaces and their static registries — method signatures, invocation timing      |
| [DefModExtensions](def-mod-extensions.md) | DefModExtension classes + extension interface for events, buildings, resources, settlements, policies, tile features |
| [Abstract Base Classes](abstract-base-classes.md) | FCPolicyBehavior, SettlementBuildingComp, MilitaryJobHandler                                    |
| [Settlement Comps](worldobject-comps.md) | WorldObjectComp pattern for per-settlement extensibility in Empire                              |
| [Event System](event-system.md) | Event lifecycle, chains, options, handler extensions                                            |

Annotated XML examples for every def type are in [ExampleDefs/](ExampleDefs/).

---

## What do you want to do?

| Goal | Start here |
|------|-----------|
| Add new buildings, events, resources, or policies (XML only) | [XML Def Types](xml-defs.md) |
| Define a new resource type | [XML Def Types — ResourceTypeDef](xml-defs.md#resourcetypedef) + [Stat System](stat-system.md) |
| Define a new settlement type | [XML Def Types — WorldSettlementDef](xml-defs.md#worldsettlementdef) + [DefModExtensions — SettlementTypeExtension](def-mod-extensions.md#settlementtypeextension) |
| Add a tab to the settlement window | [Settlement Comps](worldobject-comps.md) (ISettlementWindowOverview) |
| Add a tab to the main Empire window | [Interfaces & Registries](interfaces-and-registries.md#imaintabwindowoverview) |
| Modify resource production dynamically | [Settlement Comps](worldobject-comps.md) (IResourceProductionModifier) + [Stat System](stat-system.md) |
| Modify stats dynamically | [Settlement Comps](worldobject-comps.md) (IStatModifierProvider) + [Stat System](stat-system.md) |
| Hook into settlement/building/military lifecycle | [Interfaces & Registries](interfaces-and-registries.md#ilifecycleparticipant) |
| Intercept or modify tax collection | [Interfaces & Registries](interfaces-and-registries.md#itaxtickparticipant) |
| Modify battle outcomes | [Interfaces & Registries](interfaces-and-registries.md#ibattlemodifier) |
| Restrict defense assignments or squad assignments | [Interfaces & Registries](interfaces-and-registries.md#idefensevalidator) |
| Validate or restrict settlement founding | [Interfaces & Registries](interfaces-and-registries.md#isettlementfoundingvalidator) |
| Influence which settlements get raided | [Interfaces & Registries](interfaces-and-registries.md#iraidweightprovider) |
| Contribute road-network nodes (e.g. external outposts) | [Interfaces & Registries](interfaces-and-registries.md#iroadnodeprovider) |
| Intercept silver payments | [Interfaces & Registries](interfaces-and-registries.md#isilverpaymentmodifier) |
| Run daily faction-wide logic after production accrues | [Interfaces & Registries](interfaces-and-registries.md#idailyaccrualparticipant) |
| Redirect or handle tax delivery | [Interfaces & Registries](interfaces-and-registries.md#itaxdeliveryinterceptor) |
| Adjust a mercenary squad's combat power | [Interfaces & Registries](interfaces-and-registries.md#isquadpowermodifier) |
| Customize off-map mercenary auto-tending | [Interfaces & Registries](interfaces-and-registries.md#imercautotendprovider) |
| Filter animal kinds in the unit designer | [Interfaces & Registries](interfaces-and-registries.md#ianimalpickerfilter) |
| Add a section to the squad inspection window | [Interfaces & Registries](interfaces-and-registries.md#isquadinspectionsection) |
| Create a custom policy with procedural logic | [Abstract Base Classes — FCPolicyBehavior](abstract-base-classes.md#fcpolicybehavior) |
| Create a building with custom C# behavior | [Abstract Base Classes — SettlementBuildingComp](abstract-base-classes.md#settlementbuildingcomp) |
| Create a custom military operation | [Abstract Base Classes — MilitaryJobHandler](abstract-base-classes.md#militaryjobhandler) |
| Add custom event resolution logic | [DefModExtensions — FCEventHandlerExtension](def-mod-extensions.md#fceventhandlerextension) + [Event System](event-system.md) |
| Add a custom resource filter, production extension, or pool | [DefModExtensions — Resource Extensions](def-mod-extensions.md#resourcefilterextension) |
| Add building filter buttons to the building UI | [Interfaces & Registries](interfaces-and-registries.md#buildingfilter--buildingfilterregistry) |
| Make external world objects raidable by Empire | [Interfaces & Registries](interfaces-and-registries.md#iraidtarget) |
| Register external auto-defenders for settlements | [Interfaces & Registries](interfaces-and-registries.md#iautodefender) |
| Display external entries in the military tab | [Interfaces & Registries](interfaces-and-registries.md#imilitarytabentry) |
| Inject external tithe budget into a settlement | [Settlement Comps](worldobject-comps.md) (ITitheBudgetModifier) |
| Contribute upkeep or income to a settlement | [Settlement Comps](worldobject-comps.md) (IProfitContributor) |
| Run initialization after settlement loads | [Settlement Comps](worldobject-comps.md) (ISettlementPostLoadInit) |
| Add custom sections to the building detail panel | [DefModExtensions](def-mod-extensions.md#ibuildingdetailsection) |
| Add resource bonuses to tile mutators or landmarks | [DefModExtensions — TileMutatorResourceExtension](def-mod-extensions.md#tilemutatorresourceextension) |
| Define tech level progression gates | [XML Def Types — TechProgressionDef](xml-defs.md#techprogressiondef) |
