using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Companion window to <see cref="CreateColonyWindowFc"/> that previews non-resource stat
    /// modifiers (military, happiness, tax, etc.) contributed by the selected tile's mutators and
    /// landmark, before the settlement is founded. Mirrors the post-founding bake at
    /// WorldSettlementFC.cs:420-444.
    /// </summary>
    public class FCWindow_CreateColonyStatModifiers : Window, IFoundingCompanionWindow
    {
        public int CompanionOrder => 0;

        private const float WindowWidth = 280f;
        private const float Padding = 8f;

        private PlanetTile lastTile = PlanetTile.Invalid;
        private List<StatGroup> cachedGroups;

        public override Vector2 InitialSize => new Vector2(WindowWidth, 320f);

        public FCWindow_CreateColonyStatModifiers(List<StatGroup> groups)
        {
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            forcePause = false;
            closeOnAccept = false;
            closeOnCancel = false;
            cachedGroups = groups;
        }

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            CreateColonyWindowFc createWindow = Find.WindowStack.WindowOfType<CreateColonyWindowFc>();
            if (createWindow != null)
            {
                lastTile = createWindow.currentTileSelected;
            }
            if (cachedGroups != null && cachedGroups.Count > 0)
            {
                GameFont fontBefore = Text.Font;
                Text.Font = GameFont.Small;
                float usableWidth = WindowWidth - StandardMargin * 2;
                float h = 30f + Padding;
                foreach (StatGroup group in cachedGroups)
                {
                    h += Text.LineHeight + 12f;
                    h += 2f;
                    TaggedString desc = FCStatModifier.GetDescription(group.mods);
                    h += Text.CalcHeight(desc, usableWidth);
                    h += Padding;
                }
                windowRect.height = h + StandardMargin * 2;
                Text.Font = fontBefore;
            }
            FoundingScreenHooks.ReflowCompanions();
        }

        public override void DoWindowContents(Rect inRect)
        {
            CreateColonyWindowFc createWindow = Find.WindowStack.WindowOfType<CreateColonyWindowFc>();
            if (createWindow is null)
            {
                Close();
                return;
            }

            PlanetTile tile = createWindow.currentTileSelected;
            if (tile != lastTile)
            {
                lastTile = tile;
                cachedGroups = CollectGroups(tile, createWindow.currentBiomeSelected);
            }
            if (cachedGroups is null || cachedGroups.Count == 0)
            {
                Close();
                return;
            }

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            float curY = 0f;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0, curY, inRect.width, 30f);
            UIUtil.ClampedLabel(titleRect, "FCCreateColonyTileModifiersTitle".Translate());
            curY += 30f + Padding;

            Text.Anchor = TextAnchor.UpperLeft;
            foreach (StatGroup group in cachedGroups)
            {
                Widgets.ListSeparator(ref curY, inRect.width, group.sourceLabel);
                curY += 2f;
                TaggedString desc = FCStatModifier.GetDescription(group.mods);
                float descHeight = Text.CalcHeight(desc, inRect.width);
                Widgets.Label(new Rect(0, curY, inRect.width, descHeight), desc);
                curY += descHeight + Padding;
            }

            windowRect.height = curY + Window.StandardMargin * 2;

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        /// <summary>
        /// Opens or closes the companion window based on whether the given tile currently has any
        /// non-resource stat modifiers to show. Called from the create-colony window whenever its
        /// selected tile changes.
        /// </summary>
        public static void RefreshForTile(PlanetTile tile, BiomeResourceDef biome = null)
        {
            List<StatGroup> groups = CollectGroups(tile, biome);
            FCWindow_CreateColonyStatModifiers existing = Find.WindowStack.WindowOfType<FCWindow_CreateColonyStatModifiers>();
            if (groups.Count > 0)
            {
                if (existing is null)
                    Find.WindowStack.Add(new FCWindow_CreateColonyStatModifiers(groups));
            }
            else if (existing is object)
            {
                existing.Close();
            }
        }

        /// <summary>
        /// Closes the companion window if it's currently open. Called when the parent create-colony
        /// window closes so the companion can't outlive its parent.
        /// </summary>
        public static void TryClose()
        {
            FCWindow_CreateColonyStatModifiers existing = Find.WindowStack.WindowOfType<FCWindow_CreateColonyStatModifiers>();
            if (existing is object)
                existing.Close();
        }

        private static List<StatGroup> CollectGroups(PlanetTile tile, BiomeResourceDef biome = null)
        {
            List<StatGroup> groups = new List<StatGroup>();
            if (tile == PlanetTile.Invalid) return groups;
            Tile worldTile = tile.Tile;
            if (worldTile is null) return groups;

            // Biome stat modifiers
            if (biome?.statModifiers != null)
            {
                List<FCStatModifier> filtered = FilterNonResourceLinked(biome.statModifiers);
                if (filtered.Count > 0)
                    groups.Add(new StatGroup { sourceLabel = biome.LabelCap, mods = filtered });
            }

            IList<TileMutatorDef> mutators = worldTile.Mutators;
            if (mutators != null)
            {
                foreach (TileMutatorDef mut in mutators)
                {
                    if (mut is null) continue;
                    TileMutatorResourceExtension ext = mut.GetModExtension<TileMutatorResourceExtension>();
                    List<FCStatModifier> filtered = FilterNonResourceLinked(ext?.statModifiers);
                    if (filtered.Count > 0)
                        groups.Add(new StatGroup { sourceLabel = mut.LabelCap, mods = filtered });
                }
            }

            Landmark landmark = worldTile.Landmark;
            if (landmark?.def != null)
            {
                TileLandmarkResourceExtension ext = landmark.def.GetModExtension<TileLandmarkResourceExtension>();
                List<FCStatModifier> filtered = FilterNonResourceLinked(ext?.statModifiers);
                if (filtered.Count > 0)
                    groups.Add(new StatGroup { sourceLabel = landmark.def.LabelCap, mods = filtered });
            }

            return groups;
        }

        private static List<FCStatModifier> FilterNonResourceLinked(List<FCStatModifier> src)
        {
            List<FCStatModifier> result = new List<FCStatModifier>();
            if (src is null) return result;
            foreach (FCStatModifier mod in src)
            {
                if (mod?.stat is null) continue;
                if (mod.stat.linkedResource != null) continue;
                result.Add(mod);
            }
            return result;
        }

        public class StatGroup
        {
            public string sourceLabel;
            public List<FCStatModifier> mods;
        }
    }
}
