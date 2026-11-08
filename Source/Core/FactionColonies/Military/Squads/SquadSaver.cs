using LudeonTK;
using System.IO;
using System.Xml;
using UnityEngine;

namespace FactionColonies;

[StaticConstructorOnStartup]
public static class FactionColoniesMilitary
{
    private static List<SavedUnitFC> savedUnits = [];
    private static List<SavedSquadFC> savedSquads = [];
    private static List<SavedFireSupportFC> savedFireSupports = [];
    public static IEnumerable<SavedSquadFC> SavedSquads => savedSquads;
    public static IEnumerable<SavedUnitFC> SavedUnits => savedUnits;
    public static IEnumerable<SavedFireSupportFC> SavedFireSupports => savedFireSupports;

    public static string EmpireConfigFolderPath;
    public static string EmpireMilitaryUnitFolder;
    public static string EmpireMilitarySquadFolder;
    public static string EmpireMilitaryFireSupportFolder;

    static FactionColoniesMilitary()
    {
        EmpireConfigFolderPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "Empire");
        EmpireMilitarySquadFolder = Path.Combine(EmpireConfigFolderPath, "Squads");
        EmpireMilitaryUnitFolder = Path.Combine(EmpireConfigFolderPath, "Units");
        EmpireMilitaryFireSupportFolder = Path.Combine(EmpireConfigFolderPath, "FireSupports");
        if (!Directory.Exists(EmpireConfigFolderPath) ||
            !Directory.Exists(EmpireMilitarySquadFolder) ||
            !Directory.Exists(EmpireMilitaryUnitFolder) ||
            !Directory.Exists(EmpireMilitaryFireSupportFolder))
        {
            Directory.CreateDirectory(EmpireConfigFolderPath);
            Directory.CreateDirectory(EmpireMilitarySquadFolder);
            Directory.CreateDirectory(EmpireMilitaryUnitFolder);
            Directory.CreateDirectory(EmpireMilitaryFireSupportFolder);
        }

