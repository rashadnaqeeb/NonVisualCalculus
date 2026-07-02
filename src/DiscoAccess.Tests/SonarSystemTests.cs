using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Speech;
using DiscoAccess.Core.World;
using DiscoAccess.Core.World.Overlays;
using DiscoAccess.Core.World.Overlays.Systems;
using Xunit;

namespace DiscoAccess.Tests
{
    public class SonarSystemTests
    {
        // The sweep's tuning as the tests know it: two things ping SpreadSec/2 apart clamped into
        // [GapMin, GapMax] (0.2 s for 2..7 things), and a fresh sweep starts after the bound rest.
        private const float Gap = 0.2f;

        private sealed class FakeItem : IWorldItem
        {
            public string Name { get; set; } = "thing";
            public Vector3 Position { get; set; }
            public bool Visible { get; set; } = true;
            public bool Accessible { get; set; } = true;
            public string Cat { get; set; } = WorldTaxonomy.Interactable;
            // A destroyed Unity proxy throws on any member read rather than reporting stale state.
            public bool Destroyed;
            private T Live<T>(T value) => Destroyed ? throw new InvalidOperationException("destroyed") : value;
            public ScanBounds Bounds => Live(ScanBounds.Point(Position));
            public string Category => Cat;
            public bool IsAccessible => Live(Accessible);
            public bool IsVisible => Live(Visible);
            public bool RidesPlayer => false;
            public Vector3 InteractionPoint(Vector3 from) => Position;
            public bool ReachableFrom(Vector3 from) => true;
            public bool Interact() => false;
        }

        private sealed class FakeModel : IWorldModel
        {
            public readonly List<IWorldItem> List = new List<IWorldItem>();
            public int Enumerations; // how many times a consumer walked the registry
            public IReadOnlyCollection<IWorldItem> Items
            {
                get { Enumerations++; return List; }
            }
            public event Action<IWorldItem> Added { add { } remove { } }
            public event Action<IWorldItem> Removed { add { } remove { } }
        }

        private sealed class FakeEnv : IWorldEnvironment
        {
            public bool Control = true;
            public Vector3 PlayerPosition => Vector3.Zero;
            public bool HasControl => Control;
            public Vector3 TraceMove(Vector3 from, Vector3 intended) => intended;
            public float WallDistance(Vector3 from, Vector3 direction, float range) => range;
            public bool InView(Vector3 point) => true;
            public Vector3 ClampToView(Vector3 point) => point;
            public bool IsFogged(Vector3 point) => false;
        }

        private sealed class FakeBackend : ISpeechBackend
        {
            public bool IsAvailable => true;
            public void Speak(string text, bool interrupt) { }
            public void Stop() { }
        }

        private static (SonarSystem sonar, Overlay overlay, FakeModel model, FakeAudioEngine audio, FakeEnv env)
            Build(List<string>? warnings = null)
        {
            var model = new FakeModel();
            var env = new FakeEnv();
            var audio = new FakeAudioEngine();
            var sources = new SpatialSources(audio, _ => { });
            var overlay = new Overlay(env, new SpeechPipeline(new FakeBackend()), sources);
            var sonar = new SonarSystem(model, env, sources, w => warnings?.Add(w));
            sonar.BindMode(() => PlayMode.Continuous);
            overlay.With(sonar);
            return (sonar, overlay, model, audio, env);
        }

        private static FakeItem At(float x, float z, string cat = WorldTaxonomy.Interactable)
            => new FakeItem { Position = new Vector3(x, 0f, z), Cat = cat };

        // A still tick: no glide, just the frame advancing by dt.
        private static void Tick(Overlay overlay, float dt) => overlay.Tick(dt, 0f, 0f, 4f);

