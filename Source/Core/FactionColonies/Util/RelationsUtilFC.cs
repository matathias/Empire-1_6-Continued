using RimWorld;
using System;
using Verse;

namespace FactionColonies
{
    public class RelationsUtilFC
    {
        public static void AttackFaction(Faction faction)
        {
            Faction player = Find.FactionManager?.OfPlayer;
            if (player is null || faction is null) return;
            player.TryAffectGoodwillWith(faction, -50);
            TrySetRelationKind(player, faction, FactionRelationKind.Hostile);
        }

        public static void ResetPlayerColonyRelations()
        {
            Faction PCFaction = FindFC.EmpireFaction;
            Faction player = Find.FactionManager?.OfPlayer;
            if (PCFaction is null || player is null) return;   // nothing to sync

            foreach (Faction faction in Find.FactionManager.AllFactionsInViewOrder)
            {
                //skip null entries, the player faction, and the player colony faction
                if (faction is null || faction == player || faction == PCFaction) continue;

                PCFaction.TryAffectGoodwillWith(faction,
                    (player.RelationWith(faction).baseGoodwill -
                     PCFaction.RelationWith(faction).baseGoodwill));
                TrySetRelationKind(PCFaction, faction, player.RelationKindWith(faction));
            }
        }

        internal static bool TrySetRelationKind(Faction self, Faction other, FactionRelationKind kind, bool canSendLetter = true)
        {
            if (self is null || other is null) return false;
            FactionRelation factionRelation = self.RelationWith(other);
            if (factionRelation.kind == kind)
            {
                return true;
            }
            if (!self.HasGoodwill)
            {
                self.SetRelationDirect(other, kind, canSendLetter);
                return true;
            }
            switch (kind)
            {
                case FactionRelationKind.Hostile:
                    self.TryAffectGoodwillWith(other, -75 - factionRelation.baseGoodwill, canSendMessage: false, canSendLetter);
                    return factionRelation.kind == FactionRelationKind.Hostile;
                case FactionRelationKind.Neutral:
                    self.TryAffectGoodwillWith(other, -factionRelation.baseGoodwill, canSendMessage: false, canSendLetter);
                    return factionRelation.kind == FactionRelationKind.Neutral;
                case FactionRelationKind.Ally:
                    self.TryAffectGoodwillWith(other, 75 - factionRelation.baseGoodwill, canSendMessage: false, canSendLetter);
                    return factionRelation.kind == FactionRelationKind.Ally;
                default:
                    throw new NotSupportedException(kind.ToString());
            }
        }
    }
}