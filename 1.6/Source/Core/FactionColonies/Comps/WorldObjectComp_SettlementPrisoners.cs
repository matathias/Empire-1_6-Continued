using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace FactionColonies
{
    public class WorldObjectCompProperties_SettlementPrisoners : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_SettlementPrisoners()
        {
            compClass = typeof(WorldObjectComp_SettlementPrisoners);
        }

        public override IEnumerable<string> ConfigErrors(WorldObjectDef parentDef)
        {
            foreach (string item in base.ConfigErrors(parentDef))
            {
                yield return item;
            }
            if (!typeof(MapParent).IsAssignableFrom(parentDef.worldObjectClass))
            {
                yield return parentDef.defName + " has WorldObjectCompProperties_SettlementPrisoners but it's not MapParent.";
            }
        }
    }

    /* Owns this settlement's FCPrisoner list and its daily health tick.
       Also emits the "Transfer prisoner to settlement" gizmo when a player
       caravan sits on the settlement's tile with at least one pawn flagged
       IsPrisonerOfColony. The gizmo is visible from both sides: selecting the
       caravan (GetCaravanGizmos) and selecting the settlement (GetGizmos). */
    public class WorldObjectComp_SettlementPrisoners : WorldObjectComp_SettlementPawnArrival
    {
        public List<FCPrisoner> prisonerList = new List<FCPrisoner>();
        public WorldSettlementFC WorldSettlement => parent as WorldSettlementFC;

        /* Per-settlement override of FactionFC.defaultPrisonerWorkload. When
         * hasDefaultWorkloadOverride is false the settlement inherits the faction value. */
        public bool hasDefaultWorkloadOverride;
        public FCWorkLoad defaultWorkloadOverride = FCWorkLoad.Light;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref prisonerList, "prisoners", LookMode.Deep);
            if (prisonerList is null) prisonerList = new List<FCPrisoner>();
            Scribe_Values.Look(ref hasDefaultWorkloadOverride, "hasDefaultWorkloadOverride", false);
            Scribe_Values.Look(ref defaultWorkloadOverride, "defaultWorkloadOverride", FCWorkLoad.Light);
        }

        public FCWorkLoad GetEffectiveDefaultWorkload()
        {
            if (hasDefaultWorkloadOverride) return defaultWorkloadOverride;
            FactionFC comp = FindFC.FactionComp;
            return comp?.defaultPrisonerWorkload ?? FCWorkLoad.Light;
        }

        public void SetDefaultWorkloadOverride(FCWorkLoad w)
        {
            hasDefaultWorkloadOverride = true;
            defaultWorkloadOverride = w;
        }

        public void ClearDefaultWorkloadOverride()
        {
            hasDefaultWorkloadOverride = false;
        }

        public override void CompTick()
        {
            // base.CompTick() is empty in Rimworld 1.6.
            if (Find.TickManager.TicksGame % GenDate.TicksPerDay == 0)
            {
                AdvanceDailyHealth();
            }
        }

        /* Daily health update. Heavy workload damages, Light heals. Two-phase to keep
         * the iteration over prisonerList safe: first pass applies deltas and gathers any
         * dead prisoners into a scratch list; second pass processes the deaths. */
        public void AdvanceDailyHealth()
        {
            if (prisonerList is null) return;

            List<FCPrisoner> dead = null;
            foreach (FCPrisoner p in prisonerList)
            {
                p.AdjustHealth(p.workload);
                if (p.IsDead)
                {
                    if (dead is null) dead = new List<FCPrisoner>();
                    dead.Add(p);
                }
            }

            if (dead != null)
            {
                string sName = WorldSettlement?.Name ?? "";
                Faction player = Find.FactionManager.OfPlayer;
                Faction empire = FindFC.EmpireFaction;
                const int goodwillPerDeath = -2;

                List<string> deadNames = new List<string>(dead.Count);
                Dictionary<Faction, int> goodwillByFaction = new Dictionary<Faction, int>();

                for (int i = 0; i < dead.Count; i++)
                {
                    string name;
                    Faction home;
                    DropDeadPrisoner(dead[i], out name, out home);
                    deadNames.Add(name);

                    /* Skip non-relations factions: null, the player itself, our allied empire,
                     * hidden factions (mechs etc.), permanent enemies (goodwill is locked). */
                    if (home is null || home == player || home == empire
                        || home.Hidden || home.def.permanentEnemy) continue;

                    bool applied = home.TryAffectGoodwillWith(
                        player, goodwillPerDeath,
                        canSendMessage: false, canSendHostilityLetter: false,
                        reason: HistoryEventDefOf.PrisonerDied);
                    if (applied)
                    {
                        int total;
                        if (!goodwillByFaction.TryGetValue(home, out total)) total = 0;
                        goodwillByFaction[home] = total + goodwillPerDeath;
                    }
                }

                SendDeathLetter(sName, deadNames, goodwillByFaction);
            }

            // A Light-workload heal may have un-downed a prisoner, so refresh the worker cap.
            WorldSettlement?.NotifyWorkforceChanged();
        }

        /* The single removal entry point — no other code should call prisonerList.Remove
         * directly. Null `p` is allowed (and removes the first null entry) so CullNullPrisoners
         * can route degenerate null FCPrisoner entries through this method too. */
        public bool RemovePrisoner(FCPrisoner p)
        {
            if (prisonerList is null) return false;
            bool removed = prisonerList.Remove(p);
            if (removed) WorldSettlement?.NotifyWorkforceChanged();
            return removed;
        }

        /* Removes the prisoner and returns the data the death-letter builder needs.
         * homeFaction is the prisoner's original faction (pawn.guest.HomeFaction) — used
         * by the caller to apply a goodwill penalty against the player. */
        private void DropDeadPrisoner(FCPrisoner p, out string pawnName, out Faction homeFaction)
        {
            pawnName    = p.prisoner?.Name?.ToString() ?? "";
            homeFaction = p.prisoner?.Faction;
            RemovePrisoner(p);
        }

        private static void SendDeathLetter(
            string settlementName,
            List<string> deadNames,
            Dictionary<Faction, int> goodwillByFaction)
        {
            string title;
            string body;
            if (deadNames.Count == 1)
            {
                // Preserve existing single-death wording.
                title = "FCPrisonerHasDiedLetter".Translate();
                body  = "FCPrisonerHasDied".Translate(deadNames[0], settlementName);
            }
            else
            {
                StringBuilder header = new StringBuilder();
                header.AppendLine("FCPrisonersHaveDiedBody".Translate(deadNames.Count, settlementName));
                header.AppendLine();
                foreach (string deadname in deadNames)
                    header.AppendLine("  - " + deadname);
                title = "FCPrisonersHaveDiedLetter".Translate();
                body  = header.ToString().TrimEnd();
            }

            if (goodwillByFaction.Count > 0)
            {
                StringBuilder sb = new StringBuilder(body);
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("FCPrisonerDeathGoodwillHeader".Translate());
                foreach (KeyValuePair<Faction, int> kvp in goodwillByFaction)
                    sb.AppendLine("  " + kvp.Key.Name + ": " + kvp.Value);
                body = sb.ToString().TrimEnd();
            }

            Find.LetterStack.ReceiveLetter(title, body, LetterDefOf.NeutralEvent);
        }

        public void AddPrisoner(Pawn pawn)
        {
            WorldSettlementFC settlement = WorldSettlement;
            if (settlement is null || pawn is null) return;

            // FCPrisoner is the canonical deep owner of the held pawn. If WorldPawns
            // already has it (e.g., redressed by PawnGenerator, passed via LeaveMap,
            // dropped from a caravan), pull it out so save doesn't double-scribe.
            // The conditional Scribe in FCPrisoner.ExposeData defends on-map cases.
            // Detect the pawn's captive type before construction — the FCPrisoner ctor
            // overwrites guest status, so IsSlaveOfColony must be read first.
            bool wasSlave = pawn.IsSlaveOfColony;
            if (Find.WorldPawns is object && Find.WorldPawns.Contains(pawn))
            {
                Find.WorldPawns.RemovePawn(pawn);
            }
            FCPrisoner created = new FCPrisoner(pawn, settlement, wasSlave);
            created.workload = GetEffectiveDefaultWorkload();
            prisonerList.Add(created);
            settlement.DirtyStatsCache();
        }

        /* Removes any FCPrisoner entries that are themselves null or wrap a null pawn. */
        public int CullNullPrisoners()
        {
            if (prisonerList is null) return 0;

            List<FCPrisoner> toRemove = null;
            foreach (FCPrisoner p in prisonerList)
            {
                if (p?.prisoner is null)
                {
                    if (toRemove is null) toRemove = new List<FCPrisoner>();
                    toRemove.Add(p);
                }
            }

            if (toRemove is null) return 0;
            foreach (FCPrisoner p in toRemove) RemovePrisoner(p);

            LogUtil.Warning("Culled " + toRemove.Count + " null prisoner(s) from " + (WorldSettlement?.Name ?? "<unknown>"));
            return toRemove.Count;
        }

        public void TransferFromCaravan(Pawn pawn, Caravan caravan)
        {
            if (pawn is null || caravan is null) return;
            caravan.RemovePawn(pawn);
            caravan.Notify_PawnRemoved(pawn);
            AddPrisoner(pawn);
        }

        public void DoTransferMenu(Caravan caravan)
        {
            if (caravan is null) return;

            List<FloatMenuOption> list = new List<FloatMenuOption>();
            List<Pawn> pawns = caravan.PawnsListForReading;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn captured = pawns[i];
                if (!captured.IsPrisonerOfColony && !captured.IsSlaveOfColony) continue;
                string typeLabel = (captured.IsSlaveOfColony ? "FCCaptiveTypeSlave" : "FCCaptiveTypePrisoner").Translate();
                list.Add(new FloatMenuOption(
                    "FCTransferPrisonerOption".Translate(captured.Name.ToStringShort) + " (" + typeLabel + ")",
                    delegate { TransferFromCaravan(captured, caravan); }));
            }

            if (list.Count == 0)
            {
                Messages.Message("FCNoPrisonersInCaravan".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(list));
        }

        public void SetWorkload(FCPrisoner p, FCWorkLoad workload)
        {
            if (p is null) return;
            p.workload = workload;
            WorldSettlement?.DirtyStatsCache();
        }

        public void SellPrisoner(FCPrisoner p)
        {
            if (p?.prisoner is null) return;
            WorldSettlement?.AddOneTimeSilverIncome(p.prisoner.MarketValue);
            RemovePrisoner(p);
        }

        public void ReturnPrisonerToPlayer(FCPrisoner p)
        {
            if (p?.prisoner is null) return;
            WorldSettlementFC s = WorldSettlement;
            if (s is null) return;

            if (!HealthUtility.TryAnesthetize(p.prisoner))
                HealthUtility.DamageUntilDowned(p.prisoner, false);

            if (p.prisoner.guest is null)
                p.prisoner.guest = new Pawn_GuestTracker(p.prisoner);
            p.prisoner.guest.SetGuestStatus(Find.FactionManager.OfPlayer, p.isSlave ? GuestStatus.Slave : GuestStatus.Prisoner);

            DeliveryEvent.CreateDeliveryEvent(new FCEvent
            {
                location = Find.AnyPlayerHomeMap.Tile,
                source = s.Tile,
                goods = new List<Thing> { p.prisoner },
                customDescription = "FCAPrisonerIsBeingDeliveredToYou".Translate(),
                timeTillTrigger = Find.TickManager.TicksGame + TravelUtil.ReturnTicksToArrive(s.Tile, Find.AnyPlayerHomeMap.Tile)
            });

            RemovePrisoner(p);
        }

        public void DoActionsMenu(FCPrisoner p, Action onRemoved)
        {
            if (p is null) return;
            List<FloatMenuOption> list = new List<FloatMenuOption>();

            if (FindFC.FactionComp.IsActionAllowed(FCActionType.SellPrisoner))
            {
                list.Add(new FloatMenuOption(
                    "FCSellPawn".Translate() + " $" + p.prisoner.MarketValue + " " + "FCSellPawnInfo".Translate(),
                    delegate
                    {
                        SellPrisoner(p);
                        onRemoved?.Invoke();
                    }));
            }

            list.Add(new FloatMenuOption("FCReturnToPlayer".Translate(), delegate
            {
                ReturnPrisonerToPlayer(p);
                onRemoved?.Invoke();
            }));

            Find.WindowStack.Add(new FloatMenu(list));
        }

        public void OpenWorkloadFloatMenu(FCPrisoner p)
        {
            if (p is null) return;
            Find.WindowStack.Add(new FloatMenu(
                FCWorkLoadInfo.BuildSelectionMenu(delegate (FCWorkLoad w) { SetWorkload(p, w); })));
        }

        /* Sets this settlement's default-workload override for newly captured prisoners.
         * The "Use faction default" option clears the override so the settlement inherits
         * FactionFC.defaultPrisonerWorkload again. */
        public void OpenDefaultWorkloadFloatMenu()
        {
            List<FloatMenuOption> list = FCWorkLoadInfo.BuildSelectionMenu(SetDefaultWorkloadOverride);
            if (hasDefaultWorkloadOverride)
            {
                list.Add(new FloatMenuOption("FCUseFactionDefault".Translate(),
                    delegate { ClearDefaultWorkloadOverride(); }));
            }
            Find.WindowStack.Add(new FloatMenu(list));
        }

        /* Bulk-applies the chosen workload to every prisoner in this settlement.
         * Routes each assignment through SetWorkload so DirtyStatsCache fires correctly. */
        public void OpenBulkSetWorkloadFloatMenu()
        {
            Find.WindowStack.Add(new FloatMenu(FCWorkLoadInfo.BuildSelectionMenu(BulkSetWorkload)));
        }

        public void BulkSetWorkload(FCWorkLoad w)
        {
            if (prisonerList is null) return;
            for (int i = 0; i < prisonerList.Count; i++) SetWorkload(prisonerList[i], w);
        }

        /* Downed prisoners can't perform any work, so they're excluded from worker contributions. */
        public int ReturnMaxWorkersFromPrisoners()
        {
            int num = 0;
            foreach (FCPrisoner p in prisonerList)
            {
                if (p?.prisoner is null || p.prisoner.Downed) continue;
                num += FCWorkLoadInfo.WorkerSlots(p.workload);
            }
            return num;
        }

        public int ReturnOverMaxWorkersFromPrisoners()
        {
            int num = 0;
            foreach (FCPrisoner p in prisonerList)
            {
                if (p?.prisoner is null || p.prisoner.Downed) continue;
                num += FCWorkLoadInfo.OverMaxSlots(p.workload);
            }
            return num;
        }

        /* -*-*-*- Pawn arrival via transport pod (WorldObjectComp_SettlementPawnArrival) -*-*-*- */

        /* Gate on the same SendPrisoner permission as the caravan-transfer gizmo so the pod path
           and the caravan path stay consistent. (The battle/raid block is enforced centrally in
           TransportersArrivalAction_AddToSettlementFC.) */
        public override FloatMenuAcceptanceReport CanReceive
            => FindFC.FactionComp?.IsActionAllowed(FCActionType.SendPrisoner) ?? false;

        public override bool AcceptsPawn(Pawn pawn) => pawn is object && (pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony);

        public override string ArrivalMenuLabel => "FCAddPrisonersToSettlement".Translate(parent.Label);

        public override string ArrivalMessage(List<Pawn> pawns)
            => "FCPawnsAddedToSettlement".Translate(pawns.Count, parent.Label);

        public override void ReceivePawns(List<Pawn> pawns)
        {
            if (pawns is null) return;
            for (int i = 0; i < pawns.Count; i++) AddPrisoner(pawns[i]);
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            foreach (Gizmo gizmo in base.GetCaravanGizmos(caravan))
            {
                yield return gizmo;
            }

            if (WorldSettlement is null) yield break;
            if (caravan is null || caravan.Tile != parent.Tile) yield break;
            if (FindFC.FactionComp is null) yield break;
            if (!FindFC.FactionComp.IsActionAllowed(FCActionType.SendPrisoner)) yield break;
            if (!PrisonerUtil.HasCapturedPawns(caravan)) yield break;

            yield return BuildTransferGizmo(caravan);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (WorldSettlement is null) yield break;
            if (FindFC.FactionComp is null) yield break;
            if (!FindFC.FactionComp.IsActionAllowed(FCActionType.SendPrisoner)) yield break;

            Caravan caravan = Find.WorldObjects.PlayerControlledCaravanAt(parent.Tile);
            if (caravan is null) yield break;
            if (!PrisonerUtil.HasCapturedPawns(caravan)) yield break;

            yield return BuildTransferGizmo(caravan);
        }

        private Command_Action BuildTransferGizmo(Caravan caravan)
        {
            return new Command_Action
            {
                defaultLabel = "FCTransferPrisonerToSettlement".Translate(),
                defaultDesc = "FCTransferPrisonerGizmoDesc".Translate(),
                icon = TexLoad.iconMilitary,
                action = delegate
                {
                    DoTransferMenu(caravan);
                }
            };
        }
    }
}
