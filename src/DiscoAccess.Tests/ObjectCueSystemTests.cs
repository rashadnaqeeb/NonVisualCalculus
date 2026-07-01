using System;
using System.Collections.Generic;
using System.Numerics;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Speech;
using DiscoAccess.Core.World;
using DiscoAccess.Core.World.Overlays;
using DiscoAccess.Core.World.Overlays.Systems;
using Xunit;

namespace DiscoAccess.Tests
{
    // Shares the static SpeechPipeline tap, so keep it out of parallel with the other speech-using suites.
    [Collection("UsesSpeechPipeline")]
    public class ObjectCueSystemTests
    {
        private sealed class FakeEnv : IWorldEnvironment
        {
            public bool Control = true;
            public Vector3 PlayerPosition => Vector3.Zero;
            public bool HasControl => Control;
            public Vector3 TraceMove(Vector3 from, Vector3 intended) => intended;
            public float WallDistance(Vector3 from, Vector3 dir, float range) => range;
            public void FocusCamera(Vector3 point) { }
            public void ReleaseCamera() { }
        }

        private sealed class FakeBackend : ISpeechBackend
        {
            public readonly List<string> Spoken = new List<string>();
            public bool IsAvailable => true;
            public void Speak(string text, bool interrupt) => Spoken.Add(text);
            public void Stop() { }
        }

        private sealed class FakeAudio : IAudioEngine
        {
            public readonly List<AudioCue> Cues = new List<AudioCue>();
            public bool Available => true;
            public void PlayOneShot(float frequency, float seconds, float volume, float pan) { }
            public void PlayCue(AudioCue cue, float volume, float pan) => Cues.Add(cue);
            public IWallTones CreateWallTones() => throw new NotSupportedException();
        }

        private sealed class FakeItem : IWorldItem
        {
            public string Name { get; set; } = "thing";
            public Vector3 Position { get; set; }
            public bool Visible { get; set; } = true;
            public ScanBounds Bounds => ScanBounds.Point(Position);
            public string Category => WorldTaxonomy.Other;
            public bool IsAccessible => true;
            public bool IsVisible => Visible;
            public Vector3 InteractionPoint(Vector3 from) => Position;
            public bool IsActionable(Vector3 from) => false;
            public bool Interact() => false;
        }

        private sealed class FakeModel : IWorldModel
        {
            public readonly List<IWorldItem> List = new List<IWorldItem>();
            public IReadOnlyCollection<IWorldItem> Items => List;
            public event Action<IWorldItem> Added { add { } remove { } }
            public event Action<IWorldItem> Removed { add { } remove { } }
        }

        private static (Overlay overlay, FakeAudio audio, FakeModel model, ObjectCueSystem sys, FakeEnv env)
            Build(FakeBackend? backend = null)
        {
            var env = new FakeEnv();
            var model = new FakeModel();
            var audio = new FakeAudio();
            var overlay = new Overlay(env, new SpeechPipeline(backend ?? new FakeBackend()));
            var sys = new ObjectCueSystem(model, audio);
            sys.BindMode(() => PlayMode.Continuous);
            overlay.With(sys);
            return (overlay, audio, model, sys, env);
        }

        // Glide the cursor to a point in realistic <=0.5 m steps, ticking each, so a footprint crossing
        // registers as a move (the cue gates clicks on real glide travel, not on a teleport or jitter).
        private static void Glide(Overlay overlay, float x, float z = 0f)
        {
            Vector3 from = overlay.Cursor.Position;
            Vector3 to = new Vector3(x, 0f, z);
            int steps = System.Math.Max(1, (int)System.Math.Ceiling((to - from).Length() / 0.5f));
            for (int i = 1; i <= steps; i++)
            {
                overlay.Cursor.Position = Vector3.Lerp(from, to, (float)i / steps);
                overlay.Tick(0.05f, 0f, 0f, 4f);
            }
        }

        [Fact]
        public void FirstFrame_Baselines_NoCue()
        {
            var (overlay, audio, model, _, _) = Build();
            model.List.Add(new FakeItem { Position = Vector3.Zero }); // cursor starts here, already on it

            Glide(overlay, 0f); // one baseline tick: silent
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void GlidingOntoAThing_RisingClick()
        {
            var (overlay, audio, model, _, _) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(3f, 0f, 0f) });

            Glide(overlay, 0f);  // baseline over bare ground (3 m away)
            Glide(overlay, 3f);  // glide onto it
            Assert.Equal(new[] { AudioCue.CursorEnter }, audio.Cues);
        }

