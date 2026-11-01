using HarmonyLib;
using RimWar.Planet;
using RimWorld.Planet;
using System;
using System.Reflection;
using Verse;

namespace FactionColonies.RW
{
    /// <summary>
    /// Compatibility patches for RimWar (Torann.RimWar).
    /// This assembly is only loaded when RimWar is active (via LoadFolders.xml).
    ///
    /// Fixes:
    /// 1. Makes Empire settlements visible to RimWar's settlement tracking
    /// 2. Overrides RimWar point values with Empire's actual military/economic strength
    /// 3. No-ops RimWar's broken Empire detection check
    /// 4. Prevents RimWar from capturing/duplicating Empire settlements
    /// 5. Suppresses vassal unit request gizmos on Empire settlements
    /// 6. Scales enemy force in Empire battles based on RimWar settlement strength
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimWarCompatInit
    {
        static RimWarCompatInit()
        {
            new Harmony("com.Matathias.Empire.RW").PatchAll(Assembly.GetExecutingAssembly());
            EmpireRegistry.Register(new RWStrengthSettlementModifier());
            LogUtil.MessageForce("RimWar compatibility module loaded.");
        }
    }

    /// <summary>
    /// Overrides the cached settlement power baseline using the target's RimWar points,
    /// so settlements that have grown powerful through RimWar's warfare system are harder
    /// to raid. Implemented as <see cref="ISettlementPowerModifier"/> so the override is
    /// baked into the cached <see cref="EnemyPower"/> at recompute time, ensuring the
    /// squad-attack window's displayed range matches the battle.
    ///
    /// Uses sqrt(points) / 20 scaling:
    ///   400 pts -> level 1, 3600 -> 3, 10000 -> 5, 19600 -> 7, 32400 -> 9
    /// </summary>
    public class RWStrengthSettlementModifier : SettlementPowerModifierBase
    {
        protected override string LogLabel => "RW strength";

        protected override bool TryGetLevel(Settlement settlement, out double level)
        {
            level = 0;
            RimWarSettlementComp rwsc = settlement.GetComponent<RimWarSettlementComp>();
            if (rwsc is null || rwsc.RimWarPoints <= 0) return false;

            level = Math.Max(Math.Sqrt(rwsc.RimWarPoints) / 20.0, 1.0);
            return true;
        }
    }
}