        [Fact]
        public void Sweep_PingsWestToEast_OnePerGap()
        {
            var (_, overlay, model, audio, _) = Build();
            model.List.Add(At(3f, 0f));                          // east
            model.List.Add(At(-3f, 0f, WorldTaxonomy.Npc));      // west

            Tick(overlay, 0.01f);                                // sweep starts: west pings first
            Assert.Equal(AudioCue.ThingNpc, Assert.Single(audio.Cues));
            Assert.True(audio.Played[0].placement.Pan < -0.5f);  // west pans left

            Tick(overlay, 0.01f);                                // inside the gap: nothing yet
            Assert.Single(audio.Cues);

            Tick(overlay, Gap);                                  // gap elapsed: the east thing pings
            Assert.Equal(new[] { AudioCue.ThingNpc, AudioCue.ThingInteractable }, audio.Cues.ToArray());
            Assert.True(audio.Played[1].placement.Pan > 0.5f);   // east pans right
        }

        [Fact]
        public void Rest_SeparatesSweeps()
        {
            var (sonar, overlay, model, audio, _) = Build();
            sonar.BindRest(() => 0.4f);
            model.List.Add(At(2f, 0f));

            Tick(overlay, 0.01f);              // the one thing pings; the sweep ends into the rest
            Assert.Single(audio.Cues);

            Tick(overlay, 0.3f);               // inside the rest: silent
            Assert.Single(audio.Cues);

            Tick(overlay, 0.2f);               // rest elapsed: a fresh sweep pings it again
            Assert.Equal(2, audio.Cues.Count());
        }

        [Fact]
        public void ZeroRest_EmptyWorld_IdlesAtTheGapFloor()
        {
            var (sonar, overlay, model, _, _) = Build();
            sonar.BindRest(() => 0f);

            Tick(overlay, 0.01f);              // first tick scans, finds nothing, idles
            Tick(overlay, 0.01f);              // still idling: no per-frame registry rescan
            Tick(overlay, 0.01f);
            Assert.Equal(1, model.Enumerations);

            Tick(overlay, 0.1f);               // the gap-floor idle elapsed: one more scan
            Assert.Equal(2, model.Enumerations);
        }

        [Fact]
        public void CategoryToggle_SilencesItsGroup_DoorFoldsIntoExit()
        {
            var (sonar, overlay, model, audio, _) = Build();
            sonar.BindCategories(cat => cat != WorldTaxonomy.Exit);
            model.List.Add(At(1f, 0f, WorldTaxonomy.Door)); // browses under exit, so the exit toggle rules it
            model.List.Add(At(2f, 0f, WorldTaxonomy.Container));

            Tick(overlay, 0.01f);
            Assert.Equal(AudioCue.ThingContainer, Assert.Single(audio.Cues));
        }

        [Fact]
        public void DoorAndExit_ShareTheDoorSound()
        {
            var (_, overlay, model, audio, _) = Build();
            model.List.Add(At(1f, 0f, WorldTaxonomy.Door));
            model.List.Add(At(2f, 0f, WorldTaxonomy.Exit));

            Tick(overlay, 0.01f);
            Tick(overlay, Gap);
            Assert.Equal(new[] { AudioCue.ThingDoor, AudioCue.ThingDoor }, audio.Cues.ToArray());
        }

