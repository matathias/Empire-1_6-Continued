using Verse;

namespace FactionColonies.VPE
{
    /// <summary>
    /// Registers the VPE psycast provider when this compat assembly loads. The assembly itself is only
    /// loaded when Vanilla Psycasts Expanded is active (gated via LoadFolders), so registration is
    /// unconditional here. Providers are app-lifetime singletons; see <see cref="PsycastSystemRegistry"/>.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VPECompatInit
    {
        static VPECompatInit()
        {
            PsycastSystemRegistry.Register(new VPEPsycastProvider());
            LogUtil.MessageForce("VPE patched");
        }
    }
}
