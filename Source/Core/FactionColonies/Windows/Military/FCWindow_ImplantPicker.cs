using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Modal picker that lists every surgical implant currently installable on the unit's
/// preview pawn, reusing the base game's own surgery validation (Recipe_InstallImplant and
/// Recipe_InstallArtificialBodyPart + RecipeWorker.GetPartsToApplyOn/AvailableOnNow). Because the preview pawn already carries
/// the unit's previously-chosen implants, taken/conflicting slots are filtered out for free.
/// Stays open so multiple implants can be added; the option list rebuilds whenever the unit's
/// editVersion changes (each add regenerates the preview pawn).
/// </summary>
public class FCWindow_ImplantPicker : FCWindow_SearchablePicker<FCWindow_ImplantPicker.Option>
{
    public struct Option
    {
        public RecipeDef recipe;             // surgery install (null for self-install)
        public ThingDef selfInstallThing;    // self-install item (null for surgery)
        public BodyPartDef bodyPartDef;
        public int bodyPartIndex;
        public string label;

        public ThingDef IconThing => selfInstallThing ?? MilUnitFC.ImplantIconThing(recipe);
    }

    private readonly Func<MilUnitFC> getDisplayUnit;
    private readonly Func<MilUnitFC> getEditTarget;

    private List<Option> options = [];
    private int builtForVersion = int.MinValue;
    private MilUnitFC builtForUnit;

    public override Vector2 InitialSize => new(540f, 620f);

    public FCWindow_ImplantPicker(Func<MilUnitFC> getDisplayUnit, Func<MilUnitFC> getEditTarget)
    {
        this.getDisplayUnit = getDisplayUnit;
        this.getEditTarget = getEditTarget;
    }

    protected override string Title => "fcPickImplant".Translate();
    protected override string EmptyText => null;
    protected override bool PicksOnClick => true;
    protected override bool ShowCloseButton => true;

    protected override List<Option> Items()
    {
        RebuildIfStale();
        return options;
    }

    protected override string SearchText(Option opt) => opt.label;

    protected override void DrawRow(Rect row, Option opt)
    {
        float optCost = opt.recipe is not null
            ? MilUnitFC.ImplantCost(opt.recipe)
            : (opt.selfInstallThing?.BaseMarketValue ?? 0f);
        DrawThingRow(row, opt.IconThing, opt.label, "$" + optCost.ToString("F0"));
    }

    protected override void Pick(Option opt)
    {
        MilUnitFC target = getEditTarget?.Invoke();
        if (target is null) return;
        if (opt.recipe is not null)
            target.AddImplant(opt.recipe, opt.bodyPartDef, opt.bodyPartIndex);
        else if (opt.selfInstallThing is not null)
            target.AddImplant(opt.selfInstallThing, opt.bodyPartDef, opt.bodyPartIndex);
        builtForVersion = int.MinValue; // force rebuild against the updated preview
    }

    private void RebuildIfStale()
    {
        MilUnitFC unit = getDisplayUnit?.Invoke();
        int version = unit?.editVersion ?? int.MinValue;
        if (unit == builtForUnit && version == builtForVersion) return;
        builtForUnit = unit;
        builtForVersion = version;
        options = BuildOptions(unit?.PreviewPawn);
    }

    private static List<Option> BuildOptions(Pawn pawn)
    {
        List<Option> result = [];
        if (pawn is null || pawn.health is null) return result;

        foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs)
        {
            // Accept hediff implants and artificial body parts (prosthetic/bionic/archotech).
            // Both derive from Recipe_Surgery but not from each other, so an explicit pair of
            // checks is needed. Natural-organ transplants (Recipe_InstallNaturalBodyPart) are
            // intentionally excluded — harvested organs aren't installable augmentations here.
            if (recipe.Worker is not Recipe_InstallImplant &&
                recipe.Worker is not Recipe_InstallArtificialBodyPart) continue;
            if (recipe.addsHediff is null) continue;
            // The death acidifier is auto-applied to all mercs (see
            // MercenaryPawnFactory.TryApplyDeathAcidifier), so it isn't manually selectable here.
            if (recipe == FCRecipeDefOf.InstallDeathAcidifier) continue;
            if (!recipe.AvailableNow) continue;

            // Artificial body parts carry no research prerequisite on the INSTALL recipe — the gate
            // lives on the part item's crafting recipe (e.g. a bionic arm needs the Bionic Replacements
            // research). Only offer parts the Empire can actually make or obtain.
            if (recipe.Worker is Recipe_InstallArtificialBodyPart)
            {
                ThingDef partThing = MilUnitFC.ImplantIconThing(recipe); // fixed ingredient = the part item
                if (partThing is null || !CraftUtil.CanCraftItem(partThing)) continue;
            }

            List<BodyPartRecord> parts = new(recipe.Worker.GetPartsToApplyOn(pawn, recipe));

            if (!recipe.targetsBodyPart)
            {
                BodyPartRecord wholeBody = parts.Count > 0 ? parts[0] : null;
                if (!recipe.Worker.AvailableOnNow(pawn, wholeBody)) continue;
                Option o = new();
                o.recipe = recipe;
                o.bodyPartDef = wholeBody is not null ? wholeBody.def : null;
                o.bodyPartIndex = 0;
                o.label = recipe.Worker.GetLabelWhenUsedOn(pawn, wholeBody).CapitalizeFirst();
                result.Add(o);
                continue;
            }

            for (int idx = 0; idx < parts.Count; idx++)
            {
                BodyPartRecord part = parts[idx];
                if (!recipe.Worker.AvailableOnNow(pawn, part)) continue;
                Option o = new();
                o.recipe = recipe;
                o.bodyPartDef = part is not null ? part.def : null;
                // Store the STABLE occurrence index in body.AllParts (not the filtered-list
                // index), so the implant resolves to the same part on any pawn of this body
                // regardless of its current health state. See MilUnitFC.TryResolveImplant.
                o.bodyPartIndex = MilUnitFC.BodyPartOccurrenceIndex(pawn, part);
                string lbl = recipe.Worker.GetLabelWhenUsedOn(pawn, part).CapitalizeFirst();
                if (part is not null && !recipe.hideBodyPartNames)
                    lbl = lbl + " (" + part.Label + ")";
                o.label = lbl;
                result.Add(o);
            }
        }

