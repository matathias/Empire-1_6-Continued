using FactionColonies.util;
using System;

namespace FactionColonies
{
    public class SimulateBattleFc
    {
        /// <summary>Applies <see cref="FCSettings.defenderAdvantage"/> to the defending force in
        /// place. The single source of truth for the defender bonus; applied once when an
        /// auto-resolve battle is seeded (op-driven or synchronous), never per round.</summary>
        public static void ApplyDefenderAdvantage(MilitaryForce defender)
        {
            if (defender is null) return;
            defender.forceRemaining = Math.Round(defender.forceRemaining * FCSettings.defenderAdvantage);
        }

        /// <summary>
        /// Resolves one battle round in place: rolls via <see cref="SimulateRound"/>, decrements the
        /// losing side on both the live <see cref="MilitaryForce"/> objects and <paramref name="result"/>'s
        /// force counters, and appends a <see cref="RoundEntry"/>. On the round that depletes a side it
        /// also sets <paramref name="result"/>'s <c>winner</c>, <c>totalRounds</c>, and <c>subPhase</c>.
        /// This is the single per-round implementation, shared by the per-round op engine
        /// (<see cref="MilitaryOperation.AdvanceBattleProgress"/>) and the synchronous resolver
        /// (<see cref="ResolveSynchronously"/>).
        /// </summary>
        public static void ResolveOneRound(BattleResult result, MilitaryForce atk, MilitaryForce def,
            IRandProvider rand = null)
        {
            RoundOutcome outcome = SimulateRound(atk, def, rand);
            if (outcome.attackerWonRound)
            {
                def.forceRemaining -= 1;
                result.defenderForceRemaining -= 1;
            }
            else
            {
                atk.forceRemaining -= 1;
                result.attackerForceRemaining -= 1;
            }

            result.rounds.Add(new RoundEntry
            {
                roundNumber = result.rounds.Count + 1,
                attackerRawRoll = outcome.attackerRawRoll,
                defenderRawRoll = outcome.defenderRawRoll,
                attackerScore = outcome.attackerScore,
                defenderScore = outcome.defenderScore,
                attackerWonRound = outcome.attackerWonRound,
                attackerForceAfter = result.attackerForceRemaining,
                defenderForceAfter = result.defenderForceRemaining
            });

            if (result.IsComplete)
            {
                result.winner = result.attackerForceRemaining <= 0
                    ? BattleWinner.Defender : BattleWinner.Attacker;
                result.totalRounds = result.rounds.Count;
                result.subPhase = BattleSubPhase.Resolved;
            }
        }

        /// <summary>
        /// Synchronous full-battle resolution. Seeds a <see cref="BattleResult"/> (applying the
        /// defender advantage), then loops <see cref="ResolveOneRound"/> to completion. Actual
        /// battles run through the per-round op engine
        /// (<see cref="MilitaryOperation.BeginAutoResolveProgress"/>); this function shares the same
        /// <see cref="ResolveOneRound"/> primitive and only exists for tests, which need a deterministic,
        /// one-shot result.
        /// </summary>
        internal static BattleResult ResolveSynchronously(MilitaryForce atk, MilitaryForce def,
            IRandProvider rand = null)
        {
            var result = new BattleResult();
            try
            {
                ApplyDefenderAdvantage(def);

                result.attackerInitialForce = atk.forceRemaining;
                result.defenderInitialForce = def.forceRemaining;
                result.attackerForceRemaining = atk.forceRemaining;
                result.defenderForceRemaining = def.forceRemaining;
                result.attackerEfficiency = atk.militaryEfficiency;
                result.defenderEfficiency = def.militaryEfficiency;
                result.subPhase = BattleSubPhase.RollsInProgress;

                while (!result.IsComplete)
                    ResolveOneRound(result, atk, def, rand);

                if (result.rounds.Count == 0)
                {
                    // A side started at zero force — battle decided with no rounds rolled.
                    result.winner = result.attackerForceRemaining <= 0
                        ? BattleWinner.Defender : BattleWinner.Attacker;
                }
                result.totalRounds = result.rounds.Count;
                result.subPhase = BattleSubPhase.Resolved;
            }
            catch (Exception e)
            {
                LogUtil.Error($"An exception occurred while resolving combat in Empire {Environment.NewLine}[{e}]");
                result.winner = BattleWinner.Error;
            }

            return result;
        }

        /// <summary>
        /// Per-round outcome detail. Returned by <see cref="SimulateRound"/> so callers can
        /// record both the raw d20 roll (1..20) and the dampening-applied final score for
        /// both sides. The round winner is determined by score comparison; force decrement
        /// is the caller's responsibility (see <see cref="FightRound"/> or
        /// <c>MilitaryOperation.AdvanceBattleProgress</c>).
        /// </summary>
        public struct RoundOutcome
        {
            public int attackerRawRoll;
            public int defenderRawRoll;
            public double attackerDampenedEfficiency;
            public double defenderDampenedEfficiency;
            public double attackerScore;
            public double defenderScore;
            public bool attackerWonRound;
        }

