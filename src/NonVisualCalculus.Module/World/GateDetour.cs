using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Snv = System.Numerics.Vector3;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// The game's self-opening navmesh gates, and the spot to walk to first so one opens. DE seals some
    /// passages with a carving <c>NavMeshObstacle</c> that a <c>LuaBlockerSwitcher</c> keeps enabled while
    /// a Lua boolean is false, and a <c>ColliderBooleanSwitcher</c> trigger box around the passage sets
    /// that boolean true while the main character stands inside it (Martinaise's yard passage down to the
    /// coast and the apartment courtyard, behind <c>auto.fortress_floor</c>). So the passage exists only for
    /// a character already beside it: from anywhere else the game's own click prices the far side infinite,
    /// and so does every path query the mod makes, both asking the carved mesh. A sighted player crosses by
    /// clicking into the passage first, then on; <see cref="TryFindVia"/> finds that first spot - a point
    /// inside the trigger box the character can reach, as close as the box allows to a point the far side
    /// reaches - and the walk verb goes there first, re-issuing the real destination on arrival, the gate
    /// having opened itself exactly as it does for a click. Nothing here touches the mesh, the obstacles,
    /// or the Lua state, and a gate its own trigger does not open (the dark rooms that need a flashlight,
    /// whose trigger sets a different boolean) is never attempted: that stays the game's own can't-reach.
    /// Every query reads the live scene; nothing is cached.
    /// </summary>
    internal sealed class GateDetour
    {
        // Grid step (metres) at which a trigger box's floor is sampled for candidate spots.
        private const float SampleStep = 2f;
        // Inset from the box's sides: the spot walked to must be well inside, so the trigger has fired (it
        // fires on entry) before the character halts, and the halt is not on the box's edge.
        private const float EdgeInset = 1f;
        // Least and most snap radius when dropping a sample onto the mesh; a box's own half height widens
        // it up to the cap, since a trigger box can float over the floor it covers.
        private const float MinSnapRadius = 1.5f;
        private const float MaxSnapRadius = 3f;

        private readonly WorldEnvironment _env;

        public GateDetour(WorldEnvironment env) { _env = env; }

        /// <summary>The first leg of a two-leg walk from <paramref name="from"/> to <paramref name="to"/>:
        /// a spot inside a self-opening gate's trigger box that a complete path reaches from
        /// <paramref name="from"/>, in a box that also holds a spot a complete path reaches from
        /// <paramref name="to"/> - so once the character stands there and the gate has opened, the far
        /// side prices finite. Of all such spots (across every gate), the one nearest the far side's
        /// spots. False when no gate bridges the two - the meaningful answer only after a direct path has
        /// already failed.</summary>
        public bool TryFindVia(Snv from, Snv to, out Snv via, out string gate)
        {
            via = default;
            gate = null;
            float best = float.MaxValue;
            ColliderBooleanSwitcher[] triggers = UnityEngine.Object.FindObjectsOfType<ColliderBooleanSwitcher>();
            foreach (LuaBlockerSwitcher sw in UnityEngine.Object.FindObjectsOfType<LuaBlockerSwitcher>())
            {
                if (!ClosedAndOpensOnEntry(sw)) continue;
                foreach (ColliderBooleanSwitcher trigger in triggers)
                {
                    if (trigger.BooleanName != sw.booleanName) continue;
                    BoxCollider box = trigger.GetComponent<BoxCollider>();
                    if (box == null) continue;
                    var near = new List<Vector3>();
                    var far = new List<Vector3>();
                    foreach (Vector3 spot in Spots(box))
                    {
                        Snv s = WorldConvert.ToSnv(spot);
                        if (_env.PathComplete(from, s)) near.Add(spot);
                        else if (_env.PathComplete(to, s)) far.Add(spot);
                    }
                    if (near.Count == 0 || far.Count == 0) continue;
                    foreach (Vector3 n in near)
                        foreach (Vector3 f in far)
                        {
                            float d = Vector3.Distance(n, f);
                            if (d >= best) continue;
                            best = d;
                            via = WorldConvert.ToSnv(n);
                            gate = sw.booleanName;
                        }
                }
            }
            return gate != null;
        }

        // A gate worth walking up to: its obstacle is sealing the mesh right now (otherwise the direct path
        // would have priced), and its boolean going true opens it (the switcher inverts the boolean into the
        // obstacle's enabled state). A non-inverting switcher CLOSES its passage while the boolean holds, the
        // opposite pattern, and walking up to it opens nothing.
        private static bool ClosedAndOpensOnEntry(LuaBlockerSwitcher sw)
        {
            if (!sw.invertBoolean || !sw.isActiveAndEnabled) return false;
            foreach (NavMeshObstacle ob in sw.obstacles)
                if (ob != null && ob.enabled && ob.carving && ob.gameObject.activeInHierarchy) return true;
            return false;
        }

        // Candidate spots on the mesh inside the trigger box: a grid over the box's floor in its own local
        // frame (a trigger box is placed and scaled to fit its passage, so the sampling follows its shape),
        // each dropped onto the navmesh and kept only if it still lies inside the box, inset from the sides.
        private static IEnumerable<Vector3> Spots(BoxCollider box)
        {
            Transform t = box.transform;
            Vector3 scale = t.lossyScale;
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f) yield break;
            Vector3 half = box.size * 0.5f;
            // Half extents inset from the sides, in local units (the inset is in metres).
            float hx = half.x - EdgeInset / scale.x;
            float hz = half.z - EdgeInset / scale.z;
            if (hx <= 0f || hz <= 0f) yield break;
            float stepX = SampleStep / scale.x;
            float stepZ = SampleStep / scale.z;
            float snap = Mathf.Clamp(half.y * scale.y, MinSnapRadius, MaxSnapRadius);
            for (float lx = -hx; lx <= hx; lx += stepX)
                for (float lz = -hz; lz <= hz; lz += stepZ)
                {
                    Vector3 world = t.TransformPoint(box.center + new Vector3(lx, 0f, lz));
                    if (!NavMesh.SamplePosition(world, out NavMeshHit hit, snap, WorldEnvironment.AllAreas)) continue;
                    Vector3 local = t.InverseTransformPoint(hit.position) - box.center;
                    if (Mathf.Abs(local.x) > hx || Mathf.Abs(local.z) > hz || Mathf.Abs(local.y) > half.y) continue;
                    yield return hit.position;
                }
        }
    }
}