        [Fact]
        public void GlidingOffToBareGround_FallingClick()
        {
            var (overlay, audio, model, _, _) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(3f, 0f, 0f) });

            Glide(overlay, 0f); // baseline bare
            Glide(overlay, 3f); // enter
            Glide(overlay, 0f); // leave to bare ground
            Assert.Equal(new[] { AudioCue.CursorEnter, AudioCue.CursorExit }, audio.Cues);
        }

        [Fact]
        public void GlidingThingToThing_RisingClick_NoExit()
        {
            var (overlay, audio, model, _, _) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(2f, 0f, 0f) });
            model.List.Add(new FakeItem { Position = new Vector3(4f, 0f, 0f) }); // footprints overlap, no gap

            Glide(overlay, 0f); // baseline bare
            Glide(overlay, 2f); // enter A
            Glide(overlay, 4f); // straight onto B
            Assert.Equal(new[] { AudioCue.CursorEnter, AudioCue.CursorEnter }, audio.Cues);
        }

        [Fact]
        public void StillCursor_FlickeringSet_DoesNotClick()
        {
            // The bug: a stationary cursor with a thing flickering in and out around it must not click.
            var (overlay, audio, model, _, _) = Build();
            var a = new FakeItem { Position = Vector3.Zero };
            model.List.Add(a);

            Glide(overlay, 0f); // baseline on A
            // Flicker: A streams out, B streams in at the same spot - the nearest thing changes, but the
            // cursor has not moved, so no footprint was crossed.
            a.Visible = false;
            model.List.Add(new FakeItem { Position = Vector3.Zero });
            overlay.Cursor.Position = Vector3.Zero;
            overlay.Tick(0.05f, 0f, 0f, 4f);

            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void GlidingPastAOneFrameFlicker_DoesNotClick()
        {
            // A moving cursor plus a thing that streams in for a single frame and back out (a SenseOrb the
            // camera-follow pulls in near the cursor's path) must not blip: it was never crossed, only flashed.
            var (overlay, audio, model, _, _) = Build();
            var ghost = new FakeItem { Position = new Vector3(5f, 0f, 0f), Visible = false };
            model.List.Add(ghost);

            Glide(overlay, 3f);                              // baseline + glide over bare ground (ghost hidden)
            overlay.Cursor.Position = new Vector3(4f, 0f, 0f);
            ghost.Visible = true;
            overlay.Tick(0.05f, 0f, 0f, 4f);                // ghost streams in under the moving cursor
            overlay.Cursor.Position = new Vector3(4.2f, 0f, 0f);
            ghost.Visible = false;
            overlay.Tick(0.05f, 0f, 0f, 4f);                // and straight back out, never confirmed
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void Announce_NamesAThingWithinReach_BeyondTheBlipFootprint()
        {
            // The name-on-stop reaches farther (2.5 m) than the blip footprint (1.5 m), matching the Enter
            // snap radius, so a stop never leaves a thing Enter could act on unspoken.
            var backend = new FakeBackend();
            var (overlay, _, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "crate", Position = new Vector3(2f, 0f, 0f) });

            overlay.Cursor.Position = Vector3.Zero; // 2 m off: past the blip radius, inside the reach radius
            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "crate" }, backend.Spoken);
        }

        [Fact]
        public void Gated_NoControl_NoCue()
        {
            var (overlay, audio, model, _, env) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(3f, 0f, 0f) });
            env.Control = false;

            Glide(overlay, 0f);
            Glide(overlay, 3f); // would enter, but control is lost
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void InvisibleThing_NotHovered()
        {
            var (overlay, audio, model, _, _) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(3f, 0f, 0f), Visible = false });

            Glide(overlay, 0f);
            Glide(overlay, 3f);
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void Announce_OverAThing_SpeaksItsName()
        {
            var backend = new FakeBackend();
            var (overlay, _, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "crate", Position = new Vector3(5f, 0f, 0f) });

            overlay.Cursor.Position = new Vector3(5f, 0f, 0f);
            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "crate" }, backend.Spoken);
        }

        [Fact]
        public void Announce_OverBareGround_SaysNothingFromThisSystem()
        {
            var backend = new FakeBackend();
            var (overlay, _, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "crate", Position = new Vector3(5f, 0f, 0f) });

            overlay.Cursor.Position = Vector3.Zero; // nothing within reach
            overlay.AnnounceCurrent();
            Assert.Empty(backend.Spoken);
        }

        [Fact]
        public void Announce_NameLeadsTheSpatialReadout()
        {
            var backend = new FakeBackend();
            var env = new FakeEnv();
            var model = new FakeModel();
            var overlay = new Overlay(env, new SpeechPipeline(backend));
            var objects = new ObjectCueSystem(model, new FakeAudio());
            objects.BindMode(() => PlayMode.Continuous);
            var spatial = new SpatialSystem();
            spatial.BindMode(() => PlayMode.Continuous);
            overlay.With(objects).With(spatial); // object cue registered first: name leads

            model.List.Add(new FakeItem { Name = "crate", Position = new Vector3(5f, 0f, 0f) });
            overlay.Cursor.Position = new Vector3(5f, 0f, 0f); // due east, 5 m from the player at origin

            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "crate; east, 5 meters" }, backend.Spoken);
        }
    }
}