        Read();
    }

    public static SavedSquadFC GetSquad(string name) => savedSquads.FirstOrFallback(s => s.name == name);
    public static SavedUnitFC GetUnit(string name) => savedUnits.FirstOrFallback(u => u.name == name);
    public static SavedFireSupportFC GetFireSupport(string name) => savedFireSupports.FirstOrFallback(f => f.name == name);

    public static void RemoveSquad(string name)
    {
        savedSquads.RemoveAll(squad => squad.name == name);
        File.Delete(GetSquadPath(name));
    }

    public static void RemoveSquad(SavedSquadFC squad)
    {
        savedSquads.Remove(squad);
        File.Delete(GetSquadPath(squad.name));
    }

    public static void RemoveUnit(string name)
    {
        savedUnits.RemoveAll(unit => unit.name == name);
        File.Delete(GetUnitPath(name));
    }

    public static void RemoveUnit(SavedUnitFC unit)
    {
        savedUnits.Remove(unit);
        File.Delete(GetUnitPath(unit.name));
    }

    public static void RemoveFireSupport(string name)
    {
        savedFireSupports.RemoveAll(f => f.name == name);
        File.Delete(GetFireSupportPath(name));
    }

    public static void RemoveFireSupport(SavedFireSupportFC fireSupport)
    {
        savedFireSupports.Remove(fireSupport);
        File.Delete(GetFireSupportPath(fireSupport.name));
    }

    [DebugAction("Empire", "Reload Saved Military", allowedGameStates = AllowedGameStates.Playing)]
    public static void Read()
    {
        if (Scribe.mode != LoadSaveMode.Inactive)
            throw new Exception("Empire - Attempt to load saved military while scribe is active");

        savedSquads.Clear();
        savedUnits.Clear();
        savedFireSupports.Clear();
        LoadFolder(EmpireMilitarySquadFolder, savedSquads, ResolveSavedType<SavedSquadFC>, "squad");
        LoadFolder(EmpireMilitaryUnitFolder, savedUnits, ResolveSavedType<SavedUnitFC>, "unit");
        LoadFolder(EmpireMilitaryFireSupportFolder, savedFireSupports, () => new SavedFireSupportFC(), "fire support");
    }

    /* Loads every template file in a folder into its cache. create runs after the file is
     * opened, so it can peek at the XML to pick the concrete type. A file that fails to load is
     * logged and skipped. */
    private static void LoadFolder<T>(string folder, List<T> cache, Func<T> create, string label)
        where T : IExposable
    {
        foreach (string path in Directory.EnumerateFiles(folder))
        {
            try
            {
                Scribe.loader.InitLoading(path);
                T template = create();
                template.ExposeData();
                cache.Add(template);
            }
            catch (Exception e)
            {
                LogUtil.Error($"Failed to load {label} at path {path} due to exception: {e.Message}");
            }
            finally
            {
                Scribe.loader.FinalizeLoading();
            }
        }
    }

    /* Peek at the active Scribe XML for a "savedType" discriminator and instantiate
       the matching subclass. Falls back to the base type if absent or unknown (missing mod). */
    private static T ResolveSavedType<T>() where T : class, new()
    {
        string savedType = null;
        Scribe_Values.Look(ref savedType, "savedType");
        if (string.IsNullOrEmpty(savedType)) return new T();
        Type type = GenTypes.GetTypeInAnyAssembly(savedType);
        if (type is null || !typeof(T).IsAssignableFrom(type))
        {
            LogUtil.Warning($"Unknown saved type '{savedType}' (mod unloaded?); loading as base {typeof(T).Name}");
            return new T();
        }
        return (T)Activator.CreateInstance(type);
    }

    // Names are player-entered: replace characters the OS rejects in file names (including path
    // separators, so a name cannot point outside the folder). Names that were already valid map
    // to the same file as before.
    private static string SafeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
        }
        return new string(chars);
    }

    public static string GetUnitPath(string name) => Path.Combine(EmpireMilitaryUnitFolder, $"{SafeFileName(name)}.xml");
    public static string GetSquadPath(string name) => Path.Combine(EmpireMilitarySquadFolder, $"{SafeFileName(name)}.xml");
    public static string GetFireSupportPath(string name) => Path.Combine(EmpireMilitaryFireSupportFolder, $"{SafeFileName(name)}.xml");

    public static void SaveSquad(SavedSquadFC squad) =>
        SaveTemplate(squad, squad.name, GetSquadPath(squad.name), "squad", "squad", savedSquads, s => s.name);

    public static void SaveUnit(SavedUnitFC unit) =>
        SaveTemplate(unit, unit.name, GetUnitPath(unit.name), "unit", "unit", savedUnits, u => u.name);

    public static void SaveFireSupport(SavedFireSupportFC fireSupport) =>
        SaveTemplate(fireSupport, fireSupport.name, GetFireSupportPath(fireSupport.name), "fireSupport",
            "fire support", savedFireSupports, f => f.name);

    /* Writes one template to its own file under a version header, then replaces any same-named
     * entry in the cache. The cache is left unchanged when the write fails. */
    private static void SaveTemplate<T>(T template, string name, string path, string rootElement,
        string label, List<T> cache, Func<T, string> nameOf) where T : IExposable
    {
        if (Scribe.mode != LoadSaveMode.Inactive)
        {
            throw new Exception($"Empire - Attempt to save {label} while scribe is active");
        }

        bool saved = false;
        try
        {
            Scribe.saver.InitSaving(path, rootElement);
            int version = 0;
            Scribe_Values.Look(ref version, "version");
            template.ExposeData();
            saved = true;
        }
        catch (Exception e)
        {
            LogUtil.Error($"Failed to save {label} {name} {e}");
        }
        finally
        {
            Scribe.saver.FinalizeSaving();
        }
        if (!saved) return;
        cache.RemoveAll(t => nameOf(t) == name);
        cache.Add(template);
    }

    // Snapshot first: each save removes and re-adds its entry in the list being iterated.
    public static void SaveAllUnits() => savedUnits.ToList().ForEach(SaveUnit);
    public static void SaveAllSquads() => savedSquads.ToList().ForEach(SaveSquad);
}

public class SavedUnitFC : IExposable
{
    public string name;
    public List<SavedAnimal> animals;
    public PawnKindDef mount;
    public PawnKindDef pawnKind;
    public List<SavedThing> weapons;
    public List<SavedThing> apparel;
    public List<SavedThing> inventory;
    public List<SavedImplant> implants;
    public int psylinkLevel;
    public List<SavedPsycast> psycasts;
    public bool isMechanitor;
    public List<SavedMech> mechs;
    public List<MechWorkModeDef> mechGroupWorkModes;
    public XenotypeDef xenotype;
    public string customXenotypeName;
    public Gender? forcedGender;
    public List<PermanentStatModifier> statModifiers;

