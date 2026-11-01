namespace FactionColonies
{
    /// <summary>
    /// Implemented by Window subclasses that dock beside the Found-screen
    /// (<see cref="CreateColonyWindowFc"/>). The base mod lays all of them out in a horizontal
    /// cascade via <see cref="FoundingScreenHooks.ReflowCompanions"/> — implementers must NOT set
    /// their own windowRect.x/y. Lower CompanionOrder sits closer to the main window (rightmost).
    /// </summary>
    public interface IFoundingCompanionWindow
    {
        int CompanionOrder { get; }
    }
}
