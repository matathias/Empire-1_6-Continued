using LudeonTK;
using UnityEngine;
using Verse.AI.Group;

namespace FactionColonies;

public static class MilitaryDeploymentUtil
{
    /* Silver deploy-cost for a given equipment value, applying the configured
     * FCSettings.squadDeploymentCostPercentage. Single source of truth for the
     * deploy-cost formula — used by the live squad deploy bill, the settlement
     * max-deploy-cost badge, the design-window deploy preview, and the
     * over-budget assignment rejection message. */
    public static int CalculateDeploymentCost(double squadEquipmentCost)
    {
        return (int)Math.Round(squadEquipmentCost * FCSettings.squadDeploymentCostPercentage);
    }

    /// <summary>
    /// Internal method used to spawn a <paramref name="settlement"/>'s squad for military deployment
    /// </summary>
    /// <param name="settlement"></param>
    /// <param name="squad"></param>
    /// <param name="dropPosition"></param>
    /// <param name="DropPod"></param>
    /// <param name="bill">The deployment-cost bill created for this deployment, or
    /// <c>null</c> if none was created (zero cost / godMode). Controls whether the
    /// deployment letter mentions the cost and payment deadline.</param>
    private static void SpawnSquad(WorldSettlementFC settlement, MercenarySquadFC squad, IntVec3 dropPosition, bool DropPod, BillFC bill)
    {
        if (settlement.MilitaryComp is null)
        {
            LogUtil.Warning($"SpawnSquad called on settlement {settlement.Name} with no MilitaryComp. Aborting.");
            return;
        }

        Map currentMap = Find.CurrentMap;
        if (currentMap is null)
        {
            LogUtil.Warning("SpawnSquad: Find.CurrentMap is null. Cannot deploy squad.");
            return;
        }

        IncidentParms parms = new()
        {
            target = currentMap,
            faction = FindFC.EmpireFaction,
            podOpenDelay = 140,
            points = 999,
            raidArrivalModeForQuickMilitaryAid = true,
            raidNeverFleeIndividual = true,
            //raidForceOneIncap = true,
            raidArrivalMode = PawnsArrivalModeDefOf.CenterDrop,
            raidStrategy = RaidStrategyDefOf.ImmediateAttackFriendly
        };

        // SpawnableMercenaryPawns filters downed pawns out — they stay at base to recover.
        List<Pawn> equippedPawns = squad.SpawnableMercenaryPawns.ToList();

        if (DropPod)
        {
            parms.spawnCenter = dropPosition;
            PawnsArrivalModeWorkerUtility.DropInDropPodsNearSpawnCenter(parms, equippedPawns);
        }
        else
        {
            PawnsArrivalModeWorker_EdgeWalkIn worker = new();
            if (RCellFinder.TryFindClosestEdgeCellTo(dropPosition, currentMap, out parms.spawnCenter))
            {
                parms.spawnRotation = Rot4.FromAngleFlat((currentMap.Center - parms.spawnCenter).AngleFlat);
            }
            else
            {
                // dropPosition is unreachable from any map edge (e.g. fully sealed off) — fall back to vanilla random edge cell.
                worker.TryResolveRaidSpawnCenter(parms);
            }
            worker.Arrive(equippedPawns, parms);
        }

        equippedPawns.ForEach(pawn => pawn.ApplyIdeologyRitualWounds());

        // Apply the squad's combat-efficiency hediff to the deployed pawns, mirroring the
        // manual-battle pipeline (BattlefieldContext). The hediff is stripped automatically
        // on every map-exit path by the StripCombatEfficiencyOnDeSpawn patch on Pawn.DeSpawn,
        // so no explicit recall cleanup is needed.
        SquadSpawnUtil.ApplyCombatEfficiency(equippedPawns,
            MilitaryForce.CreateMilitaryForceFromSquad(squad)?.militaryEfficiency ?? 1.0);
        // Load reloadable-weapon magazines from carried ammo (e.g. Yayo's Combat); no-op otherwise.
        foreach (Pawn pawn in equippedPawns)
            ReloadableWeaponUtil.LoadMagazinesFromInventory(pawn);

        // Start the deployment from a clean order. MilitaryOrder is persistent squad state, so
        // a leftover order from a PRIOR deployment (e.g. RecoverWoundedAndLeave from a dismiss)
        // would otherwise make this new lord execute it the moment it's ready — the squad would
        // turn around and leave as soon as it arrived.
        squad.Deployment.MilitaryOrder = MilitaryOrder.Undefined;
        squad.Deployment.OrderLocation = dropPosition;
        squad.Deployment.Map = currentMap;
        // Squad-first: name the deployed squad and its home settlement. When a deployment-cost
        // bill was created, also surface the cost and payment deadline; otherwise omit that
        // sentence (no bill when cost is 0% or godMode is on).
        string deploymentDesc = bill is not null
            ? "FCDeploymentSuccessDesc".Translate(squad.DisplayName, squad.settlement?.Name, currentMap.Parent.LabelCap, squad.DeploymentCost(), FCSettings.deploymentBillLifespan_days)
            : "FCDeploymentSuccessDescNoBill".Translate(squad.DisplayName, squad.settlement?.Name, currentMap.Parent.LabelCap);
        Find.LetterStack.ReceiveLetter("FCDeploymentSuccessLabel".Translate(), deploymentDesc, LetterDefOf.NeutralEvent, new LookTargets(equippedPawns));
        FindFC.MilitaryManager?.CreateDeployOp(squad, currentMap.Tile);

        // Mounts (Giddy Up 2): map each mounted merc to its mount animal so the deploy lord can mount
        // them once they spawn. Only mount-typed sub-pawns; companion animals deploy and fight on foot.
        Dictionary<Pawn, Pawn> mounts = squad.BuildMountMap();

        LordMaker.MakeNewLord(FindFC.EmpireFaction, new LordJob_DeployMilitary(dropPosition, squad, mounts), currentMap, equippedPawns);
    }

