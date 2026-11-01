using FactionColonies.util;
using Verse;

namespace FactionColonies.GiddyUpCompat
{
    /// <summary>
    /// Registers the Giddy Up 2 bridge on game startup. This class only exists in Empire.GiddyUp.dll,
    /// which is only loaded when Giddy Up 2 is active (via LoadFolders.xml conditional loading), so the
    /// registration is unconditional here.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GiddyUpInit
    {
        static GiddyUpInit()
        {
            GiddyUpUtil.Bridge = new GiddyUpBridge();
            LogUtil.MessageForce("Giddy Up 2 compatibility module loaded.");
        }
    }
}
