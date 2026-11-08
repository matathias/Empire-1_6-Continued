namespace FactionColonies;

public class FCWindow_Pay_Silver_Loyalty : FCWindow_Pay_Silver
{
    public FCWindow_Pay_Silver_Loyalty(WorldSettlementFC settlement) : base(settlement)
    {
        this.forcePause = false;
        this.draggable = true;
        this.doCloseX = true;
        this.preventCameraMotion = false;
        this.silverCount = PaymentUtil.GetSilver();
        this.settlement = settlement;
        this.selectedSilver = 0;
        this.stringEffect = "FCSettlementGainsXLoyalty";
    }


    public override float ReturnValue(int silver)
    {
        return silver / 100f;
    }

    /// <summary>Loyalty caps at 100, so silver beyond the remaining gap would be wasted.</summary>
    public override int MaxUsefulSilver => (int)Math.Ceiling((100 - settlement.loyalty) * 100);

    public override void UseValue(float value)
    {
        settlement.loyalty += ReturnValue(selectedSilver);
    }



}