        /// <summary>
        /// Roll one round without mutating either force. True d20 (1..20 inclusive) rolled for
        /// each side, multiplied by their dampened efficiency. The higher score wins the round.
        /// On ties the defender wins.
        /// </summary>
        public static RoundOutcome SimulateRound(MilitaryForce MFA, MilitaryForce MFB, IRandProvider rand = null)
        {
            rand = rand ?? new RimWorldRandProvider();
            // True d20: 1..20 inclusive. rand.Range(int, int) follows Verse.Rand semantics
            // (max-exclusive), so pass (1, 21).
            int rawA = rand.Range(1, 21);
            int rawB = rand.Range(1, 21);
            double effA = DampenEfficiency(MFA.militaryEfficiency);
            double effB = DampenEfficiency(MFB.militaryEfficiency);
            double scoreA = rawA * effA;
            double scoreB = rawB * effB;
            return new RoundOutcome
            {
                attackerRawRoll = rawA,
                defenderRawRoll = rawB,
                attackerDampenedEfficiency = effA,
                defenderDampenedEfficiency = effB,
                attackerScore = scoreA,
                defenderScore = scoreB,
                attackerWonRound = scoreA > scoreB
            };
        }

        public static void FightRound(MilitaryForce MFA, MilitaryForce MFB, IRandProvider rand = null)
        {
            RoundOutcome outcome = SimulateRound(MFA, MFB, rand);
            if (outcome.attackerWonRound)
            {
                MFB.forceRemaining -= 1;
            }
            else
            {
                MFA.forceRemaining -= 1;
            }
        }

        private static double DampenEfficiency(double efficiency)
        {
            return 1.0 + (efficiency - 1.0) * FCSettings.efficiencyDamping;
        }

        /// <summary>Mirror of <see cref="CalculateDefenderWinChance"/> from the attacker's side.
        /// Returns the probability that the attacker depletes the defender's HP before being
        /// depleted itself. Stable across calls (deterministic given the two force snapshots).</summary>
        public static double CalculateAttackerWinChance(MilitaryForce attacker, MilitaryForce defender)
        {
            if (attacker is null || defender is null) return 0.0;
            return 1.0 - CalculateDefenderWinChance(attacker, defender);
        }

        /// <summary>
        /// Calculates the probability that the defender wins using the binomial tail sum for a
        /// Bernoulli race (attrition model). The attacker needs <c>defenderHP</c> round-wins to deplete
        /// the defender; the defender needs <c>attackerHP</c> round-wins to deplete the attacker.
        /// <c>P(attacker wins) = P(X &gt;= defenderHP)</c> where <c>X ~ Binomial(attackerHP+defenderHP-1, p)</c>.
        /// Does not account for <see cref="BattleModifierRegistry"/> modifications.
        /// <para>Expects <b>raw / pre-advantage</b> forces: it applies <see cref="FCSettings.defenderAdvantage"/>
        /// to the defender internally. Never pass a force already run through
        /// <see cref="ApplyDefenderAdvantage"/> (double-counts the advantage).</para>
        /// </summary>
        /// <returns>Defender win probability in [0, 1].</returns>
        public static double CalculateDefenderWinChance(MilitaryForce attacker, MilitaryForce defender)
        {
            if (attacker.forceRemaining <= 0) return 1.0;
            if (defender.forceRemaining <= 0) return 0.0;

            int attackerHP = (int)Math.Max(1, Math.Round(attacker.forceRemaining));
            int defenderHP = (int)Math.Max(1, Math.Round(defender.forceRemaining * FCSettings.defenderAdvantage));

            double attackerEfficiency = DampenEfficiency(attacker.militaryEfficiency);
            double defenderEfficiency = DampenEfficiency(defender.militaryEfficiency);

            // P(attacker wins a single round) for Uniform(0, 20*attackerEfficiency) vs Uniform(0, 20*defenderEfficiency)
            double p;
            if (attackerEfficiency <= defenderEfficiency)
                p = attackerEfficiency / (2.0 * defenderEfficiency);
            else
                p = 1.0 - defenderEfficiency / (2.0 * attackerEfficiency);

            if (p <= 0.0) return 1.0;
            if (p >= 1.0) return 0.0;

            double q = 1.0 - p;
            // n = the maximum possible number of rounds
            int n = attackerHP + defenderHP - 1;

            // Sum P(X >= defenderHP) where X ~ Binomial(n, p), iterating from k=n down to k=defenderHP.
            // term(n) = p^n, then term(k) = term(k+1) * (k+1)/(n-k) * q/p
            double term = Math.Pow(p, n);
            double sum = term;
            for (int k = n - 1; k >= defenderHP; k--)
            {
                term *= (k + 1.0) / (n - k) * (q / p);
                sum += term;
            }

            return Math.Max(0.0, Math.Min(1.0, 1.0 - sum));
        }
    }
}