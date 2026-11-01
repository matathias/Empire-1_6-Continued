using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Quest node that raises each supplied pawn's non-disabled skills by a flat bonus. Used by the
    /// Hire-Laborers quests to make hired laborers more competent as the empire's average settlement
    /// level rises (the bonus is computed in <see cref="FactionColonies.util.LaborerHireUtil"/> and
    /// passed via the slate). A zero bonus is a no-op.
    /// </summary>
    public class QuestNode_BoostLaborerSkills : QuestNode
    {
        public SlateRef<IEnumerable<Pawn>> pawns;
        public SlateRef<int> skillBonus;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            int bonus = skillBonus.GetValue(slate);
            if (bonus <= 0) return;

            IEnumerable<Pawn> value = pawns.GetValue(slate);
            if (value is null) return;

            foreach (Pawn pawn in value)
            {
                if (pawn?.skills is null) continue;
                foreach (SkillRecord skill in pawn.skills.skills)
                {
                    if (skill.TotallyDisabled) continue;
                    skill.Level = Mathf.Clamp(skill.Level + bonus, 0, 20);
                }
            }
        }
    }
}
