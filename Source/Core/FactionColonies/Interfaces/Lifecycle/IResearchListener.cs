using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Lifecycle hook for research completion. Register implementations via
    /// <see cref="LifecycleRegistry"/>. Settlement stat caches are invalidated faction-wide
    /// between participants so later ones see changes from earlier ones.
    /// </summary>
    public interface IResearchListener
    {
        void OnResearchCompleted(ResearchProjectDef project);
    }
}
