namespace FactionColonies
{
    /// <summary>
    /// Defines an interface to let classes intercept and modify silver payments before they are processed.
    /// </summary>
    public interface ISilverPaymentModifier
    {
        /// <summary>
        /// Called to decide how much silver is charged. Reduce context.Amount by whatever this
        /// modifier covers, using context.Reason and context.Settlement to decide whether it applies.
        /// <para>Must be side-effect-free: do NOT consume any resource inline here. If covering part
        /// of the payment means draining a resource, enqueue that drain via context.Commit(...) so it
        /// runs only after affordability is confirmed and only when the payment actually commits. This
        /// is also invoked for pure affordability queries (PaymentUtil.CanAfford), where commit actions
        /// are never run.</para>
        /// </summary>
        void ModifyPayment(SilverPaymentContext context);
    }
}
