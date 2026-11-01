using CombatExtended;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies.CE
{
    /// <summary>
    /// Registers the CE bridge on game startup.
    /// This class only exists in Empire.CE.dll, which is only loaded when CE is active
    /// (via LoadFolders.xml conditional loading).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CombatExtendedInit
    {
        static CombatExtendedInit()
        {
            CombatExtendedUtil.Bridge = new CombatExtendedBridge();
            LogUtil.MessageForce("Combat Extended compatibility module loaded.");
        }
    }

    /// <summary>
    /// Direct CE API implementation of ICombatExtendedBridge.
    /// Uses direct type references instead of reflection for all CE APIs.
    /// </summary>
    public class CombatExtendedBridge : ICombatExtendedBridge
    {
        public void UpdateInventory(Pawn pawn)
        {
            if (pawn == null) return;

            try
            {
                pawn.TryGetComp<CompInventory>()?.UpdateInventory();
            }
            catch (Exception e)
            {
                LogUtil.Error($"Failed to update CE inventory for {pawn.LabelShort}: {e.Message}");
            }
        }

        public bool LaunchFireSupportProjectile(ThingDef ammoDef, Map map, IntVec3 source, IntVec3 target)
        {
            try
            {
                ThingDef projectileDef = ResolveAmmoProjectile(ammoDef);
                if (projectileDef == null) return false;

                var ceProps = projectileDef.projectile as ProjectilePropertiesCE;
                if (ceProps == null) return false;

                // Match CE's TravelingShell pattern: off-map artillery arriving from altitude
                float shotHeight = 200f;

                // Compute minimum speed so CE's ballistic discriminant is always >= 0.
                // From TryFindShotAngle: discriminant = v^4 - g*(g*d^2 + 2*dh*v^2)
                // Solving for v^2 >= g*(sqrt(h^2 + d^2) - h) ensures a valid trajectory.
                float gravityPerWidth = ceProps.GravityPerWidth;
                float range = source.DistanceTo(target);
                float minSpeedSq = gravityPerWidth * (Mathf.Sqrt(shotHeight * shotHeight + range * range) - shotHeight);
                float shotSpeed = Mathf.Max(20f, Mathf.Sqrt(minSpeedSq) * 1.1f);

                Vector3 source3D = new Vector3(source.x, shotHeight, source.z);
                Vector3 target3D = target.ToVector3Shifted();

                float shotRotation = ceProps.TrajectoryWorker.ShotRotation(ceProps, source3D, target3D);
                float shotAngle = ceProps.TrajectoryWorker.ShotAngle(ceProps, source3D, target3D, shotSpeed);

                var projectile = (ProjectileCE)GenSpawn.Spawn(projectileDef, source, map);
                if (projectile == null) return false;
                projectile.canTargetSelf = false;

                projectile.Launch(
FindFC.EmpireFaction?.leader,
                    new Vector2(source.x, source.z),
                    shotAngle,
                    shotRotation,
                    shotHeight,
                    shotSpeed);

                // Override the internally-computed Destination with the exact target.
                // CE's Lerped trajectory interpolates toward Destination, so this
                // guarantees the shell lands at the target regardless of ballistic math.
                projectile.Destination = new Vector2(target.x + 0.5f, target.z + 0.5f);

                return true;
            }
            catch (Exception e)
            {
                LogUtil.Error($"CE fire support launch failed: {e}");
                return false;
            }
        }

        public bool IsIndirectFireAmmo(ThingDef ammoDef)
        {
            // Non-CE ammo (vanilla shells) — always allowed
            if (!(ammoDef is AmmoDef ceAmmo)) return true;
            // CE ammo — only allow if it belongs to a mortar ammo set
            var ammoSets = ceAmmo.AmmoSetDefs;
            return ammoSets != null && ammoSets.Any(set => set.isMortarAmmoSet);
        }

        /// <summary>
        /// Resolve an AmmoDef to the ThingDef of its mortar projectile.
        /// The same ammo maps to different projectile defs per weapon type (mortar vs rifle).
        /// Fire support is mortar bombardment, so prefer mortar ammo sets (isMortarAmmoSet)
        /// with flyOverhead projectiles.
        /// </summary>
        private static ThingDef ResolveAmmoProjectile(ThingDef ammoDef)
        {
            if (!(ammoDef is AmmoDef ceAmmo)) return null;

            var ammoSetDefs = ceAmmo.AmmoSetDefs;
            if (ammoSetDefs == null || ammoSetDefs.Count == 0) return null;

            // Prefer mortar ammo sets with flyOverhead projectiles (indirect fire)
            foreach (AmmoSetDef ammoSet in ammoSetDefs)
            {
                if (!ammoSet.isMortarAmmoSet) continue;
                foreach (AmmoLink link in ammoSet.ammoTypes)
                {
                    if (link.ammo == ceAmmo && link.projectile?.projectile?.flyOverhead == true)
                        return link.projectile;
                }
            }

            // Fallback: any mortar set, any matching projectile
            foreach (AmmoSetDef ammoSet in ammoSetDefs)
            {
                if (!ammoSet.isMortarAmmoSet) continue;
                foreach (AmmoLink link in ammoSet.ammoTypes)
                {
                    if (link.ammo == ceAmmo)
                        return link.projectile;
                }
            }

            // Last resort: first set, first match (original behavior)
            foreach (AmmoLink link in ammoSetDefs[0].ammoTypes)
            {
                if (link.ammo == ceAmmo)
                    return link.projectile;
            }

            return null;
        }
    }
}
