using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class TaxesFC : ILoadReferenceable, IExposable
    {
        //internal variables
        public int loadID;
        public List<Thing> itemTithes;
        public float silverAmount;
        public List<ResourcePool> resourcePools;

        //ref
        public WorldSettlementFC settlement;
        public BillFC bill;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref itemTithes, "itemTithes", LookMode.Deep);
            Scribe_Values.Look(ref silverAmount, "silverAmount");
            Scribe_Collections.Look(ref resourcePools, "resourcePools", LookMode.Deep);

            Scribe_Values.Look(ref loadID, "loadID", -1);
            Scribe_References.Look(ref settlement, "settlement");
            Scribe_References.Look(ref bill, "bill");
        }

        public string GetUniqueLoadID()
        {
            return "Taxes_" + loadID;
        }

        public TaxesFC()
        {

        }

        public TaxesFC(BillFC bill)
        {
            SetUniqueLoadID();
            this.bill = bill;
            settlement = bill.settlement;
            silverAmount = 0;
            itemTithes = new List<Thing>();
            resourcePools = new List<ResourcePool>();

        }

        public void SetUniqueLoadID()
        {
            loadID = FindFC.TaxLedger.NextTaxId();
        }
    }
}