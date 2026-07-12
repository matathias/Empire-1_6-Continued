using System;
using System.Collections.Generic;

namespace FactionColonies
{
    public class SilverPaymentContext
    {
        /// <summary>The amount of silver to charge. Modifiers can change this.</summary>
        public int Amount;
        /// <summary>Why silver is being charged. Match against PaymentUtil.Reason_* constants.</summary>
        public string Reason;
        /// <summary>The settlement this payment is associated with, if any.</summary>
        public WorldSettlementFC Settlement;

        // Side effects a modifier wants to perform (e.g. draining an outpost) only if the payment
        // actually commits. Collected during ModifyPayment, but run by the payment path only after
        // affordability is confirmed, and never for a pure CanAfford query. Keeps modifiers from
        // consuming resources for a payment that turns out to be unaffordable.
        private List<Action> commitActions;

        public SilverPaymentContext(int amount, string reason, WorldSettlementFC settlement = null)
        {
            Amount = amount;
            Reason = reason;
            Settlement = settlement;
        }

        /// <summary>
        /// Enqueue a side effect to run only when the payment commits. Modifiers must not consume
        /// resources inline in ModifyPayment; enqueue the consumption here instead.
        /// </summary>
        public void Commit(Action action)
        {
            if (action is null) return;
            if (commitActions is null) commitActions = new List<Action>();
            commitActions.Add(action);
        }

        /// <summary>
        /// Run all enqueued commit actions. Each is isolated so one failing modifier cannot abort the rest.
        /// </summary>
        public void RunCommit()
        {
            if (commitActions is null) return;
            foreach (Action action in commitActions)
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    LogUtil.Error("Silver payment commit action threw: " + e);
                }
            }
        }
    }

    public static class SilverPaymentRegistry
    {
        private static readonly RegistryList<ISilverPaymentModifier> _list = new RegistryList<ISilverPaymentModifier>();

        internal static void Register(ISilverPaymentModifier modifier) => _list.Register(modifier);
        internal static void Unregister(ISilverPaymentModifier modifier) => _list.Unregister(modifier);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<ISilverPaymentModifier> Modifiers => _list.Items;

        public static SilverPaymentContext InvokeModifiers(SilverPaymentContext context)
        {
            RegistryDispatch.Each(_list.Items, m => m.ModifyPayment(context), nameof(ISilverPaymentModifier.ModifyPayment));
            return context;
        }
    }
}
