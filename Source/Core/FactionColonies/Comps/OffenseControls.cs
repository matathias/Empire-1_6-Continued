using System.Collections.Generic;
using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    public class WorldObjectCompProperties_OffenseControls : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_OffenseControls()
        {
            compClass = typeof(WorldObjectComp_OffenseControls);
        }
    }

    /// <summary>
    /// Hangs offense controls on a vanilla enemy Settlement while an Empire manual offensive battle
    /// owns its tile: Withdraw + watch-battle on the settlement's own gizmos (<see cref="GetGizmos"/>),
    /// and a "Join Attack" command on a player caravan parked at the tile (<see cref="GetCaravanGizmos"/>).
    /// Inert otherwise, and never on Empire's own settlements (the parent is a <see cref="WorldSettlementFC"/>).
    /// </summary>
    public class WorldObjectComp_OffenseControls : WorldObjectComp
    {
        private BattlefieldContext ActiveOffense()
        {
            if (parent is WorldSettlementFC) return null;   // never on Empire settlements
            BattlefieldContext bf = FindFC.MilitaryManager?.GetBattlefield(parent.Tile);
            return (bf is object && bf.HasOffenseAt()) ? bf : null;
        }

        /// <summary>Caravan-context controls: a "Join Attack" command shown when a player caravan is
        /// on this settlement's tile during an active assault (the offense counterpart of joining a
        /// defense with a caravan). Uses the same icon as the attack gizmo it replaces.</summary>
        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            foreach (Gizmo g in base.GetCaravanGizmos(caravan)) yield return g;

            BattlefieldContext bf = ActiveOffense();
            // bf.endingBattle: the assault is resolving, so no new joiners. The loot linger
            // (awaitingPlayerExit) is intentionally still joinable, so it is not excluded.
            if (bf is null || bf.endingBattle || caravan is null || !caravan.IsPlayerControlled) yield break;

            yield return new Command_Action
            {
                defaultLabel = "FCJoinAttack".Translate(),
                defaultDesc = "FCJoinAttackDesc".Translate(),
                icon = TexLoad.iconMilitary,
                action = () => bf.CaravanJoinAttack(caravan)
            };
        }

        /// <summary>Caravan right-click float-menu option to send a caravan to join the ongoing
        /// assault (the offense counterpart of the defense "Defend" caravan option). The vanilla
        /// "Attack settlement" option is separately suppressed during an active offense
        /// (see CaravanAttackSettlement_SuppressDuringOffense).</summary>
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            foreach (FloatMenuOption o in base.GetFloatMenuOptions(caravan)) yield return o;

            BattlefieldContext bf = ActiveOffense();
            if (bf is null || bf.endingBattle) yield break;
            Settlement settlement = parent as Settlement;
            if (settlement is null) yield break;
            foreach (FloatMenuOption o in WorldSettlementJoinAttackAction.GetFloatMenuOptions(caravan, settlement))
                yield return o;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;

            BattlefieldContext bf = ActiveOffense();
            if (bf is null) yield break;

            // The battle is over once teardown/linger begins -- Withdraw would be a no-op.
            if (!bf.endingBattle && !bf.awaitingPlayerExit)
                yield return new Command_Action
                {
                    defaultLabel = "FCOffenseWithdraw".Translate(),
                    defaultDesc = "FCOffenseWithdrawDesc".Translate(),
                    icon = TexCommand.ClearPrioritizedWork,
                    action = () =>
                    {
                        if (bf.endingBattle || bf.awaitingPlayerExit) return;
                        bf.endingBattle = true;
                        LongEventHandler.QueueLongEvent(() => bf.EndOffense(false, withdrawn: true),
                            "EndingAttack", false, error =>
                            {
                                DelayedErrorWindowRequest.Add("FCErrorEndingAttack".Translate(),
                                    "FCErrorEndingAttackDescription".Translate());
                                LogUtil.Error(error.Message);
                            });
                    }
                };

            if (bf.map is object)
                yield return new Command_Action
                {
                    defaultLabel = "FCOffenseWatchBattle".Translate(),
                    defaultDesc = "FCOffenseWatchBattleDesc".Translate(),
                    icon = TexCommand.Attack,
                    action = () => CameraJumper.TryJump(new GlobalTargetInfo(bf.map.Center, bf.map))
                };
        }
    }
}