    // Set during load when defs fail to resolve (e.g. mod removed). Not serialized.
    public bool isDegraded;
    public List<string> missingDefs;

    public SavedUnitFC() { }

    public SavedUnitFC(MilUnitFC unit)
    {
        name = unit.name;
        weapons = new List<SavedThing>(unit.weapons);
        apparel = new List<SavedThing>(unit.apparel);
        inventory = new List<SavedThing>(unit.inventory ?? []);
        implants = new List<SavedImplant>(unit.implants ?? []);
        psylinkLevel = unit.psylinkLevel;
        psycasts = new List<SavedPsycast>(unit.psycasts ?? []);
        isMechanitor = unit.isMechanitor;
        mechs = new List<SavedMech>(unit.mechs ?? []);
        mechGroupWorkModes = new List<MechWorkModeDef>(unit.mechGroupWorkModes ?? []);
        animals = new List<SavedAnimal>(unit.animals ?? []);
        mount = unit.mount;
        pawnKind = unit.pawnKind;
        xenotype = unit.xenotype;
        customXenotypeName = unit.customXenotypeName;
        forcedGender = unit.forcedGender;
        statModifiers = unit.statModifiers?.Select(m => m.Clone()).ToList() ?? [];
    }

    public MilUnitFC CreateMilUnit()
    {
        // Race weight controls random race selection only - an exported unit's race is
        // an explicit player choice and must be respected on import, even at weight 0.
        PawnKindDef resolvedKind = pawnKind;

        if (resolvedKind is null)
        {
            resolvedKind = FindFC.EmpireFaction?.RandomPawnKind()
                ?? PawnKindDefOf.Colonist;
            LogUtil.Warning($"Saved unit '{name}' has no pawnKind (mod removed?), "
                + $"using {resolvedKind.defName}");
        }

        MilUnitFC unit = MilTemplateFactory.CreateUnit(false);
        unit.name = name;
        // Drop companion rows whose kind failed to resolve (mod removed); mount loads as null and
        // is simply unassigned. Mirrors the mechs/psycasts filtering below.
        unit.animals = animals?.Where(a => a.kind is not null).ToList() ?? [];
        unit.mount = mount;
        unit.pawnKind = resolvedKind;
        unit.xenotype = xenotype;
        unit.customXenotypeName = customXenotypeName;
        unit.forcedGender = forcedGender;
        unit.weapons = weapons?.Where(w => w.thing is not null).ToList() ?? [];
        unit.apparel = apparel?.Where(a => a.thing is not null).ToList() ?? [];
        unit.inventory = inventory?.Where(i => i.thing is not null).ToList() ?? [];
        unit.implants = implants?.Where(im => im.IsValid).ToList() ?? [];
        unit.psylinkLevel = psylinkLevel;
        // Keep def-backed entries (psycast/focus) and aggregate entries that carry a kind
        // (e.g. stat upgrades, which have no defName).
        unit.psycasts = psycasts?.Where(a => a.IsValid()).ToList() ?? [];
        unit.isMechanitor = isMechanitor;
        unit.mechs = mechs?.Where(m => m.kind is not null).ToList() ?? [];
        unit.mechGroupWorkModes = mechGroupWorkModes is not null
            ? new List<MechWorkModeDef>(mechGroupWorkModes)
            : [];
        unit.statModifiers = statModifiers?.Select(m => m.Clone()).ToList() ?? [];

        unit.LoadFromSaved(this);
        unit.ChangeTick();
        unit.UpdateEquipmentTotalCost();

        return unit;
    }

    public MilUnitFC Import()
    {
        FactionFC fc = FindFC.FactionComp;
        MilUnitFC unit = this.CreateMilUnit();
        fc.military.units.Add(unit);
        return unit;
    }

