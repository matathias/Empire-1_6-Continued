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
    /// Hangs the Withdraw + watch-battle controls on a vanilla enemy Settlement while an Empire
    /// manual offensive battle owns its tile. Inert otherwise: <see cref="GetGizmos"/> early-returns
    /// unless an active offense <see cref="BattlefieldContext"/> owns this tile AND the parent is not
    /// a <see cref="WorldSettlementFC"/> (so these controls never appear on Empire's own settlements).
    /// </summary>
    public class WorldObjectComp_OffenseControls : WorldObjectComp
    {
        private BattlefieldContext ActiveOffense()
        {
            if (parent is WorldSettlementFC) return null;   // never on Empire settlements
            BattlefieldContext bf = FindFC.MilitaryManager?.GetBattlefield(parent.Tile);
            return (bf is object && bf.HasOffenseAt()) ? bf : null;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;

            BattlefieldContext bf = ActiveOffense();
            if (bf is null) yield break;

            yield return new Command_Action
            {
                defaultLabel = "FCOffenseWithdraw".Translate(),
                defaultDesc = "FCOffenseWithdrawDesc".Translate(),
                icon = TexCommand.ClearPrioritizedWork,
                action = () =>
                {
                    if (bf.endingBattle || bf.awaitingPlayerExit) return;
                    bf.endingBattle = true;
                    LongEventHandler.QueueLongEvent(() => bf.EndOffense(false),
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
