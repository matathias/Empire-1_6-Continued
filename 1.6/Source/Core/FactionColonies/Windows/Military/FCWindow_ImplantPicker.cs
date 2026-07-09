using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Modal picker that lists every surgical implant currently installable on the unit's
    /// preview pawn, reusing the base game's own surgery validation (Recipe_InstallImplant and
    /// Recipe_InstallArtificialBodyPart + RecipeWorker.GetPartsToApplyOn/AvailableOnNow). Because the preview pawn already carries
    /// the unit's previously-chosen implants, taken/conflicting slots are filtered out for free.
    /// Stays open so multiple implants can be added; the option list rebuilds whenever the unit's
    /// editVersion changes (each add regenerates the preview pawn).
    /// </summary>
    public class FCWindow_ImplantPicker : Window
    {
        private struct Option
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

        private List<Option> options = new List<Option>();
        private int builtForVersion = int.MinValue;
        private MilUnitFC builtForUnit;
        private string searchTerm = "";
        private Vector2 scrollPos;

        private const float RowHeight = 30f;
        private const float SearchBarHeight = 28f;
        private const float margin = 5f;

        public override Vector2 InitialSize => new Vector2(540f, 620f);

        public FCWindow_ImplantPicker(Func<MilUnitFC> getDisplayUnit, Func<MilUnitFC> getEditTarget)
        {
            this.getDisplayUnit = getDisplayUnit;
            this.getEditTarget = getEditTarget;
            forcePause = false;
            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), "fcPickImplant".Translate());

            RebuildIfStale();

            Rect searchRect = new Rect(0, 40f, inRect.width, SearchBarHeight);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            searchTerm = Widgets.TextField(searchRect, searchTerm);

            Rect listOut = new Rect(0, searchRect.yMax + margin, inRect.width, inRect.height - searchRect.yMax - margin - 40f);
            Widgets.DrawMenuSection(listOut);

            List<Option> filtered = string.IsNullOrEmpty(searchTerm)
                ? options
                : options.Where(o => o.label.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            float viewHeight = filtered.Count * RowHeight;
            Rect scrollView = ScrollUtil.BeginScrollView(listOut, ref scrollPos, viewHeight);

            for (int i = 0; i < filtered.Count; i++)
            {
                Option opt = filtered[i];
                Rect row = new Rect(scrollView.x, scrollView.y + i * RowHeight, scrollView.width, RowHeight);
                if (i % 2 == 0) Widgets.DrawHighlight(row);

                ThingDef iconThing = opt.IconThing;
                Rect iconRect = new Rect(row.x + margin, row.y, RowHeight, RowHeight);
                if (iconThing != null)
                    Widgets.ThingIcon(iconRect, iconThing);

                Rect infoRect = new Rect(iconRect.xMax, row.y + 2f, RowHeight - 4f, RowHeight - 4f);
                if (iconThing != null)
                    Widgets.InfoCardButton(infoRect, iconThing);

                float optCost = opt.recipe is object
                    ? MilUnitFC.ImplantCost(opt.recipe)
                    : (opt.selfInstallThing?.BaseMarketValue ?? 0f);
                Rect costRect = new Rect(row.xMax - margin - 65f, row.y, 60f, RowHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(costRect, "$" + optCost.ToString("F0"));

                Rect labelRect = new Rect(infoRect.xMax + margin, row.y, costRect.x - infoRect.xMax - 2 * margin, RowHeight);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, opt.label);

                if (Widgets.ButtonInvisible(row))
                {
                    MilUnitFC target = getEditTarget?.Invoke();
                    if (target != null)
                    {
                        if (opt.recipe is object)
                            target.AddImplant(opt.recipe, opt.bodyPartDef, opt.bodyPartIndex);
                        else if (opt.selfInstallThing is object)
                            target.AddImplant(opt.selfInstallThing, opt.bodyPartDef, opt.bodyPartIndex);
                        builtForVersion = int.MinValue; // force rebuild against the updated preview
                    }
                }
            }

            ScrollUtil.EndScrollView();

            // Reset font explicitly: when the list is empty the row loop (which would have left it
            // at Small) never ran, so the Close button would otherwise inherit the title's Medium font.
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect closeRect = new Rect(inRect.width - 120f, inRect.height - 35f, 120f, 30f);
            if (Widgets.ButtonText(closeRect, "FCDialogPawnLoadoutClose".Translate()))
                Close();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
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
            List<Option> result = new List<Option>();
            if (pawn == null || pawn.health == null) return result;

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs)
            {
                // Accept hediff implants and artificial body parts (prosthetic/bionic/archotech).
                // Both derive from Recipe_Surgery but not from each other, so an explicit pair of
                // checks is needed. Natural-organ transplants (Recipe_InstallNaturalBodyPart) are
                // intentionally excluded — harvested organs aren't installable augmentations here.
                if (!(recipe.Worker is Recipe_InstallImplant) &&
                    !(recipe.Worker is Recipe_InstallArtificialBodyPart)) continue;
                if (recipe.addsHediff == null) continue;
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

                List<BodyPartRecord> parts = new List<BodyPartRecord>(recipe.Worker.GetPartsToApplyOn(pawn, recipe));

                if (!recipe.targetsBodyPart)
                {
                    BodyPartRecord wholeBody = parts.Count > 0 ? parts[0] : null;
                    if (!recipe.Worker.AvailableOnNow(pawn, wholeBody)) continue;
                    Option o = new Option();
                    o.recipe = recipe;
                    o.bodyPartDef = wholeBody != null ? wholeBody.def : null;
                    o.bodyPartIndex = 0;
                    o.label = recipe.Worker.GetLabelWhenUsedOn(pawn, wholeBody).CapitalizeFirst();
                    result.Add(o);
                    continue;
                }

                for (int idx = 0; idx < parts.Count; idx++)
                {
                    BodyPartRecord part = parts[idx];
                    if (!recipe.Worker.AvailableOnNow(pawn, part)) continue;
                    Option o = new Option();
                    o.recipe = recipe;
                    o.bodyPartDef = part != null ? part.def : null;
                    // Store the STABLE occurrence index in body.AllParts (not the filtered-list
                    // index), so the implant resolves to the same part on any pawn of this body
                    // regardless of its current health state. See MilUnitFC.TryResolveImplant.
                    o.bodyPartIndex = MilUnitFC.BodyPartOccurrenceIndex(pawn, part);
                    string lbl = recipe.Worker.GetLabelWhenUsedOn(pawn, part).CapitalizeFirst();
                    if (part != null && !recipe.hideBodyPartNames)
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
                if (usable?.userMustHaveHediff != null && !pawn.health.hediffSet.HasHediff(usable.userMustHaveHediff))
                    continue;

                if (inst.requiresPsychicallySensitive && pawn.psychicEntropy != null && !pawn.psychicEntropy.IsPsychicallySensitive)
                    continue;

                BodyPartRecord part = pawn.RaceProps.body.GetPartsWithDef(inst.bodyPart).FirstOrFallback();
                if (part is null) continue;

                // Leveled-install limits, mirroring CanBeUsedBy against the current preview state.
                Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(inst.hediffDef);
                if (inst.requiresExistingHediff && existing is null) continue;
                if (existing is object)
                {
                    if (!inst.canUpgrade) continue;
                    Hediff_Level lvl = existing as Hediff_Level;
                    if (lvl != null)
                    {
                        if (lvl.level >= lvl.def.maxSeverity) continue;
                        if (inst.maxSeverity <= lvl.level) continue;
                        if (inst.minSeverity > lvl.level) continue;
                    }
                }

                Option o = new Option();
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
            if (rm.researchPrerequisite != null && !rm.researchPrerequisite.IsFinished) return false;
            if (rm.researchPrerequisites != null && rm.researchPrerequisites.Any(r => !r.IsFinished)) return false;
            return true;
        }
    }
}
