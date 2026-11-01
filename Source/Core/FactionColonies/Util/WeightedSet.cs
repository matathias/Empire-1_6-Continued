using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies.util
{
    /* Dictionary of TKey -> float weights with a cached total and an optional
     * onChanged hook. Replaces the triplicate xenotype/customXenotype/race
     * weight machinery that used to live inline in XenotypeFilter. */
    public class WeightedSet<TKey>
    {
        private Dictionary<TKey, float> weights = new Dictionary<TKey, float>();
        private bool dirtyTotal = true;
        private float cachedTotal;
        private readonly Action onChanged;

        public WeightedSet() { }
        public WeightedSet(Action onChanged) { this.onChanged = onChanged; }

        public Dictionary<TKey, float> Weights => weights;
        public int Count => weights.Count;
        public bool ContainsKey(TKey key) => weights.ContainsKey(key);

        public float TotalWeight
        {
            get
            {
                if (dirtyTotal)
                {
                    float sum = 0f;
                    foreach (float w in weights.Values) sum += w;
                    cachedTotal = sum;
                    dirtyTotal = false;
                }
                return cachedTotal;
            }
        }

        public void Set(TKey key, float weight)
        {
            if ((object)key is null) return;
            weights[key] = weight;
            Invalidate();
        }

        public bool Remove(TKey key)
        {
            if (!weights.ContainsKey(key)) return false;
            weights.Remove(key);
            Invalidate();
            return true;
        }

        public void Clear()
        {
            if (weights.Count == 0) return;
            weights.Clear();
            Invalidate();
        }

        public float Get(TKey key)
        {
            return weights.TryGetValue(key, out float w) ? w : 0f;
        }

        public float ChanceOf(TKey key) => ChanceOf(key, TotalWeight);

        public float ChanceOf(TKey key, float divisor)
        {
            if (divisor <= 0f) return 0f;
            return Get(key) / divisor;
        }

        /* Removes entries whose weight is 0. */
        public void Cull()
        {
            List<TKey> toRemove = null;
            foreach (KeyValuePair<TKey, float> kvp in weights)
            {
                if (kvp.Value == 0f)
                {
                    if (toRemove is null) toRemove = new List<TKey>();
                    toRemove.Add(kvp.Key);
                }
            }
            if (toRemove is object)
            {
                for (int i = 0; i < toRemove.Count; i++) Remove(toRemove[i]);
            }
        }

        public void MarkDirty()
        {
            dirtyTotal = true;
        }

        /* Serializes via Scribe_Collections, restoring a fresh dict if Scribe handed back null
         * and forcing a total recompute. */
        public void Expose(string label, LookMode keyMode)
        {
            Scribe_Collections.Look(ref weights, label, keyMode, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (weights is null) weights = new Dictionary<TKey, float>();
                MarkDirty();
            }
        }

        private void Invalidate()
        {
            dirtyTotal = true;
            onChanged?.Invoke();
        }
    }
}
