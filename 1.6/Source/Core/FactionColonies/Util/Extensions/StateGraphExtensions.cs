using System.Collections.Generic;
using Verse.AI.Group;

namespace FactionColonies.util
{
    static class StateGraphExtensions
    {
        /// <summary>
        /// Same as <paramref name="stateGraph"/>.AddTransition, but for multiple <paramref name="transitions"/>
        /// </summary>
        /// <param name="stateGraph"></param>
        /// <param name="transitions"></param>
        public static void AddTransitions(this StateGraph stateGraph, IEnumerable<Transition> transitions)
        {
            foreach (Transition transition in transitions) stateGraph.AddTransition(transition);
        }
    }
}
