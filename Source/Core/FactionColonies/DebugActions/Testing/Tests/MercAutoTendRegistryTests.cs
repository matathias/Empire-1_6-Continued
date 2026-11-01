using RimWorld;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /* Tests for MercAutoTendRegistry: PickDoctor (first non-null wins) and PickMedicine
       (seeds with tech-level default, then chains through every provider). Exception-safety
       is exercised on both paths. */
    public static class MercAutoTendRegistryTests
    {
        private class FixedDoctorProvider : IMercAutoTendProvider
        {
            private readonly Pawn _doctor;
            public FixedDoctorProvider(Pawn doctor) { _doctor = doctor; }
            public Pawn ProvideTendingDoctor(Mercenary patient, WorldSettlementFC settlement) => _doctor;
            public ThingDef OverrideTendingMedicine(Mercenary patient, WorldSettlementFC settlement, ThingDef currentChoice) => currentChoice;
        }

        private class FixedMedicineProvider : IMercAutoTendProvider
        {
            private readonly ThingDef _medicine;
            public FixedMedicineProvider(ThingDef medicine) { _medicine = medicine; }
            public Pawn ProvideTendingDoctor(Mercenary patient, WorldSettlementFC settlement) => null;
            public ThingDef OverrideTendingMedicine(Mercenary patient, WorldSettlementFC settlement, ThingDef currentChoice) => _medicine;
        }

        private class ThrowingProvider : IMercAutoTendProvider
        {
            public Pawn ProvideTendingDoctor(Mercenary patient, WorldSettlementFC settlement)
                => throw new System.InvalidOperationException("test");
            public ThingDef OverrideTendingMedicine(Mercenary patient, WorldSettlementFC settlement, ThingDef currentChoice)
                => throw new System.InvalidOperationException("test");
        }

        /// <summary>Records whether it was probed, returning null. A throwing sentinel cannot prove
        /// short-circuiting because RegistryDispatch swallows provider exceptions and keeps walking;
        /// a probe flag proves the provider was never reached.</summary>
        private class SpyDoctorProvider : IMercAutoTendProvider
        {
            public bool DoctorProbed;
            public Pawn ProvideTendingDoctor(Mercenary patient, WorldSettlementFC settlement)
            {
                DoctorProbed = true;
                return null;
            }
            public ThingDef OverrideTendingMedicine(Mercenary patient, WorldSettlementFC settlement, ThingDef currentChoice) => currentChoice;
        }

        private static Pawn AnyPawn()
        {
            // Find any alive pawn — colonist, world pawn, doesn't matter (we use it as a reference
            // token only). Skip if none.
            return Find.WorldPawns?.AllPawnsAlive?.FirstOrDefault();
        }

        // -*- PickDoctor -*-

        [EmpireTest("Registry")]
        public static void PickDoctor_NoProviders_ReturnsNull()
        {
            Pawn doctor = MercAutoTendRegistry.PickDoctor(null, null);
            TestAssert.IsNull(doctor);
        }

        [EmpireTest("Registry")]
        public static void PickDoctor_AllReturnNull_ReturnsNull()
        {
            var p1 = new FixedDoctorProvider(null);
            var p2 = new FixedDoctorProvider(null);
            MercAutoTendRegistry.Register(p1);
            MercAutoTendRegistry.Register(p2);
            try
            {
                TestAssert.IsNull(MercAutoTendRegistry.PickDoctor(null, null));
            }
            finally
            {
                MercAutoTendRegistry.Unregister(p1);
                MercAutoTendRegistry.Unregister(p2);
            }
        }

        [EmpireTest("Registry")]
        public static void PickDoctor_FirstNonNullWins()
        {
            Pawn token = AnyPawn();
            if (token is null) TestAssert.Skip("No world pawns available as reference token");

            // First provider returns null; second returns the token; we expect the token back.
            var nullProvider = new FixedDoctorProvider(null);
            var tokenProvider = new FixedDoctorProvider(token);
            MercAutoTendRegistry.Register(nullProvider);
            MercAutoTendRegistry.Register(tokenProvider);
            try
            {
                Pawn result = MercAutoTendRegistry.PickDoctor(null, null);
                TestAssert.IsTrue(ReferenceEquals(token, result),
                    "First non-null provider's return value should win");
            }
            finally
            {
                MercAutoTendRegistry.Unregister(nullProvider);
                MercAutoTendRegistry.Unregister(tokenProvider);
            }
        }

        [EmpireTest("Registry")]
        public static void PickDoctor_FirstNonNullShortCircuits()
        {
            Pawn token = AnyPawn();
            if (token is null) TestAssert.Skip("No world pawns available as reference token");

            // First provider returns the token; the spy sentinel is registered after it. If PickDoctor
            // short-circuits on the first non-null, the sentinel is never probed (DoctorProbed stays false).
            var first = new FixedDoctorProvider(token);
            var sentinel = new SpyDoctorProvider();
            MercAutoTendRegistry.Register(first);
            MercAutoTendRegistry.Register(sentinel);
            try
            {
                Pawn result = MercAutoTendRegistry.PickDoctor(null, null);
                TestAssert.IsTrue(ReferenceEquals(token, result), "First non-null provider's return value should win");
                TestAssert.IsFalse(sentinel.DoctorProbed,
                    "PickDoctor should short-circuit and never probe the provider after the first non-null");
            }
            finally
            {
                MercAutoTendRegistry.Unregister(first);
                MercAutoTendRegistry.Unregister(sentinel);
            }
        }

        [EmpireTest("Registry")]
        public static void PickDoctor_Exception_FallsThrough()
        {
            Pawn token = AnyPawn();
            if (token is null) TestAssert.Skip("No world pawns available as reference token");

            // Throwing provider first; legitimate provider second. The chain should survive the
            // exception and return the second provider's pawn.
            var bad = new ThrowingProvider();
            var good = new FixedDoctorProvider(token);
            MercAutoTendRegistry.Register(bad);
            MercAutoTendRegistry.Register(good);
            try
            {
                Pawn result = MercAutoTendRegistry.PickDoctor(null, null);
                TestAssert.IsTrue(ReferenceEquals(token, result),
                    "Throwing provider should not abort the chain");
            }
            finally
            {
                MercAutoTendRegistry.Unregister(bad);
                MercAutoTendRegistry.Unregister(good);
            }
        }

        // -*- PickMedicine -*-

        [EmpireTest("Registry")]
        public static void PickMedicine_NoProviders_ReturnsTechDefault()
        {
            // With no providers the result is the tech-level default for the player colony faction.
            // For default Industrial-tech factions that's MedicineIndustrial. We don't pin the
            // expected value (faction tech may vary in dev sessions); we only require that the
            // result is non-null and is one of the recognized medicine defs.
            ThingDef result = MercAutoTendRegistry.PickMedicine(null, null);
            if (result is null) TestAssert.Skip("No tech-default medicine resolvable (likely Animal tech-level faction)");

            bool recognized = result == ThingDefOf.MedicineHerbal
                           || result == ThingDefOf.MedicineIndustrial
                           || result == ThingDefOf.MedicineUltratech;
            TestAssert.IsTrue(recognized,
                $"Expected one of the tech-level defaults; got {result.defName}");
        }

        [EmpireTest("Registry")]
        public static void PickMedicine_OverrideTakesEffect()
        {
            // A provider that always returns MedicineHerbal should win regardless of tech default.
            var provider = new FixedMedicineProvider(ThingDefOf.MedicineHerbal);
            MercAutoTendRegistry.Register(provider);
            try
            {
                ThingDef result = MercAutoTendRegistry.PickMedicine(null, null);
                TestAssert.IsTrue(result == ThingDefOf.MedicineHerbal,
                    $"Expected MedicineHerbal override; got {result?.defName ?? "null"}");
            }
            finally { MercAutoTendRegistry.Unregister(provider); }
        }

        [EmpireTest("Registry")]
        public static void PickMedicine_ChainsLastWins()
        {
            // Two providers: first returns Herbal, second returns Ultratech. Second sees Herbal
            // as its currentChoice and overrides to Ultratech. Final = Ultratech.
            var first = new FixedMedicineProvider(ThingDefOf.MedicineHerbal);
            var second = new FixedMedicineProvider(ThingDefOf.MedicineUltratech);
            MercAutoTendRegistry.Register(first);
            MercAutoTendRegistry.Register(second);
            try
            {
                ThingDef result = MercAutoTendRegistry.PickMedicine(null, null);
                TestAssert.IsTrue(result == ThingDefOf.MedicineUltratech,
                    $"Expected last provider's override; got {result?.defName ?? "null"}");
            }
            finally
            {
                MercAutoTendRegistry.Unregister(first);
                MercAutoTendRegistry.Unregister(second);
            }
        }

        [EmpireTest("Registry")]
        public static void PickMedicine_Exception_KeepsRunningChoice()
        {
            // A throwing provider should not abort the chain — the running choice survives.
            var first = new FixedMedicineProvider(ThingDefOf.MedicineHerbal);
            var bad = new ThrowingProvider();
            MercAutoTendRegistry.Register(first);
            MercAutoTendRegistry.Register(bad);
            try
            {
                ThingDef result = null;
                TestAssert.DoesNotThrow(() => result = MercAutoTendRegistry.PickMedicine(null, null));
                TestAssert.IsTrue(result == ThingDefOf.MedicineHerbal,
                    $"Throwing provider should leave running choice intact; got {result?.defName ?? "null"}");
            }
            finally
            {
                MercAutoTendRegistry.Unregister(first);
                MercAutoTendRegistry.Unregister(bad);
            }
        }

        // -*- Lifecycle parity with other registries -*-

        [EmpireTest("Registry")]
        public static void MercAutoTend_DuplicateRegister_Ignored()
        {
            // Mirrors the duplicate-registration behavior of the other registries.
            var provider = new FixedMedicineProvider(ThingDefOf.MedicineHerbal);
            MercAutoTendRegistry.Register(provider);
            MercAutoTendRegistry.Register(provider);
            try
            {
                int count = 0;
                foreach (IMercAutoTendProvider p in MercAutoTendRegistry.Providers)
                    if (ReferenceEquals(p, provider)) count++;
                TestAssert.AreEqual(1, count, "Should only appear once");
            }
            // Unregister twice so a dedup regression can't leak a test double for the session.
            finally { MercAutoTendRegistry.Unregister(provider); MercAutoTendRegistry.Unregister(provider); }
        }

        [EmpireTest("Registry")]
        public static void MercAutoTend_Unregister_StopsContributing()
        {
            var provider = new FixedMedicineProvider(ThingDefOf.MedicineHerbal);
            MercAutoTendRegistry.Register(provider);
            MercAutoTendRegistry.Unregister(provider);

            ThingDef result = MercAutoTendRegistry.PickMedicine(null, null);
            // After unregister, result reverts to tech default (not Herbal, unless the player
            // colony faction's tech is Neolithic/Medieval; in that case the assertion still
            // passes because tech-default happens to match).
            bool overrideStillInEffect = result == ThingDefOf.MedicineHerbal
                && (FindFC.EmpireFaction?.def?.techLevel != TechLevel.Neolithic
                    && FindFC.EmpireFaction?.def?.techLevel != TechLevel.Medieval);
            TestAssert.IsFalse(overrideStillInEffect,
                "Unregistered provider should not continue to influence medicine choice");
        }
    }
}
