using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// This extension allows for "pool"-type resources to specify how their pools are created, and other conditionals.
    /// </summary>
    public abstract class ResourcePoolExtension : DefModExtension
    {
        /// <summary>
        /// Called to create the resource pool at tax time.
        /// </summary>
        /// <param name="production">Production value of the pool's associated resource.</param>
        /// <param name="settlement">Settlement to create the pool for.</param>
        /// <returns>The size of the newly-created pool.</returns>
        public virtual double CreatePool(double production, WorldSettlementFC settlement = null)
        {
            return 0;
        }
        /// <summary>
        /// Controls whether the resource's faction-level pool is reset to 0 at tax time.
        /// </summary>
        /// <returns>TRUE if the pool should reset every tax period. FALSE otherwise. Defaults to TRUE.</returns>
        public virtual bool ResetAtTaxTime()
        {
            return true;
        }
        /// <summary>
        /// Called right before adding a pool of this resource to the global (faction) pool.
        /// <para>This can be used to modify the pool value before it's added. If you don't want to modify the value, then be sure to return the same value that the function receives.</para>
        /// </summary>
        /// <param name="value">The value that we intend to add to the global pool.</param>
        /// <returns>A modified value to add to the global pool.</returns>
        public virtual double PreAddToGlobalPool(double value)
        {
            return value;
        }
        /// <summary>
        /// Called whenever a pool of this resource is added to the global (faction) pool.
        /// <para>This function is called after the resource is actually added to the pool.</para>
        /// </summary>
        /// <param name="value">The value that is added to the global pool.</param>
        public virtual void AddedToGlobalPool(double value)
        {
        }
        /// <summary>
        /// Generates float menu options for the Faction Menu.
        /// </summary>
        /// <param name="pool">The faction-level ResourcePool associated with this pool.</param>
        /// <returns></returns>
        public virtual IEnumerable<FloatMenuOption> GetFactionMenuFloatMenuOptions(ResourcePool pool)
        {
            return null;
        }
        /// <summary>
        /// Called daily at the FactionFC level. Handles any daily processing for the faction-level resource pool.
        /// </summary>
        /// <param name="pool">The ResourcePool to update.</param>
        public virtual void DailyUpdate(ResourcePool pool)
        {
        }
    }
}
