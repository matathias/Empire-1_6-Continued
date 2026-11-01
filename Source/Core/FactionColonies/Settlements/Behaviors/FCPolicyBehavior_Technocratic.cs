using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class FCPolicyBehavior_Technocratic : FCPolicyBehavior
    {
        public override IEnumerable<(TaggedString label, Action onClick)> GetMainTabActionButtons(FactionFC faction)
        {
            yield return ("FCSendResearchItems".Translate(), () =>
            {
                if (Find.ColonistBar.GetColonistsInOrder().Count > 0)
                {
                    Pawn playerNegotiator = Find.ColonistBar.GetColonistsInOrder()[0];
                    FCTrader_Research trader = new FCTrader_Research();
                    Find.WindowStack.Add(new Dialog_Trade(playerNegotiator, trader));
                }
                else
                {
                    LogUtil.Error("Couldn't find any colonists to trade with");
                }
            }
            );
        }
    }
}