    public virtual void ExposeData()
    {
        // Capture XML parent before any collection loading can shift the cursor.
        XmlNode xmlParent = (Scribe.mode == LoadSaveMode.LoadingVars)
            ? Scribe.loader.curXmlParent : null;

        /* Polymorphic type discriminator — saved as concrete type, ignored on load
           (ResolveSavedType handles type selection before ExposeData runs). */
        string savedType = GetType().FullName;
        Scribe_Values.Look(ref savedType, "savedType");

        Scribe_Values.Look(ref name, "name");
        Scribe_Collections.Look(ref animals, "animals", LookMode.Deep);
        Scribe_Defs.Look(ref mount, "mount");
        // Migrate pre-multi-animal exports: the old single companion lived under "animal".
        PawnKindDef legacyAnimal = null;
        Scribe_Defs.Look(ref legacyAnimal, "animal");
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            animals ??= [];
            if (animals.Count == 0 && legacyAnimal is not null) animals.Add(new SavedAnimal(legacyAnimal, 1));
        }
        Scribe_Defs.Look(ref pawnKind, "pawnKind");
        Scribe_Defs.Look(ref xenotype, "xenotype");
        Scribe_Values.Look(ref customXenotypeName, "customXenotypeName");
        Scribe_Collections.Look(ref weapons, "weapons", LookMode.Deep);
        Scribe_Collections.Look(ref apparel, "apparel", LookMode.Deep);
        Scribe_Collections.Look(ref inventory, "inventory", LookMode.Deep);
        Scribe_Collections.Look(ref implants, "implants", LookMode.Deep);
        Scribe_Values.Look(ref psylinkLevel, "psylinkLevel", 0);
        Scribe_Collections.Look(ref psycasts, "psycasts", LookMode.Deep);
        Scribe_Values.Look(ref isMechanitor, "isMechanitor", false);
        Scribe_Collections.Look(ref mechs, "mechs", LookMode.Deep);
        Scribe_Collections.Look(ref mechGroupWorkModes, "mechGroupWorkModes", LookMode.Def);
        Scribe_Collections.Look(ref statModifiers, "statModifiers", LookMode.Deep);

        // forcedGender nullable — save only if set
        bool hasGender = forcedGender.HasValue;
        Gender genderVal = forcedGender ?? Gender.None;
        Scribe_Values.Look(ref hasGender, "hasForcedGender", false);
        if (hasGender)
            Scribe_Values.Look(ref genderVal, "forcedGender", Gender.None);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            forcedGender = hasGender ? genderVal : (Gender?)null;

        if (xmlParent is not null)
        {
            ValidateAfterLoad(xmlParent);
        }
    }

    private void ValidateAfterLoad(XmlNode xmlParent)
    {
        List<string> missing = [];

        CheckDef(xmlParent, "pawnKind", pawnKind, missing);
        CheckDef(xmlParent, "mount", mount, missing);
        CheckDef(xmlParent, "xenotype", xenotype, missing);

        int nullAnimals = animals?.Count(a => a.kind is null) ?? 0;
        if (nullAnimals > 0) missing.Add($"{nullAnimals} companion animal(s)");
        int nullWeapons = weapons?.Count(w => w.thing is null) ?? 0;
        int nullApparel = apparel?.Count(a => a.thing is null) ?? 0;
        int nullInventory = inventory?.Count(i => i.thing is null) ?? 0;
        int nullImplants = implants?.Count(im => !im.IsValid) ?? 0;
        if (nullWeapons > 0) missing.Add($"{nullWeapons} weapon(s)");
        if (nullApparel > 0) missing.Add($"{nullApparel} apparel item(s)");
        if (nullInventory > 0) missing.Add($"{nullInventory} inventory item(s)");
        if (nullImplants > 0) missing.Add($"{nullImplants} implant(s)");

        if (missing.Count > 0)
        {
            isDegraded = true;
            missingDefs = missing;
            LogUtil.Warning($"Saved unit '{name}' references missing defs (unloaded mod?): "
                + string.Join(", ", missing));
        }
    }

    private static void CheckDef(XmlNode parent, string label, Def resolved, List<string> missing)
    {
        XmlNode node = parent?[label];
        if (node is null) return;
        string raw = node.InnerText;
        if (string.IsNullOrEmpty(raw) || raw == "null") return;
        if (resolved is null)
        {
            missing.Add($"{label}={raw}");
        }
    }
}

public class SavedSquadFC : IExposable
{
    public string name;
    public List<SavedUnitFC> unitTemplates = [];
    public List<int> units = new(30);
    public List<PermanentStatModifier> statModifiers;
    public bool IsDegraded => unitTemplates is not null && unitTemplates.Any(u => u.isDegraded);

    public SavedSquadFC() { }

    public SavedSquadFC(MilSquadFC squad)
    {
        name = squad.name;

        // Dont store blank units
        var squadTemplates = squad.Units.Distinct().Where(u => !u.isBlank).ToList();

        unitTemplates = squadTemplates.Select(unit => unit.ToSavedUnit()).ToList();
        units = squad.Units.Select(unit => squadTemplates.IndexOf(unit)).ToList();
        statModifiers = squad.statModifiers?.Select(m => m.Clone()).ToList() ?? [];
    }