    /// <summary>
    /// Deploys a <paramref name="settlement"/>'s main force, or <paramref name="overrideSquad"/> when given.
    /// <paramref name="extraCost"/> is charged up front when the player confirms the drop position.
    /// </summary>
    /// <param name="settlement"></param>
    /// <param name="DropPod"></param>
    /// <param name="overrideSquad"></param>
    /// <param name="extraCost"></param>
    public static void CallinAlliedForces(WorldSettlementFC settlement, bool DropPod, MercenarySquadFC overrideSquad = null, int extraCost = 0)
    {
        MercenarySquadFC squad = overrideSquad
            ?? settlement?.FirstAvailableStationedSquad
            ?? settlement?.PrimaryStationedSquad;

        if (squad is null)
        {
            LogUtil.Warning($"Attempted to call in allied forces for settlement {settlement?.Name} with no squad. Skipping");
            return;
        }

        /* Morale lockout: refuse the deploy before positioning / any deployment-cost bill. */
        if (settlement is not null && settlement.TryGetSquadDeploymentBlock(out string lockReason))
        {
            Messages.Message(lockReason, MessageTypeDefOf.RejectInput);
            return;
        }

        SquadSpawnUtil.PrepareSquad(squad, settlement);

        IntVec3 dropPosition;
        DebugTool tool = new("FCSelectDeploymentPosition".Translate(), delegate
        {
            dropPosition = UI.MouseCell();
            Map curMap = Find.CurrentMap;

            if (!dropPosition.InBounds(curMap))
            {
                Messages.Message("FCSelectedPosOutOfBounds".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            if (dropPosition.CloseToEdge(curMap, 10))
            {
                Messages.Message("FCSelectedPosTooCloseToEdge".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // Morale can drop while the placement tool is open. The deploy op checks the squad's own
            // settlement, so check it here too, before any silver is taken or a bill is created.
            WorldSettlementFC lockSettlement = squad.settlement ?? settlement;
            if (lockSettlement is not null && lockSettlement.TryGetSquadDeploymentBlock(out string clickLockReason))
            {
                Messages.Message(clickLockReason, MessageTypeDefOf.RejectInput);
                DebugTools.curTool = null;
                return;
            }

            if (extraCost > 0 && !PaymentUtil.TryPaySilver(extraCost, PaymentUtil.Reason_SquadDeployment, settlement))
            {
                Messages.Message("FCNotEnoughSilverToDeploySquad".Translate(), MessageTypeDefOf.RejectInput);
                DebugTools.curTool = null;
                return;
            }

            BillFC deploymentBill = FindFC.TaxLedger.CreateDeploymentCostBill(squad);
            SpawnSquad(settlement, squad, dropPosition, DropPod, deploymentBill);
            DebugTools.curTool = null;
        });
        DebugTools.curTool = tool;

        //UI.UIToMapPosition(UI.MousePositionOnUI).ToIntVec3();
    }

    /// <summary>
    /// Deploys the secondary military of the empire from a <paramref name="settlement"/> 
    /// </summary>
    /// <param name="settlement"></param>
    /// <param name="DropPod"></param>
    /// <param name="cost">Silver charged when the player confirms the drop position.</param>
    public static void CallinExtraForces(WorldSettlementFC settlement, bool DropPod, int cost)
    {
        /* Morale lockout: refuse before creating the temp squad (CallinAlliedForces also gates,
         * but gating here avoids orphaning a freshly-created extra squad). */
        if (settlement is not null && settlement.TryGetSquadDeploymentBlock(out string lockReason))
        {
            Messages.Message(lockReason, MessageTypeDefOf.RejectInput);
            return;
        }

        // An extra squad whose placement tool was cancelled never got an op; clear it out first.
        FindFC.Military.RemoveIdleExtraSquads();

        // Copy the outfit from the settlement's primary stationed squad (any squad with an
        // outfit will do — we just need a template to clone the gear from).
        MilSquadFC mainOutfit = settlement?.PrimaryStationedSquad?.outfit;
        if (mainOutfit is null) return;
        MercenarySquadFC squad = FindFC.Military.CreateMercenarySquad(settlement, true, mainOutfit);
        if (squad is null) return;
        CallinAlliedForces(settlement, DropPod, squad, cost);
    }
    public static void FireSupport(WorldSettlementFC settlement, MilitaryFireSupport support)
    {
        TargetingParameters targetParams = new()
        {
            canTargetLocations = true,
            canTargetSelf = false,
            canTargetPawns = false,
            canTargetFires = false,
            canTargetBuildings = false,
            canTargetItems = false
        };

        Find.Targeter.BeginTargeting(targetParams,
            delegate (LocalTargetInfo target)
            {
                float cost = support.ReturnTotalCost(settlement);
                // godMode short-circuits before TryPaySilver, so no silver is taken under godMode.
                if (DebugSettings.godMode
                    || PaymentUtil.TryPaySilver((int)Math.Round(cost), PaymentUtil.Reason_FireSupport, settlement))
                {
                    Map map = Find.CurrentMap;
                    List<ThingDef> projectiles = new(support.projectiles);
                    MilitaryFireSupport fireSupport = new("fireSupport", map, target.Cell,
                        projectiles.Count() * 15, 600, support.accuracy, projectiles, settlement.Tile);
                    // Active strikes are deep-saved as load-referenceables, so each needs its own ID.
                    fireSupport.SetLoadID();
                    FindFC.Military.fireSupport.Add(fireSupport);

                    Messages.Message("FCFireSupportNameWillBeFiredOnPosition".Translate(support.name), MessageTypeDefOf.ThreatSmall);
                    if (settlement.MilitaryComp is not null)
                        settlement.MilitaryComp.artilleryTimer = Find.TickManager.TicksGame + (DebugSettings.godMode ? 1 : 60000);
                }
                else
                {
                    Messages.Message("FCFireSupportNoSilver".Translate(), MessageTypeDefOf.RejectInput);
                }
            },
            highlightAction: delegate (LocalTargetInfo target)
            {
                if (target.Cell.IsValid)
                    GenDraw.DrawRadiusRing(target.Cell, support.accuracy, Color.red);
            },
            targetValidator: null,
            onGuiAction: delegate (LocalTargetInfo target)
            {
                Widgets.MouseAttachedLabel("FCFireSupportSelectPosition".Translate());
            });
    }

    /// <summary>
    /// Rolls a single offset within +/-<paramref name="variance"/> for use when sampling a
    /// battle force from a cached <see cref="EnemyPower"/> baseline. Uniform distribution;
    /// replaces the prior curve-weighted <c>RandomAttackModifier</c> whose weighting was a
    /// no-op once the input clamped to the curve's first point.
    /// </summary>
    public static double RollVarianceOffset(double variance)
    {
        if (variance <= 0) return 0;
        return Rand.Range((float)-variance, (float)variance);
    }

    /// <summary>
    /// Returns the bare tech-level baseline (level, efficiency, variances) from
    /// <see cref="EnemyPowerTechDef"/>.
    /// </summary>
    public static void GetTechLevelBaseline(TechLevel tl,
        out double level, out double efficiency,
        out double levelVariance, out double efficiencyVariance)
    {
        EnemyPowerTechDef d = FindFC.EnemyPower?.GetTechDef(tl);
        if (d is null)
        {
            /* Pre-world / test path: no WorldComponent yet. Read DefDatabase directly. */
            foreach (EnemyPowerTechDef candidate in DefDatabase<EnemyPowerTechDef>.AllDefsListForReading)
            {
                if (candidate.techLevel == tl) { d = candidate; break; }
            }
        }
        if (d is not null)
        {
            level = d.level;
            efficiency = d.efficiency;
            levelVariance = d.levelVariance;
            efficiencyVariance = d.efficiencyVariance;
            return;
        }
        level = 1; efficiency = 1; levelVariance = 2; efficiencyVariance = 0;
    }

    /// <summary>
    /// Convenience overload returning only level and efficiency from the tech-level baseline.
    /// </summary>
    public static void GetTechLevelBaseline(TechLevel tl, out double level, out double efficiency)
    {
        GetTechLevelBaseline(tl, out level, out efficiency, out _, out _);
    }

    public static BattleViewerSide ResolvePlayerSide(MilitaryOperation op)
    {
        if (op is null) return BattleViewerSide.Neither;
        if (op.IsOffensive) return BattleViewerSide.Attacker;
        if (op.IsDefensive) return BattleViewerSide.Defender;
        return BattleViewerSide.Neither;
    }

    /// <summary>Runs <see cref="SquadUpgradeUtil.UpgradeToTemplate"/>, first prompting for
    /// confirmation when the re-template would fire (destroy) a pawn the player has
    /// personalized via the per-pawn loadout editor. Shared by every Upgrade-All entry
    /// point (squad inspection, military tab, settlement squad menu) so the warning is
    /// consistent. Window-layer helper — the model never opens dialogs itself.</summary>
    public static void ConfirmAndUpgradeAll(MercenarySquadFC squad)
    {
        if (squad is null) return;
        if (SquadUpgradeUtil.UpgradeWouldFirePersonalized(squad))
        {
            MercenarySquadFC captured = squad;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "FCSquadUpgradeFirePersonalizedConfirm".Translate(),
                delegate { SquadUpgradeUtil.UpgradeToTemplate(captured); }));
            return;
        }
        SquadUpgradeUtil.UpgradeToTemplate(squad);
    }
}
