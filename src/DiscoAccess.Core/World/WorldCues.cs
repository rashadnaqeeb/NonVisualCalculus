using System;
using System.Numerics;
using DiscoAccess.Core.Audio;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// How a thing in the world sounds: the mapping from its <see cref="WorldTaxonomy"/> category to the
    /// <see cref="AudioCue"/> it plays, and the one <see cref="Ping"/> that places that cue - shared by the
    /// sonar sweep and the scanner's review ping so the two senses speak one sound language with one
    /// falloff. A door and a destination exit share the door sound: to the player both are a way through,
    /// the same rule that lists them under one browse category.
    /// </summary>
    public static class WorldCues
    {
        // The thing-ping's spatialization, the WOTR review-cue values in metres: pan crosses over at
        // PanWidth, volume halves every RefDistance and never falls below the floor, scaled by the
        // caller's live sonar-volume setting. DefaultVolume is the unbound level (WOTR's, matching the
        // cursor blip) and the sonar-volume setting's default.
        public const float PanWidth = 3f;
        public const float RefDistance = 3f;
        public const float VolumeFloor = 0.08f;
        public const float DefaultVolume = 0.7f;

        public static AudioCue CueFor(string category)
        {
            switch (category)
            {
                case WorldTaxonomy.Npc: return AudioCue.ThingNpc;
                case WorldTaxonomy.Container: return AudioCue.ThingContainer;
                case WorldTaxonomy.Orb: return AudioCue.ThingOrb;
                case WorldTaxonomy.Door: return AudioCue.ThingDoor;
                case WorldTaxonomy.Exit: return AudioCue.ThingDoor;
                default: return AudioCue.ThingInteractable;
            }
        }

        /// <summary>Fire the thing's cue as a tracked one-shot placed at the nearest part of its shape
        /// relative to the live <paramref name="listener"/> - pan + ear delay east/west, muffled when it
        /// sits behind (south of) the listener - re-placed while it sounds, so a glide during the ping
        /// keeps it truthful. <paramref name="volume"/> is the live sonar-volume setting, read at every
        /// re-place so a menu change lands mid-ping.</summary>
        public static void Ping(SpatialSources cues, IWorldItem item, Func<Vector3> listener,
                                Func<float> volume)
        {
            cues.Play(CueFor(item.Category),
                      listener,
                      from => item.Bounds.NearestPoint(from),
                      dist => volume() * Spatial.DistanceVolume(dist, RefDistance, VolumeFloor),
                      PanWidth);
        }
    }
}