    public MilSquadFC CreateMilSquad()
    {
        MilSquadFC squad = MilTemplateFactory.CreateSquad(true);
        squad.name = name;

        FactionFC fc = FindFC.FactionComp;

        var milUnits = unitTemplates.Select(unit => unit.CreateMilUnit()).ToList();

        foreach (int i in units)
        {
            if (i == -1)
                squad.AddUnit(fc.military.blankUnit);
            else
                squad.AddUnit(milUnits[i]);
        }

        squad.statModifiers = statModifiers?.Select(m => m.Clone()).ToList() ?? [];
        squad.LoadFromSaved(this);
        return squad;
    }
    public MilSquadFC Import()
    {
        FactionFC fc = FindFC.FactionComp;
        MilSquadFC squad = this.CreateMilSquad();
        foreach (MilUnitFC unit in squad.Units.Distinct().Where(unit => !unit.isBlank))
        {
            fc.military.units.Add(unit);
        }
        fc.military.squads.Add(squad);
        return squad;
    }

    public virtual void ExposeData()
    {
        /* Polymorphic type discriminator — saved as concrete type, ignored on load
           (ResolveSavedType handles type selection before ExposeData runs). */
        string savedType = GetType().FullName;
        Scribe_Values.Look(ref savedType, "savedType");

        Scribe_Values.Look(ref name, "name");
        Scribe_Collections.Look(ref unitTemplates, "unitTemplates", LookMode.Deep);
        Scribe_Collections.Look(ref units, "units", LookMode.Value);
        Scribe_Collections.Look(ref statModifiers, "statModifiers", LookMode.Deep);
    }
}

public class SavedFireSupportFC : IExposable
{
    public string name;
    public float accuracy;
    public List<ThingDef> projectiles;
    public string fireSupportType;

    // Set during load when defs fail to resolve (e.g. mod removed). Not serialized.
    public bool isDegraded;
    public List<string> missingDefs;

    public SavedFireSupportFC() { }

    public SavedFireSupportFC(MilitaryFireSupport fireSupport)
    {
        name = fireSupport.name;
        accuracy = fireSupport.accuracy;
        projectiles = fireSupport.projectiles is not null
            ? new List<ThingDef>(fireSupport.projectiles)
            : [];
        fireSupportType = fireSupport.fireSupportType;
    }

    public MilitaryFireSupport CreateFireSupport()
    {
        MilitaryFireSupport fs = new();
        fs.name = name;
        fs.accuracy = accuracy;
        fs.fireSupportType = fireSupportType;
        fs.projectiles = projectiles?.Where(p => p is not null).ToList() ?? [];
        fs.SetLoadID();
        return fs;
    }

    public MilitaryFireSupport Import()
    {
        MilitaryFireSupport fs = CreateFireSupport();
        FindFC.Military.fireSupportDefs.Add(fs);
        return fs;
    }

    public void ExposeData()
    {
        XmlNode xmlParent = (Scribe.mode == LoadSaveMode.LoadingVars)
            ? Scribe.loader.curXmlParent : null;

        Scribe_Values.Look(ref name, "name");
        Scribe_Values.Look(ref accuracy, "accuracy");
        Scribe_Values.Look(ref fireSupportType, "fireSupportType");
        Scribe_Collections.Look(ref projectiles, "projectiles", LookMode.Def);

        if (xmlParent is not null)
        {
            ValidateAfterLoad(xmlParent);
        }
    }

    private void ValidateAfterLoad(XmlNode xmlParent)
    {
        List<string> missing = [];

        XmlNode projNode = xmlParent["projectiles"];
        if (projNode is not null)
        {
            int xmlCount = 0;
            foreach (XmlNode li in projNode.ChildNodes)
            {
                if (li.Name != "li") continue;
                xmlCount++;
                string defName = li.InnerText;
                if (!string.IsNullOrEmpty(defName)
                    && DefDatabase<ThingDef>.GetNamedSilentFail(defName) is null)
                {
                    missing.Add(defName);
                }
            }
        }

        if (missing.Count > 0)
        {
            isDegraded = true;
            missingDefs = missing;
            LogUtil.Warning($"Saved fire support '{name}' references missing defs (unloaded mod?): "
                + string.Join(", ", missing));
        }
    }
}