        AddSelfInstallOptions(pawn, result);

        result.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /* Adds self-install implants (CompUseEffect_InstallImplant items such as control sublinks) that
     * are currently installable on the pawn. We can't call the base game's CompUseEffect_InstallImplant
     * .CanBeUsedBy directly: its first check rejects any pawn that isn't a free player colonist
     * ("InstallImplantNotAllowedForNonColonists"), and our preview pawn is an Empire-faction NPC — so it
     * would reject everything. Instead we replicate the rest of CanBeUsedBy minus that colonist gate
     * (body part present, userMustHaveHediff, psychic sensitivity, leveled-upgrade limits), so a leveled
     * implant keeps appearing until it hits its cap and a higher-tier variant takes over. The mechlink
     * and psylink are excluded — those are owned by the Mechs and Psycasts tabs. */
    private static void AddSelfInstallOptions(Pawn pawn, List<Option> result)
    {
        foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            CompProperties_UseEffectInstallImplant inst = thing.GetCompProperties<CompProperties_UseEffectInstallImplant>();
            if (inst?.hediffDef is null || inst.bodyPart is null) continue;

            // Owned by other tabs.
            if (inst.hediffDef == HediffDefOf.MechlinkImplant) continue;
            if (inst.hediffDef == HediffDefOf.PsychicAmplifier) continue;

            // Must be craftable with the research the player has unlocked.
            if (!SelfInstallResearchDone(thing)) continue;

            // Item may require the pawn to already have a hediff (e.g. control sublink needs a mechlink).
            CompProperties_Usable usable = thing.GetCompProperties<CompProperties_Usable>();
            if (usable?.userMustHaveHediff is not null && !pawn.health.hediffSet.HasHediff(usable.userMustHaveHediff))
                continue;

            if (inst.requiresPsychicallySensitive && pawn.psychicEntropy is not null && !pawn.psychicEntropy.IsPsychicallySensitive)
                continue;

            BodyPartRecord part = pawn.RaceProps.body.GetPartsWithDef(inst.bodyPart).FirstOrFallback();
            if (part is null) continue;

            // Leveled-install limits, mirroring CanBeUsedBy against the current preview state.
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(inst.hediffDef);
            if (inst.requiresExistingHediff && existing is null) continue;
            if (existing is not null)
            {
                if (!inst.canUpgrade) continue;
                Hediff_Level lvl = existing as Hediff_Level;
                if (lvl is not null)
                {
                    if (lvl.level >= lvl.def.maxSeverity) continue;
                    if (inst.maxSeverity <= lvl.level) continue;
                    if (inst.minSeverity > lvl.level) continue;
                }
            }

            Option o = new();
            o.selfInstallThing = thing;
            o.bodyPartDef = part.def;
            o.bodyPartIndex = MilUnitFC.BodyPartOccurrenceIndex(pawn, part);
            o.label = "fcInstallSelfImplant".Translate(thing.LabelCap) + " (" + part.Label + ")";
            result.Add(o);
        }
    }

    private static bool SelfInstallResearchDone(ThingDef thing)
    {
        RecipeMakerProperties rm = thing.recipeMaker;
        if (rm is null) return false; // not craftable — not normally obtainable
        if (rm.researchPrerequisite is not null && !rm.researchPrerequisite.IsFinished) return false;
        if (rm.researchPrerequisites is not null && rm.researchPrerequisites.Any(r => !r.IsFinished)) return false;
        return true;
    }
}
