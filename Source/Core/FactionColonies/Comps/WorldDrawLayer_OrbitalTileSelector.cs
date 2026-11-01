using RimWorld.Planet;
using System.Collections;
using Verse;

namespace FactionColonies
{
    public class WorldDrawLayer_OrbitalTileSelector : WorldDrawLayer_RaycastableGrid
    {
        private bool activated;

        private static bool ShouldActivate
        {
            get
            {
                if (Find.TilePicker.Active)
                {
                    return FindFC.FactionComp?.layersForTilePicker?.Contains(Find.WorldGrid.Orbit.Def) ?? false;
                }
                return false;
            }
        }

        public override bool ShouldRegenerate
        {
            get
            {
                if (activated == ShouldActivate)
                {
                    return base.ShouldRegenerate;
                }
                return true;
            }
        }

        public override IEnumerable Regenerate()
        {
            activated = ShouldActivate;
            if (activated)
            {
                foreach (object item in base.Regenerate())
                {
                    yield return item;
                }
            }
            else
            {
                Dispose();
                yield return RegenerateWorldMeshColliders();
            }
        }
    }
}