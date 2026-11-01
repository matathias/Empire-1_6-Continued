using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Per-world cache of <see cref="EnemyPower"/> entries plus the single sanctioned site
    /// for invoking <see cref="BattleModifierRegistry"/>. Holds two parallel dictionaries:
    /// faction baselines (canonical) and settlement entries (mirrored from their faction's
    /// baseline at recompute time, then independently mutated by settlement-level modifiers).
    ///
    /// <para>Three modifier sites fire from this component:</para>
    /// <list type="number">
    /// <item><c>InvokeFactionPowerModifiers</c> — after the deterministic faction baseline (tech-level def + faction override) is computed.</item>
    /// <item><c>InvokeSettlementPowerModifiers</c> — after a settlement entry is mirrored from its faction.</item>
    /// <item><c>InvokeBattleModifiers</c> — at engagement / display, via <see cref="ResolveDefenderForceForOp"/>, <see cref="ResolveDefenderBounds"/>, and <see cref="ApplyBattleModifiers"/>.</item>
    /// </list>
    /// </summary>
    public class WorldComponent_EnemyPower : WorldComponent
    {
        public WorldComponent_EnemyPower(World world) : base(world) { }

        public const int RecomputeIntervalTicks = GenDate.TicksPerDay * 5;

        private Dictionary<Settlement, EnemyPower> settlementPowers
            = new Dictionary<Settlement, EnemyPower>();
        private Dictionary<Faction, EnemyPower> factionPowers
            = new Dictionary<Faction, EnemyPower>();

        /* Working lists for Scribe_Collections (Reference-keyed dicts need them per scribe-primer §5). */
        private List<Settlement> _scribeSettlementKeys;
        private List<EnemyPower> _scribeSettlementValues;
        private List<Faction> _scribeFactionKeys;
        private List<EnemyPower> _scribeFactionValues;

        /* Def caches built lazily from DefDatabase. */
        private Dictionary<TechLevel, EnemyPowerTechDef> techDefCache;
        private Dictionary<FactionDef, EnemyPowerFactionDef> factionDefCache;

        private int nextRecomputeTick = -1;

        public IReadOnlyDictionary<Settlement, EnemyPower> SettlementPowers => settlementPowers;
        public IReadOnlyDictionary<Faction, EnemyPower> FactionPowers => factionPowers;

        public override void ExposeData()
        {
            /* Before writing, drop entries whose key was destroyed/removed since the last
             * recompute. No settlement-destruction hook evicts entries between the 5-day
             * RecomputeAll passes, so a conquered foreign settlement can linger as a key; a
             * LookMode.Reference key to a destroyed Settlement is saved as a dangling ref and
             * logs "Null key while loading dictionary ... settlementPowers" on load. The
             * PostLoadInit prune below stays as a backstop for already-saved games. */
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (settlementPowers is object)
                {
                    List<Settlement> staleSettlements = settlementPowers.Where(kv => kv.Key is null || kv.Key.Destroyed).Select(kv => kv.Key).ToList();
                    foreach (Settlement k in staleSettlements) settlementPowers.Remove(k);
                }
                if (factionPowers is object)
                {
                    List<Faction> staleFactions = factionPowers.Where(kv => kv.Key is null).Select(kv => kv.Key).ToList();
                    foreach (Faction k in staleFactions) factionPowers.Remove(k);
                }
            }

            Scribe_Collections.Look(ref settlementPowers, "settlementPowers", LookMode.Reference, LookMode.Deep, ref _scribeSettlementKeys, ref _scribeSettlementValues);
            Scribe_Collections.Look(ref factionPowers, "factionPowers", LookMode.Reference, LookMode.Deep, ref _scribeFactionKeys, ref _scribeFactionValues);
            Scribe_Values.Look(ref nextRecomputeTick, "nextRecomputeTick", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (settlementPowers is null) settlementPowers = new Dictionary<Settlement, EnemyPower>();
                if (factionPowers is null) factionPowers = new Dictionary<Faction, EnemyPower>();

                /* LookMode.Reference silently leaves entries with null keys for unresolved /
                 * destroyed targets — drop them. */
                List<Settlement> staleSettlements = settlementPowers.Where(kv => kv.Key is null || kv.Key.Destroyed).Select(kv => kv.Key).ToList();
                foreach (Settlement k in staleSettlements) settlementPowers.Remove(k);

                List<Faction> staleFactions = factionPowers.Where(kv => kv.Key is null).Select(kv => kv.Key).ToList();
                foreach (Faction k in staleFactions) factionPowers.Remove(k);
            }
        }

        public override void WorldComponentTick()
        {
            if (Find.TickManager.TicksGame >= nextRecomputeTick)
            {
                RecomputeAll();
                nextRecomputeTick = Find.TickManager.TicksGame + RecomputeIntervalTicks;
            }
        }

        /* === Lookup === */

        /// <summary>
        /// Returns the cached entry for <paramref name="faction"/>, computing one (with
        /// faction-level modifier pass) on the spot if missing.
        /// </summary>
        public EnemyPower GetOrCompute(Faction faction)
        {
            if (faction is null || faction.def is null) return null;
            /* Player and empire factions have no enemy estimate (mirrors RecomputeAll). */
            if (faction.IsPlayer || FindFC.IsEmpireFaction(faction)) return null;
            EnemyPower p;
            if (factionPowers.TryGetValue(faction, out p)) return p;
            p = new EnemyPower();
            ComputeFactionBaseline(faction, p);
            BattleModifierRegistry.InvokeFactionPowerModifiers(faction, p);
            factionPowers[faction] = p;
            return p;
        }

        /// <summary>
        /// Returns the cached entry for <paramref name="settlement"/>, computing one (mirrored
        /// from its faction's entry, then settlement-level modifier pass) on the spot if missing.
        /// Catches settlements created between recompute ticks.
        /// </summary>
        public EnemyPower GetOrCompute(Settlement settlement)
        {
            if (settlement is null || settlement.Destroyed) return null;
            /* Empire's own settlements have no enemy estimate (mirrors RecomputeAll). */
            if (settlement is WorldSettlementFC) return null;
            EnemyPower p;
            if (settlementPowers.TryGetValue(settlement, out p)) return p;
            if (settlement.Faction is null) return null;

            EnemyPower factionEntry = GetOrCompute(settlement.Faction);
            if (factionEntry is null) return null;

            p = new EnemyPower();
            CopyFrom(factionEntry, p);
            BattleModifierRegistry.InvokeSettlementPowerModifiers(settlement, p);
            settlementPowers[settlement] = p;
            return p;
        }

        /* === Periodic recompute === */

        /// <summary>
        /// Refresh all faction baselines and settlement entries. Faction-level modifiers run
        /// once per faction; settlement-level modifiers run once per settlement.
        /// </summary>
        public void RecomputeAll()
        {
            // 1. Faction baselines (canonical) + faction-level modifier pass.
            HashSet<Faction> currentFactions = new HashSet<Faction>();
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (f is null || f.def is null) continue;
                if (f.IsPlayer) continue;
                if (FindFC.IsEmpireFaction(f)) continue;
                currentFactions.Add(f);
            }

            List<Faction> staleFactions = factionPowers.Keys.Where(k => !currentFactions.Contains(k)).ToList();
            foreach (Faction k in staleFactions) factionPowers.Remove(k);

            foreach (Faction f in currentFactions)
            {
                EnemyPower fp;
                if (!factionPowers.TryGetValue(f, out fp))
                {
                    fp = new EnemyPower();
                    factionPowers[f] = fp;
                }
                ComputeFactionBaseline(f, fp);
                BattleModifierRegistry.InvokeFactionPowerModifiers(f, fp);
            }

            // 2. Settlement entries: copy from faction baseline, then settlement-level modifier pass.
            HashSet<Settlement> currentSettlements = new HashSet<Settlement>();
            foreach (Settlement s in Find.WorldObjects.Settlements)
            {
                if (s is null || s.Destroyed) continue;
                if (s is WorldSettlementFC) continue;
                if (s.Faction is null) continue;
                currentSettlements.Add(s);
            }

            List<Settlement> staleSettlements = settlementPowers.Keys.Where(k => !currentSettlements.Contains(k)).ToList();
            foreach (Settlement k in staleSettlements) settlementPowers.Remove(k);

            foreach (Settlement s in currentSettlements)
            {
                EnemyPower fp;
                if (!factionPowers.TryGetValue(s.Faction, out fp)) continue;
                EnemyPower sp;
                if (!settlementPowers.TryGetValue(s, out sp))
                {
                    sp = new EnemyPower();
                    settlementPowers[s] = sp;
                }
                CopyFrom(fp, sp);
                BattleModifierRegistry.InvokeSettlementPowerModifiers(s, sp);
            }
        }

        /* === Battle-time helpers (the only call site for InvokeBattleModifiers) === */

        /// <summary>
        /// Resolves the defender force for an op: pulls the settlement entry (or faction entry
        /// if the op has no settlement target), samples within variance, applies battle
        /// modifiers in <paramref name="ctx"/>, and returns. Returns null if no entry can be
        /// resolved (faction destroyed mid-cycle, etc.).
        /// </summary>
        public MilitaryForce ResolveDefenderForceForOp(MilitaryOperation op, BattleForceContext ctx)
        {
            if (op is null || op.defender?.faction is null) return null;

            Settlement target = op.targetObject as Settlement;
            if (target is null && op.targetTile.Valid)
                target = Find.WorldObjects.SettlementAt(op.targetTile);

            EnemyPower entry = target is object
                ? GetOrCompute(target)
                : GetOrCompute(op.defender.faction);
            if (entry is null) return null;

            MilitaryForce force = entry.SampleBattleForce(op.defender.faction);
            // Extra NPC defensive levels: only when the player is the aggressor (player-attacks-NPC),
            // not NPC-vs-NPC or player-defends. Rebuild via the ctor so forceRemaining is recomputed.
            if (op.IsOffensive && FCSettings.extraNPCDefensiveLevels > 0)
            {
                force = new MilitaryForce(force.militaryLevel + FCSettings.extraNPCDefensiveLevels,
                    force.militaryEfficiency, force.homeSettlement, force.homeFaction);
            }
            BattleModifierRegistry.InvokeBattleModifiers(ctx, force, isAttacker: false);
            return force;
        }

        /// <summary>
        /// For the squad picker: build the defender's variance-bounded min/max forces and
        /// run battle modifiers against each. Mutates <paramref name="probeCtx"/>.defender to
        /// reflect the side currently being modified. Returns (null, null) if the target has
        /// no resolvable registry entry.
        /// </summary>
        public (MilitaryForce min, MilitaryForce max) ResolveDefenderBounds(BattleForceContext probeCtx)
        {
            if (probeCtx is null) return (null, null);
            Settlement target = probeCtx.targetObject as Settlement;
            if (target is null || target.Faction is null) return (null, null);

            EnemyPower entry = GetOrCompute(target);
            if (entry is null) return (null, null);

            MilitaryForce min = entry.BuildBoundForce(target.Faction, max: false);
            MilitaryForce max = entry.BuildBoundForce(target.Faction, max: true);

            probeCtx.defender = new MilitaryOperationParticipant
            {
                faction = target.Faction,
                force = min
            };
            BattleModifierRegistry.InvokeBattleModifiers(probeCtx, min, isAttacker: false);
            probeCtx.defender.force = max;
            BattleModifierRegistry.InvokeBattleModifiers(probeCtx, max, isAttacker: false);
            return (min, max);
        }

        /// <summary>
        /// Sanctioned single entry point for applying <see cref="IBattleModifier"/> to an
        /// already-resolved force. Used by the squad picker's per-row attacker invocation
        /// and by <see cref="MilitaryOperation.BeginEngagement"/> when a pre-set force should
        /// only have battle modifiers applied (no fresh sample).
        /// </summary>
        public void ApplyBattleModifiers(BattleForceContext ctx, MilitaryForce force, bool isAttacker)
        {
            if (force is null) return;
            BattleModifierRegistry.InvokeBattleModifiers(ctx, force, isAttacker);
        }

        /* === Def cache lookup === */

        private void BuildDefCachesIfNeeded()
        {
            if (techDefCache is object) return;
            techDefCache = new Dictionary<TechLevel, EnemyPowerTechDef>();
            foreach (EnemyPowerTechDef d in DefDatabase<EnemyPowerTechDef>.AllDefsListForReading)
            {
                if (techDefCache.ContainsKey(d.techLevel))
                    LogUtil.Warning($"Duplicate EnemyPowerTechDef for {d.techLevel}: {d.defName} overrides previous");
                techDefCache[d.techLevel] = d;
            }
            factionDefCache = new Dictionary<FactionDef, EnemyPowerFactionDef>();
            foreach (EnemyPowerFactionDef d in DefDatabase<EnemyPowerFactionDef>.AllDefsListForReading)
            {
                if (d.factionDef is null) continue;
                if (factionDefCache.ContainsKey(d.factionDef))
                    LogUtil.Warning($"Duplicate EnemyPowerFactionDef for {d.factionDef.defName}: {d.defName} overrides previous");
                factionDefCache[d.factionDef] = d;
            }
        }

        /// <summary>
        /// Returns the EnemyPowerTechDef for <paramref name="tl"/>, or null if no XML defines one.
        /// </summary>
        public EnemyPowerTechDef GetTechDef(TechLevel tl)
        {
            BuildDefCachesIfNeeded();
            EnemyPowerTechDef d;
            return techDefCache.TryGetValue(tl, out d) ? d : null;
        }

        /// <summary>
        /// Returns the EnemyPowerFactionDef for <paramref name="fd"/>, or null if no XML defines one.
        /// Sparse lookup: most factions have no override.
        /// </summary>
        public EnemyPowerFactionDef GetFactionDef(FactionDef fd)
        {
            BuildDefCachesIfNeeded();
            if (fd is null) return null;
            EnemyPowerFactionDef d;
            return factionDefCache.TryGetValue(fd, out d) ? d : null;
        }

        /* === Internals === */

        /// <summary>
        /// Resolves the deterministic baseline for <paramref name="faction"/>: the tech-level def
        /// supplies all four values, then any matching faction def overrides them per-field.
        /// </summary>
        private void ComputeFactionBaseline(Faction faction, EnemyPower power)
        {
            double level = 1.0, efficiency = 1.0, levelVariance = 2.0, efficiencyVariance = 0.0;

            if (faction is object && faction.def is object)
            {
                EnemyPowerTechDef techDef = GetTechDef(faction.def.techLevel);
                if (techDef is null)
                {
                    LogUtil.Warning($"No EnemyPowerTechDef for {faction.def.techLevel}; using fallback (1, 1, 2, 0)");
                }
                else
                {
                    level = techDef.level;
                    efficiency = techDef.efficiency;
                    levelVariance = techDef.levelVariance;
                    efficiencyVariance = techDef.efficiencyVariance;
                }

                EnemyPowerFactionDef factionDef = GetFactionDef(faction.def);
                if (factionDef is object)
                {
                    if (factionDef.level.HasValue) level = factionDef.level.Value;
                    if (factionDef.efficiency.HasValue) efficiency = factionDef.efficiency.Value;
                    if (factionDef.levelVariance.HasValue) levelVariance = factionDef.levelVariance.Value;
                    if (factionDef.efficiencyVariance.HasValue) efficiencyVariance = factionDef.efficiencyVariance.Value;
                }
            }

            power.level = level;
            power.efficiency = efficiency;
            power.levelVariance = levelVariance;
            power.efficiencyVariance = efficiencyVariance;
            power.lastComputedTick = Find.TickManager.TicksGame;
        }

        private static void CopyFrom(EnemyPower src, EnemyPower dst)
        {
            dst.level = src.level;
            dst.efficiency = src.efficiency;
            dst.levelVariance = src.levelVariance;
            dst.efficiencyVariance = src.efficiencyVariance;
            dst.lastComputedTick = src.lastComputedTick;
        }
    }
}
