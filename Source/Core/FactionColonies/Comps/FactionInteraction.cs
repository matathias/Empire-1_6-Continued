using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public class WorldObjectCompProperties_FactionInteraction : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_FactionInteraction()
        {
            compClass = typeof(WorldObjectComp_FactionInteraction);
        }
    }

    /// <summary>
    /// Adds Empire interaction gizmos (attack, diplomacy) to non-player, non-Empire settlements.
    /// XML-patched onto the vanilla Settlement WorldObjectDef so gizmos flow through
    /// RimWorld's native comp system instead of Harmony patches.
    /// </summary>
    public class WorldObjectComp_FactionInteraction : WorldObjectComp
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
                yield return gizmo;

            if (!HasValidFaction()) yield break;
            FactionFC factionFC = FindFC.FactionComp;
            if (factionFC is null) yield break;

            Faction faction = parent.Faction;
            PlanetTile tile = parent.Tile;

            if (FindFC.FactionComp.IsActionAllowed(FCActionType.SendDiplomat))
                yield return PeacefulAction(factionFC, faction);

            // Suppress the launch-attack gizmo while an Empire military operation is actively
            // targeting this settlement (a squad en route or a battle in progress, manual or auto) --
            // you can't launch a new attack on a settlement that is already an active target. Ops in
            // cooldown are ignored: that battle is over, so a fresh attack is allowed again. During an
            // offense the enemy settlement instead shows the "Join Attack" control (WorldObjectComp_OffenseControls).
            // Also hide it when a live map already exists at the tile: the player is attacking the
            // settlement in person (vanilla caravan / quest site), and an Empire op would hijack that
            // map. Prevents opening an attack dialog that can only be rejected at launch.
            bool blockLaunch = (FindFC.MilitaryManager?.HasActiveOpAt(tile) ?? false)
                || Current.Game.FindMap(tile) is object;
            if (!blockLaunch && FindFC.FactionComp.IsActionAllowed(FCActionType.DeployMilitary))
                yield return HostileAction(factionFC, faction, tile);
        }

        private bool HasValidFaction() =>
            parent.Faction is object &&
            parent.Faction != FindFC.EmpireFaction &&
            parent.Faction != Find.FactionManager.OfPlayer;

        private static Command_Action HostileAction(FactionFC factionFC, Faction faction, PlanetTile tile) =>
            new Command_Action
            {
                defaultLabel = "FCAttackSettlement".Translate(
                    faction.HasName ? faction.Name : "FCUnsupportedSettlementFaction".Translate().ToString()),
                defaultDesc = "",
                icon = TexLoad.iconMilitary,
                action = delegate
                {
                    WorldObject target = Find.WorldObjects.WorldObjectAt<WorldObject>(tile);
                    if (target is null)
                    {
                        Messages.Message("FCNoValidMilitaries".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    List<MilitaryJobDef> jobs = FactionCache.HostileMilitaryJobs
                        .Where(j => FindFC.FactionComp.IsMilitaryJobAllowed(j))
                        .Where(j => j.Handler == null || j.Handler.IsValidTarget(faction))
                        .ToList();
                    if (jobs.Count == 0)
                    {
                        Messages.Message("FCNoValidMilitaries".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Find.WindowStack.Add(new Dialog_AttackSettlement(target, faction, jobs));
                }
            };

        private static Command_Action PeacefulAction(FactionFC factionFC, Faction faction) =>
            new Command_Action
            {
                defaultLabel = "FCIncreaseRelations".Translate(),
                defaultDesc = "",
                icon = TexLoad.iconProsperity,
                action = delegate { factionFC.SendDiplomaticEnvoy(faction); }
            };
    }
}
