using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// One side's contribution to a <see cref="MilitaryOperation"/>: which empire settlement and squad
    /// (if any) is involved, the force snapshot used for simulation, and the on-map pawns/lord during
    /// a manual battle.
    /// <para>Either side of an op (aggressor or defender) is a <c>MilitaryOperationParticipant</c>;
    /// the only structural difference is which side the participant is on, captured by
    /// <see cref="ParticipantSide"/>.</para>
    /// </summary>
    public class MilitaryOperationParticipant : IExposable
    {
        /// <summary>Empire settlement supplying this side, if any. Null for non-Empire forces (raiding faction, ad-hoc militia).</summary>
        public WorldSettlementFC homeSettlement;

        /// <summary>Squad assigned to this participation, if any. Null for non-squad forces (auto-defenders, ad-hoc militia).</summary>
        public MercenarySquadFC squad;

        /// <summary>Snapshot of force strength used by the battle simulator (<see cref="SimulateBattleFc"/>).</summary>
        public MilitaryForce force;

        /// <summary>Faction this side belongs to.</summary>
        public Faction faction;

        /// <summary>Pawns spawned on the battlefield map for manual battles. Empty for auto-resolved ops.</summary>
        public List<Pawn> pawns = new List<Pawn>();

        /// <summary>Initial spawn-time count of pawns on this side. Set as pawns are added to
        /// <see cref="pawns"/>; never decremented. Reading <c>initialPawnCount</c> minus the
        /// current <c>pawns.Count</c> gives the cumulative casualty count for this op's side.
        /// Used by <c>MilitaryOperation.CompleteBattle</c> for overwhelming-victory detection
        /// in manual battles.</summary>
        public int initialPawnCount;

        public MilitaryOperationParticipant() { }

        public MilitaryOperationParticipant(MilitaryForce force, Faction faction,
            WorldSettlementFC homeSettlement = null, MercenarySquadFC squad = null)
        {
            this.force = force;
            this.faction = faction;
            this.homeSettlement = homeSettlement;
            this.squad = squad;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref homeSettlement, "homeSettlement");
            Scribe_References.Look(ref squad, "squad");
            Scribe_Deep.Look(ref force, "force");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_Values.Look(ref initialPawnCount, "initialPawnCount", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && pawns is null)
                pawns = new List<Pawn>();
        }
    }
}