public struct SavedThing : IExposable
{
    public ThingDef thing;
    public ThingDef stuff;
    public QualityCategory? quality; // null = not specified (future feature)
    public Color color;
    public bool hasColor;
    public int count; // stack count — always 1 for weapons/apparel, may be >1 for inventory

    public SavedThing(Thing t)
    {
        thing = t.def;
        stuff = t.Stuff;
        quality = t.TryGetQuality(out QualityCategory q) ? q : (QualityCategory?)null;
        color = Color.white;
        hasColor = false;
        count = Mathf.Max(1, t.stackCount);
    }

    public SavedThing(ThingDef thing, ThingDef stuff, QualityCategory? quality = null)
    {
        this.thing = thing;
        this.stuff = stuff;
        this.quality = quality;
        this.color = Color.white;
        this.hasColor = false;
        this.count = 1;
    }

    public SavedThing(ThingDef thing, ThingDef stuff, int count, QualityCategory? quality = null)
    {
        this.thing = thing;
        this.stuff = stuff;
        this.quality = quality;
        this.color = Color.white;
        this.hasColor = false;
        this.count = Mathf.Max(1, count);
    }

    /* Creates a single Thing capped to one stack. For inventory counts larger than
     * the item's stack limit, callers (e.g. SquadEquipmentTracker.EquipPawn) split
     * the total across multiple CreateThing calls. */
    public Thing CreateThing()
    {
        if (thing is null) return null;
        Thing t = ThingMaker.MakeThing(thing, stuff);
        if (quality.HasValue)
            t.TryGetComp<CompQuality>()?.SetQuality(quality.Value, null);
        t.stackCount = Mathf.Clamp(count <= 0 ? 1 : count, 1, Mathf.Max(1, thing.stackLimit));
        return t;
    }

    public float MarketValue =>
        thing is not null ? CraftUtil.ThingValue(thing, stuff, quality ?? QualityCategory.Normal) * Mathf.Max(1, count) : 0f;

    public void ExposeData()
    {
        Scribe_Defs.Look(ref thing, "thing");
        Scribe_Defs.Look(ref stuff, "stuff");
        Scribe_Values.Look(ref count, "count", 1);
        // quality is nullable — save only if set
        QualityCategory qualityVal = quality ?? QualityCategory.Normal;
        bool hasQuality = quality.HasValue;
        Scribe_Values.Look(ref hasQuality, "hasQuality", false);
        if (hasQuality)
        {
            Scribe_Values.Look(ref qualityVal, "quality", QualityCategory.Normal);
        }
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            quality = hasQuality ? qualityVal : (QualityCategory?)null;
        }

        // Apparel color override
        Scribe_Values.Look(ref hasColor, "hasColor", false);
        if (hasColor)
        {
            Scribe_Values.Look(ref color, "color", Color.white);
        }
        if (Scribe.mode == LoadSaveMode.LoadingVars && !hasColor)
        {
            color = Color.white;
        }
    }
}

/* A surgically-installed implant chosen for a unit design. Stored by (recipe, bodyPart,
 * occurrence index) so the concrete BodyPartRecord can be re-resolved against any pawn of
 * the unit's body via recipe.Worker.GetPartsToApplyOn (deterministic ordering). bodyPart is
 * null and bodyPartIndex is 0 for whole-body / non-targeted implants (recipe.targetsBodyPart
 * == false). Future psycast picking would live in a parallel list, not on this struct. */
public struct SavedImplant : IExposable
{
    public RecipeDef recipe;             // surgery install path (null for self-install items)
    public ThingDef selfInstallThing;    // self-install item with CompUseEffect_InstallImplant (null for surgery)
    public BodyPartDef bodyPart;
    public int bodyPartIndex;

    public SavedImplant(RecipeDef recipe, BodyPartDef bodyPart, int bodyPartIndex)
    {
        this.recipe = recipe;
        this.selfInstallThing = null;
        this.bodyPart = bodyPart;
        this.bodyPartIndex = bodyPartIndex;
    }

    public SavedImplant(ThingDef selfInstallThing, BodyPartDef bodyPart, int bodyPartIndex)
    {
        this.recipe = null;
        this.selfInstallThing = selfInstallThing;
        this.bodyPart = bodyPart;
        this.bodyPartIndex = bodyPartIndex;
    }