        [Fact]
        public void BeyondTheRadius_DropsFromTheSweep()
        {
            var (_, overlay, model, audio, _) = Build();
            model.List.Add(At(13f, 0f)); // past the 12 m sweep radius

            Tick(overlay, 0.01f);
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void LateSlot_OutOfRangeSinceSnapshot_SkipsSilently()
        {
            var (_, overlay, model, audio, _) = Build();
            model.List.Add(At(-2f, 0f, WorldTaxonomy.Npc));
            model.List.Add(At(11.8f, 0f)); // inside the radius at snapshot time, barely

            Tick(overlay, 0.01f);                 // west pings; the east thing waits its slot
            Assert.Single(audio.Cues);

            overlay.Tick(0.25f, -1f, 0f, 4f);     // the cursor glides 1 m west: the east thing is now out
            Assert.Single(audio.Cues);            // its slot fired but the radius re-check skipped it
        }

        [Fact]
        public void Volume_FallsWithDistance()
        {
            var (_, overlay, model, audio, _) = Build();
            model.List.Add(At(-3f, 0f)); // one reference-distance west
            model.List.Add(At(9f, 0f, WorldTaxonomy.Npc)); // three reference-distances east

            Tick(overlay, 0.01f);
            Tick(overlay, Gap);
            // 0.7 cue level, halved at the 3 m reference distance, quartered at 9 m.
            Assert.Equal(0.7f * 0.5f, audio.Played[0].volume, 3);
            Assert.Equal(0.7f * 0.25f, audio.Played[1].volume, 3);
            Assert.True(audio.Played[0].volume > audio.Played[1].volume);
        }

        [Fact]
        public void GoneSinceSnapshot_SkipsSilently()
        {
            var (sonar, overlay, model, audio, _) = Build();
            sonar.BindRest(() => 0.4f);
            var east = At(2f, 0f);
            model.List.Add(east);
            model.List.Add(At(-2f, 0f, WorldTaxonomy.Npc));

            Tick(overlay, 0.01f);        // snapshot of both; west pings
            Assert.Single(audio.Cues);
            east.Visible = false;        // ...then the east thing despawns
            Tick(overlay, Gap);          // its slot passes silently; the sweep ends into the rest
            Assert.Single(audio.Cues);

            Tick(overlay, 0.45f);        // next sweep: only the west thing remains
            Assert.Equal(new[] { AudioCue.ThingNpc, AudioCue.ThingNpc }, audio.Cues.ToArray());
        }

        [Fact]
        public void DestroyedMidSweep_ThrowingProxy_IsSkippedAndLogged_NotThrown()
        {
            var warnings = new List<string>();
            var (sonar, overlay, model, audio, _) = Build(warnings);
            sonar.BindRest(() => 0.4f);
            var east = At(2f, 0f);
            model.List.Add(east);
            model.List.Add(At(-2f, 0f, WorldTaxonomy.Npc));

            Tick(overlay, 0.01f);        // west pings
            east.Destroyed = true;       // the east proxy's object is destroyed: every read now throws
            Tick(overlay, Gap);          // its slot passes without the exception escaping the tick
            Assert.Single(audio.Cues);
            Assert.Contains(warnings, w => w.Contains("mid-sweep"));

            Tick(overlay, 0.45f);        // the next snapshot drops it the same way
            Assert.Equal(2, audio.Cues.Count());
            Assert.Contains(warnings, w => w.Contains("died since the poll"));
        }

        [Fact]
        public void LosingControl_ResetsTheSweep()
        {
            var (_, overlay, model, audio, env) = Build();
            model.List.Add(At(-2f, 0f, WorldTaxonomy.Npc));
            model.List.Add(At(2f, 0f));

            Tick(overlay, 0.01f);                    // west pings, east still queued
            env.Control = false;
            Tick(overlay, Gap);                      // cutscene: stands down, sweep dropped
            Assert.Single(audio.Cues);

            env.Control = true;
            Tick(overlay, 0.01f);                    // control returns: a FRESH sweep from the west
            Assert.Equal(AudioCue.ThingNpc, audio.Cues.Last());
        }

        [Fact]
        public void OffMode_NeverPings()
        {
            var (sonar, overlay, model, audio, _) = Build();
            sonar.BindMode(() => PlayMode.Off);
            model.List.Add(At(2f, 0f));

            Tick(overlay, 0.01f);
            Tick(overlay, 1f);
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void WhenMoving_SweepsOnlyWhileGliding()
        {
            var (sonar, overlay, model, audio, _) = Build();
            sonar.BindMode(() => PlayMode.WhenMoving);
            model.List.Add(At(2f, 0f));

            Tick(overlay, 0.01f);                    // at rest: silent
            Assert.Empty(audio.Cues);

            overlay.Tick(0.05f, 1f, 0f, 4f);         // gliding: the sweep runs
            Assert.Single(audio.Cues);

            Tick(overlay, MotionTracker.LingerSec + 0.5f); // stopped past the linger: stands down
            int settled = audio.Cues.Count();
            Tick(overlay, 1f);
            Assert.Equal(settled, audio.Cues.Count());
        }
    }
}
