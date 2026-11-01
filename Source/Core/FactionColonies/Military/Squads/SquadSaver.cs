using FactionColonies.util;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    [StaticConstructorOnStartup]
    public static class FactionColoniesMilitary
    {
        private static List<SavedUnitFC> savedUnits = new List<SavedUnitFC>();
        private static List<SavedSquadFC> savedSquads = new List<SavedSquadFC>();
        private static List<SavedFireSupportFC> savedFireSupports = new List<SavedFireSupportFC>();
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
            foreach (string path in Directory.EnumerateFiles(EmpireMilitarySquadFolder))
            {
                try
                {
                    Scribe.loader.InitLoading(path);
                    SavedSquadFC squad = ResolveSavedType<SavedSquadFC>();
                    squad.ExposeData();
                    savedSquads.Add(squad);
                }
                catch (Exception e)
                {
                    LogUtil.Error($"Failed to load squad at path {path} due to exception: {e.Message}");
                }
                finally
                {
                    Scribe.loader.FinalizeLoading();
                }
            }

            foreach (string path in Directory.EnumerateFiles(EmpireMilitaryUnitFolder))
            {
                try
                {
                    Scribe.loader.InitLoading(path);
                    SavedUnitFC unit = ResolveSavedType<SavedUnitFC>();
                    unit.ExposeData();
                    savedUnits.Add(unit);
                }
                catch (Exception e)
                {
                    LogUtil.Error($"Failed to load unit at path {path} due to exception: {e.Message}");
                }
                finally
                {
                    Scribe.loader.FinalizeLoading();
                }
            }

            foreach (string path in Directory.EnumerateFiles(EmpireMilitaryFireSupportFolder))
            {
                try
                {
                    SavedFireSupportFC fs = new SavedFireSupportFC();
                    Scribe.loader.InitLoading(path);
                    fs.ExposeData();
                    savedFireSupports.Add(fs);
                }
                catch (Exception e)
                {
                    LogUtil.Error($"Failed to load fire support at path {path} due to exception: {e.Message}");
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
            if (type == null || !typeof(T).IsAssignableFrom(type))
            {
                LogUtil.Warning($"Unknown saved type '{savedType}' (mod unloaded?); loading as base {typeof(T).Name}");
                return new T();
            }
            return (T)Activator.CreateInstance(type);
        }

        public static string GetUnitPath(string name) => Path.Combine(EmpireMilitaryUnitFolder, $"{name}.xml");
        public static string GetSquadPath(string name) => Path.Combine(EmpireMilitarySquadFolder, $"{name}.xml");
        public static string GetFireSupportPath(string name) => Path.Combine(EmpireMilitaryFireSupportFolder, $"{name}.xml");

        public static void SaveSquad(SavedSquadFC squad)
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                throw new Exception("Empire - Attempt to save squad while scribe is active");
            }

            string path = GetSquadPath(squad.name);
            try
            {
                Scribe.saver.InitSaving(path, "squad");
                int version = 0;
                Scribe_Values.Look(ref version, "version");
                squad.ExposeData();
            }
            catch (Exception e)
            {
                LogUtil.Error($"Failed to save squad {squad.name} {e}");
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }

            savedSquads.RemoveAll(s => s.name == squad.name);
            savedSquads.Add(squad);
        }

        public static void SaveUnit(SavedUnitFC unit)
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                throw new Exception("Empire - Attempt to save unit while scribe is active");
            }

            string path = GetUnitPath(unit.name);
            try
            {
                Scribe.saver.InitSaving(path, "unit");
                int version = 0;
                Scribe_Values.Look(ref version, "version");
                unit.ExposeData();
            }
            catch (Exception e)
            {
                LogUtil.Error($"Failed to save unit {unit.name} {e}");
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }
            savedUnits.RemoveAll(u => u.name == unit.name);
            savedUnits.Add(unit);
        }

        public static void SaveFireSupport(SavedFireSupportFC fireSupport)
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                throw new Exception("Empire - Attempt to save fire support while scribe is active");
            }

            string path = GetFireSupportPath(fireSupport.name);
            try
            {
                Scribe.saver.InitSaving(path, "fireSupport");
                int version = 0;
                Scribe_Values.Look(ref version, "version");
                fireSupport.ExposeData();
            }
            catch (Exception e)
            {
                LogUtil.Error($"Failed to save fire support {fireSupport.name} {e}");
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }
            savedFireSupports.RemoveAll(f => f.name == fireSupport.name);
            savedFireSupports.Add(fireSupport);
        }

        public static void SaveAllUnits() => savedUnits.ForEach(SaveUnit);
        public static void SaveAllSquads() => savedSquads.ForEach(SaveSquad);
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
            inventory = new List<SavedThing>(unit.inventory ?? new List<SavedThing>());
            implants = new List<SavedImplant>(unit.implants ?? new List<SavedImplant>());
            psylinkLevel = unit.psylinkLevel;
            psycasts = new List<SavedPsycast>(unit.psycasts ?? new List<SavedPsycast>());
            isMechanitor = unit.isMechanitor;
            mechs = new List<SavedMech>(unit.mechs ?? new List<SavedMech>());
            mechGroupWorkModes = new List<MechWorkModeDef>(unit.mechGroupWorkModes ?? new List<MechWorkModeDef>());
            animals = new List<SavedAnimal>(unit.animals ?? new List<SavedAnimal>());
            mount = unit.mount;
            pawnKind = unit.pawnKind;
            xenotype = unit.xenotype;
            customXenotypeName = unit.customXenotypeName;
            forcedGender = unit.forcedGender;
            statModifiers = unit.statModifiers?.Select(m => m.Clone()).ToList() ?? new List<PermanentStatModifier>();
        }

        public MilUnitFC CreateMilUnit()
        {
            // Race weight controls random race selection only - an exported unit's race is
            // an explicit player choice and must be respected on import, even at weight 0.
            PawnKindDef resolvedKind = pawnKind;

            if (resolvedKind == null)
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
            unit.animals = animals?.Where(a => a.kind != null).ToList() ?? new List<SavedAnimal>();
            unit.mount = mount;
            unit.pawnKind = resolvedKind;
            unit.xenotype = xenotype;
            unit.customXenotypeName = customXenotypeName;
            unit.forcedGender = forcedGender;
            unit.weapons = weapons?.Where(w => w.thing != null).ToList() ?? new List<SavedThing>();
            unit.apparel = apparel?.Where(a => a.thing != null).ToList() ?? new List<SavedThing>();
            unit.inventory = inventory?.Where(i => i.thing != null).ToList() ?? new List<SavedThing>();
            unit.implants = implants?.Where(im => im.IsValid).ToList() ?? new List<SavedImplant>();
            unit.psylinkLevel = psylinkLevel;
            // Keep def-backed entries (psycast/focus) and aggregate entries that carry a kind
            // (e.g. stat upgrades, which have no defName).
            unit.psycasts = psycasts?.Where(a => a.IsValid()).ToList() ?? new List<SavedPsycast>();
            unit.isMechanitor = isMechanitor;
            unit.mechs = mechs?.Where(m => m.kind != null).ToList() ?? new List<SavedMech>();
            unit.mechGroupWorkModes = mechGroupWorkModes != null
                ? new List<MechWorkModeDef>(mechGroupWorkModes)
                : new List<MechWorkModeDef>();
            unit.statModifiers = statModifiers?.Select(m => m.Clone()).ToList() ?? new List<PermanentStatModifier>();

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
                if (animals == null) animals = new List<SavedAnimal>();
                if (animals.Count == 0 && legacyAnimal != null) animals.Add(new SavedAnimal(legacyAnimal, 1));
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

            if (xmlParent != null)
            {
                ValidateAfterLoad(xmlParent);
            }
        }

        private void ValidateAfterLoad(XmlNode xmlParent)
        {
            List<string> missing = new List<string>();

            CheckDef(xmlParent, "pawnKind", pawnKind, missing);
            CheckDef(xmlParent, "mount", mount, missing);
            CheckDef(xmlParent, "xenotype", xenotype, missing);

            int nullAnimals = animals?.Count(a => a.kind == null) ?? 0;
            if (nullAnimals > 0) missing.Add($"{nullAnimals} companion animal(s)");
            int nullWeapons = weapons?.Count(w => w.thing == null) ?? 0;
            int nullApparel = apparel?.Count(a => a.thing == null) ?? 0;
            int nullInventory = inventory?.Count(i => i.thing == null) ?? 0;
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
            if (node == null) return;
            string raw = node.InnerText;
            if (string.IsNullOrEmpty(raw) || raw == "null") return;
            if (resolved == null)
            {
                missing.Add($"{label}={raw}");
            }
        }
    }

    public class SavedSquadFC : IExposable
    {
        public string name;
        public List<SavedUnitFC> unitTemplates = new List<SavedUnitFC>();
        public List<int> units = new List<int>(30);
        public List<PermanentStatModifier> statModifiers;
        public bool IsDegraded => unitTemplates != null && unitTemplates.Any(u => u.isDegraded);

        public SavedSquadFC() { }

        public SavedSquadFC(MilSquadFC squad)
        {
            name = squad.name;

            // Dont store blank units
            var squadTemplates = squad.Units.Distinct().Where(u => !u.isBlank).ToList();

            unitTemplates = squadTemplates.Select(unit => unit.ToSavedUnit()).ToList();
            units = squad.Units.Select(unit => squadTemplates.IndexOf(unit)).ToList();
            statModifiers = squad.statModifiers?.Select(m => m.Clone()).ToList() ?? new List<PermanentStatModifier>();
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

            squad.statModifiers = statModifiers?.Select(m => m.Clone()).ToList() ?? new List<PermanentStatModifier>();
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
            projectiles = fireSupport.projectiles is object
                ? new List<ThingDef>(fireSupport.projectiles)
                : new List<ThingDef>();
            fireSupportType = fireSupport.fireSupportType;
        }

        public MilitaryFireSupport CreateFireSupport()
        {
            MilitaryFireSupport fs = new MilitaryFireSupport();
            fs.name = name;
            fs.accuracy = accuracy;
            fs.fireSupportType = fireSupportType;
            fs.projectiles = projectiles?.Where(p => p is object).ToList() ?? new List<ThingDef>();
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

            if (xmlParent is object)
            {
                ValidateAfterLoad(xmlParent);
            }
        }

        private void ValidateAfterLoad(XmlNode xmlParent)
        {
            List<string> missing = new List<string>();

            XmlNode projNode = xmlParent["projectiles"];
            if (projNode is object)
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
            if (thing == null) return null;
            Thing t = ThingMaker.MakeThing(thing, stuff);
            if (quality.HasValue)
                t.TryGetComp<CompQuality>()?.SetQuality(quality.Value, null);
            t.stackCount = Mathf.Clamp(count <= 0 ? 1 : count, 1, Mathf.Max(1, thing.stackLimit));
            return t;
        }

        public float MarketValue =>
            thing != null ? CraftUtil.ThingValue(thing, stuff, quality ?? QualityCategory.Normal) * Mathf.Max(1, count) : 0f;

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
        public bool IsValid => recipe != null || selfInstallThing != null;

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
}