    /// <summary>True when this entry resolved to a real install source (surgery recipe or
    /// self-install item). Used to filter out entries whose def failed to load.</summary>
    public bool IsValid => recipe is not null || selfInstallThing is not null;

    public void ExposeData()
    {
        Scribe_Defs.Look(ref recipe, "recipe");
        Scribe_Defs.Look(ref selfInstallThing, "selfInstallThing");
        Scribe_Defs.Look(ref bodyPart, "bodyPart");
        Scribe_Values.Look(ref bodyPartIndex, "bodyPartIndex", 0);
    }
}

/* A point-purchase chosen for a unit design within a psycast system. Stored by strings rather
 * than Def references so the core assembly never has to reference a foreign psycast-def type
 * (e.g. VEF.Abilities.AbilityDef): a template designed with VPE loads cleanly even when VPE is
 * absent — the entry simply resolves to no provider via PsycastSystemRegistry.ByKey and is
 * skipped. systemKey is the owning IPsycastSystemProvider.Key ("Vanilla" / "VPE").
 *
 * The core treats the remaining fields opaquely — only the owning provider interprets them:
 *   - kind: provider-defined entry type (null/"" == a psycast, for back-compat with
 *     older saves). VPE also uses "MeditationFocus" and "StatUpgrade".
 *   - psycastDef: the defName for def-backed entries (psycast or meditation focus); empty for
 *     aggregate entries like stat upgrades.
 *   - count: multiplicity for aggregate entries (e.g. number of psycaster-stat points); 0 is
 *     treated as 1. Paths (VPE) are re-derived from the psycast at apply time. */
public struct SavedPsycast : IExposable
{
    public string systemKey;
    public string psycastDef;
    public string kind;
    public int count;

    public SavedPsycast(string systemKey, string psycastDef)
        : this(systemKey, psycastDef, null, 1) { }

    public SavedPsycast(string systemKey, string psycastDef, string kind, int count)
    {
        this.systemKey = systemKey;
        this.psycastDef = psycastDef;
        this.kind = kind;
        this.count = count;
    }

    public bool IsInvalid()
    {
        return psycastDef.NullOrEmpty() && kind.NullOrEmpty();
    }
    public bool IsValid()
    {
        return !IsInvalid();
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref systemKey, "systemKey");
        Scribe_Values.Look(ref psycastDef, "psycastDef");
        Scribe_Values.Look(ref kind, "kind");
        Scribe_Values.Look(ref count, "count", 0);
    }
}

/* A mech assignment chosen for a mechanitor unit design: a controllable mechanoid PawnKindDef
 * and how many of it to bond to the mechanitor at deploy time. Stored as (kind, count) so a
 * single row can represent multiple identical mechs. A kind that fails to resolve (mod removed,
 * Biotech absent) loads as null and is filtered out in SavedUnitFC.CreateMilUnit — the same
 * graceful-degradation contract as SavedImplant/SavedPsycast. */
public struct SavedMech : IExposable
{
    public PawnKindDef kind;
    public int count;
    public int group;   // 0-based mechanitor control-group index

    public SavedMech(PawnKindDef kind, int count) : this(kind, count, 0) { }

    public SavedMech(PawnKindDef kind, int count, int group)
    {
        this.kind = kind;
        this.count = Mathf.Max(1, count);
        this.group = Mathf.Max(0, group);
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref kind, "kind");
        Scribe_Values.Look(ref count, "count", 1);
        Scribe_Values.Look(ref group, "group", 0);
    }
}

/* A companion-animal assignment chosen for a unit design: an animal PawnKindDef and how many of
 * it to spawn alongside the merc. Stored as (kind, count) so a single row can represent several
 * identical animals (mirrors SavedMech minus the mech-only group/work-mode). A kind that fails to
 * resolve (mod removed) loads as null and is filtered out in SavedUnitFC.CreateMilUnit — the same
 * graceful-degradation contract as SavedMech/SavedImplant. The companion total is capped by
 * FCSettings.maxAnimalSubpawns at add time. The single rideable mount is a separate field, not a
 * SavedAnimal. */
public struct SavedAnimal : IExposable
{
    public PawnKindDef kind;
    public int count;

    public SavedAnimal(PawnKindDef kind, int count)
    {
        this.kind = kind;
        this.count = Mathf.Max(1, count);
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref kind, "kind");
        Scribe_Values.Look(ref count, "count", 1);
    }
}
