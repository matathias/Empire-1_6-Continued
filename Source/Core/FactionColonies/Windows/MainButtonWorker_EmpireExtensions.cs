using RimWorld;

namespace FactionColonies
{
    public class MainButtonWorker_EmpireExtensions : MainButtonWorker_ToggleTab
    {
        public override bool Visible
        {
            get
            {
                if (!base.Visible) return false;
                return MainTableRegistry.Tabs.Count > 0;
            }
        }
    }
}
