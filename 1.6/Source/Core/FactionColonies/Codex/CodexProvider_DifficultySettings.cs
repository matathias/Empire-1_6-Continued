using FactionColonies.util;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider that shows the full difficulty parameter table
    /// with the current difficulty highlighted.
    /// </summary>
    public class CodexProvider_DifficultySettings : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            EmpireDifficultyLevel current = FCSettings.difficultyLevel;

            string result = "FCCodexDiffCurrent".Translate(current.ToString()) + "\n\n";
            result += "FCCodexDiffHeader".Translate() + "\n";

            result += FormatRow(EmpireDifficultyLevel.Peaceful,        FCSettings.DEFAULT_SILVER_PER_RESOURCE_PEACEFUL,        FCSettings.DEFAULT_TAX_INTERVAL_DAYS_PEACEFUL,        FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_PEACEFUL,        FCSettings.DEFAULT_WORKER_COST_PEACEFUL,        current);
            result += FormatRow(EmpireDifficultyLevel.CommunityBuilder, FCSettings.DEFAULT_SILVER_PER_RESOURCE_COMMUNITYBUILDER, FCSettings.DEFAULT_TAX_INTERVAL_DAYS_COMMUNITYBUILDER, FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_COMMUNITYBUILDER, FCSettings.DEFAULT_WORKER_COST_COMMUNITYBUILDER, current);
            result += FormatRow(EmpireDifficultyLevel.AdventureStory,   FCSettings.DEFAULT_SILVER_PER_RESOURCE_ADVENTURESTORY,   FCSettings.DEFAULT_TAX_INTERVAL_DAYS_ADVENTURESTORY,   FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_ADVENTURESTORY,   FCSettings.DEFAULT_WORKER_COST_ADVENTURESTORY,   current);
            result += FormatRow(EmpireDifficultyLevel.StriveToSurvive,  FCSettings.DEFAULT_SILVER_PER_RESOURCE_STRIVETOSURVIVE,  FCSettings.DEFAULT_TAX_INTERVAL_DAYS_STRIVETOSURVIVE,  FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_STRIVETOSURVIVE,  FCSettings.DEFAULT_WORKER_COST_STRIVETOSURVIVE,  current);
            result += FormatRow(EmpireDifficultyLevel.BloodAndDust,     FCSettings.DEFAULT_SILVER_PER_RESOURCE_BLOODANDDUST,     FCSettings.DEFAULT_TAX_INTERVAL_DAYS_BLOODANDDUST,     FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_BLOODANDDUST,     FCSettings.DEFAULT_WORKER_COST_BLOODANDDUST,     current);
            result += FormatRow(EmpireDifficultyLevel.LosingIsFun,      FCSettings.DEFAULT_SILVER_PER_RESOURCE_LOSINGISFUN,      FCSettings.DEFAULT_TAX_INTERVAL_DAYS_LOSINGISFUN,      FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_LOSINGISFUN,      FCSettings.DEFAULT_WORKER_COST_LOSINGISFUN,      current);

            result += "\n" + "FCCodexDiffUpkeepMult".Translate(FCSettings.buildingUpkeepDifficultyMult.ToString("F2"));

            if (current == EmpireDifficultyLevel.Custom)
            {
                result += "\n" + "FCCodexDiffCustomLabel".Translate() + "\n";
                result += "FCCodexDiffCustomRow".Translate("Silver/Res (per day)", FCSettings.silverPerResource) + "\n";
                result += "FCCodexDiffCustomRow".Translate("Tax Days", FCSettings.timeBetweenTaxes / GenDate.TicksPerDay) + "\n";
                result += "FCCodexDiffCustomRow".Translate("Tithe Mod", FCSettings.productionTitheMod) + "\n";
                result += "FCCodexDiffCustomRow".Translate("Worker Cost (per day)", FCSettings.workerCost) + "\n";
                result += "FCCodexDiffCustomRow".Translate("Upkeep Mult", FCSettings.buildingUpkeepDifficultyMult.ToString("F2"));
            }

            return result;
        }

        private static string FormatRow(EmpireDifficultyLevel level, int silver, int taxDays, int tithe, int workerCost, EmpireDifficultyLevel current)
        {
            string marker = (level == current) ? " <--" : "";
            return "  " + "FCCodexDiffRow".Translate(
                level.ToString(),
                silver,
                taxDays,
                tithe,
                workerCost) + marker + "\n";
        }
    }
}
