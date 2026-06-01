using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YuumisProwl.Utilities
{
    /// <summary>
    /// Collects trigger positions that arrive within a single frame and, one frame later,
    /// fires onResolve once per spatial cluster — positions within mergeRadius merge into one
    /// burst, far-apart ones stay separate. Bounds same-frame pile-ups while letting
    /// cross-frame chain reactions propagate (each frame is its own batch).
    ///
    /// Shared by effects that can be triggered many times in one destruction event
    /// (ignite mini-blasts, cryo bursts): one burst per real location, never N stacked on the
    /// same instant. The one-frame defer also avoids mutating the chain re-entrantly while a
    /// match is still being processed.
    /// </summary>
    public class FrameCoalescer
    {
        private readonly MonoBehaviour owner;                    // runs the deferred coroutine
        private readonly System.Action<Vector3, int, int> onResolve; // (clusterCentroid, memberCount, ignitePower)
        private readonly float mergeRadius;                      // positions closer than this share a burst
        private readonly List<Vector3> pending = new List<Vector3>(8);
        private readonly List<int> pendingIgnite = new List<int>(8); 
        private int batchFrame = -1;

        public FrameCoalescer(MonoBehaviour owner, float mergeRadius,
                              System.Action<Vector3, int,int> onResolve)
        {
            this.owner = owner;
            this.mergeRadius = mergeRadius;
            this.onResolve = onResolve;
        }

        /// <summary>Queue a trigger position. The first call each frame schedules the resolve.</summary>
        public void Add(Vector3 pos, int igniteStacks = 0)
        {
            pending.Add(pos);
            pendingIgnite.Add(igniteStacks);
            if (Time.frameCount != batchFrame)
            {
                batchFrame = Time.frameCount;
                owner.StartCoroutine(ResolveNextFrame());
            }
        }

        /// <summary>Drop any queued positions — call on synergy teardown.</summary>
        public void Clear() { pending.Clear(); pendingIgnite.Clear(); }

        private IEnumerator ResolveNextFrame()
        {
            yield return null;
            if (pending.Count == 0) yield break;

            // Greedy spatial clustering — merge positions within mergeRadius, keep far ones apart.
            var sums = new List<Vector3>(4);   // running position sum per cluster
            var counts = new List<int>(4);     // membership count per cluster
            var ignitePowers = new List<int>(4); // ignite power SUMMED per cluster
            float r2 = mergeRadius * mergeRadius;

            for (int i = 0; i < pending.Count; i++)
            {
                Vector3 p = pending[i];
                int hit = -1;
                for (int c = 0; c < sums.Count; c++)
                {
                    Vector3 centroid = sums[c] / counts[c];
                    if ((centroid - p).sqrMagnitude <= r2) { hit = c; break; }
                }
                if (hit >= 0) { sums[hit] += p; counts[hit]++; ignitePowers[hit] += pendingIgnite[i]; }
                else { sums.Add(p); counts.Add(1); ignitePowers.Add(pendingIgnite[i]); }
            }

            pending.Clear();
            pendingIgnite.Clear();

            for (int c = 0; c < sums.Count; c++)
                onResolve?.Invoke(sums[c] / counts[c], counts[c], ignitePowers[c]);
        }
    }
}
