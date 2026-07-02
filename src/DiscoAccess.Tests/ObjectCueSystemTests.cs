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
            public bool InView(Vector3 point) => true;
            public Vector3 ClampToView(Vector3 point) => point;
            public bool IsFogged(Vector3 point) => false;
        }

        private sealed class FakeBackend : ISpeechBackend
        {
            public readonly List<string> Spoken = new List<string>();
            public bool IsAvailable => true;
            public void Speak(string text, bool interrupt) => Spoken.Add(text);
            public void Stop() { }
        }

        private sealed class FakeItem : IWorldItem
        {
            public string Name { get; set; } = "thing";
            public Vector3 Position { get; set; }
            public bool Visible { get; set; } = true;
            public bool Accessible { get; set; } = true;
            public string Cat { get; set; } = WorldTaxonomy.Interactable;
            // A footprint half-extent: 0 is a point (the default), >0 is a square Box the size of the thing,
            // so a test can put a wide object under the cursor from its edge.
            public float HalfExtent { get; set; }
            public ScanBounds Bounds => HalfExtent > 0f
                ? ScanBounds.Box(Position, HalfExtent, HalfExtent)
                : ScanBounds.Point(Position);
            public string Category => Cat;
            public bool IsAccessible => Accessible;
            public bool IsVisible => Visible;
            public bool Rides { get; set; }
            public bool RidesPlayer => Rides;
            public Vector3 InteractionPoint(Vector3 from) => Position;
            // Whether the game can path to it from here. Defaults true (an accessible thing is usually
            // reachable); only the off-level cursor gate consults it, so a test sets it false to model a thing
            // hanging out of reach (a balcony door over the plaza).
            public bool Reachable { get; set; } = true;
            public bool ReachableFrom(Vector3 from) => Reachable;
            public bool Interact() => false;
        }

        private sealed class FakeModel : IWorldModel
        {
            public readonly List<IWorldItem> List = new List<IWorldItem>();
            public IReadOnlyCollection<IWorldItem> Items => List;
            public event Action<IWorldItem> Added { add { } remove { } }
            public event Action<IWorldItem> Removed { add { } remove { } }
        }

        private static (Overlay overlay, FakeAudioEngine audio, FakeModel model, ObjectCueSystem sys, FakeEnv env)
            Build(FakeBackend? backend = null)
        {
            var env = new FakeEnv();
            var model = new FakeModel();
            var audio = new FakeAudioEngine();
            var overlay = new Overlay(env, new SpeechPipeline(backend ?? new FakeBackend()),
                                      new SpatialSources(audio, _ => { }));
            var sys = new ObjectCueSystem(model, new SpatialSources(audio, _ => { }));
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
            model.List.Add(new FakeItem { Position = new Vector3(2f, 0f, 0f), HalfExtent = 1f }); // covers x 1..3
            model.List.Add(new FakeItem { Position = new Vector3(4f, 0f, 0f), HalfExtent = 1f }); // covers x 3..5, abuts A at 3

            Glide(overlay, 0f); // baseline bare
            Glide(overlay, 2f); // enter A
            Glide(overlay, 4f); // straight onto B across their shared edge - no bare ground between
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
            a.Accessible = false;
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
            var ghost = new FakeItem { Position = new Vector3(5f, 0f, 0f), Accessible = false };
            model.List.Add(ghost);

            Glide(overlay, 4.6f);                            // baseline + glide over bare ground (ghost out of set)
            overlay.Cursor.Position = new Vector3(5f, 0f, 0f);
            ghost.Accessible = true;
            overlay.Tick(0.05f, 0f, 0f, 4f);                // ghost streams in under the moving cursor (0.4 m step)
            overlay.Cursor.Position = new Vector3(5.2f, 0f, 0f);
            ghost.Accessible = false;
            overlay.Tick(0.05f, 0f, 0f, 4f);                // and straight back out, never confirmed
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void Announce_UsesTheSameSelectionAsTheBlip()
        {
            // The name-on-stop and the blip share one Under() selection, so a point-thing 2 m off (well past
            // the hover margin) is not named - exactly as it would not have blipped and Enter would not act.
            var backend = new FakeBackend();
            var (overlay, _, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "crate", Position = new Vector3(2f, 0f, 0f) });

            overlay.Cursor.Position = Vector3.Zero; // 2 m off a point footprint: outside the margin
            overlay.AnnounceCurrent();
            Assert.Empty(backend.Spoken);
        }

        [Fact]
        public void WideFootprint_HoveredFromItsEdge()
        {
            // A thing with a real footprint (a 2 m half-extent box) is "on" the cursor anywhere over its
            // surface, even 2 m from its centre - what a point bound could never do, and the fix for a wide
            // crate reading as bare ground everywhere but dead-centre.
            var backend = new FakeBackend();
            var (overlay, audio, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "crate", Position = new Vector3(4f, 0f, 0f), HalfExtent = 2f });

            Glide(overlay, 0f);   // baseline over bare ground (box edge is at x=2, still 2 m away)
            Glide(overlay, 2f);   // glide onto the box's near edge
            Assert.Equal(new[] { AudioCue.CursorEnter }, audio.Cues);

            overlay.Cursor.Position = new Vector3(2f, 0f, 0f); // on the edge, 2 m from the centre
            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "crate" }, backend.Spoken);
        }

        [Fact]
        public void ThingInsideAWiderFootprint_WinsTheTie()
        {
            // A crate inside a wide footprint (an orb's widened disc, a rug under a table): the cursor over
            // the crate is inside BOTH footprints at distance 0. Before the body tie-break the pick fell to
            // registry order, so the wide thing - registered first here, the failing order - shadowed the
            // crate permanently. The nearer body wins the tie: the crate names on itself, and the wide thing
            // still names on its own bare ground.
            var backend = new FakeBackend();
            var (overlay, _, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "wide orb", Position = new Vector3(5f, 0f, 0f), HalfExtent = 3f, Cat = WorldTaxonomy.Orb });
            model.List.Add(new FakeItem { Name = "crate", Position = new Vector3(4f, 0f, 0f), HalfExtent = 0.5f });

            overlay.Cursor.Position = new Vector3(4f, 0f, 0f); // on the crate, inside the wide footprint
            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "crate" }, backend.Spoken);

            backend.Spoken.Clear();
            overlay.Cursor.Position = new Vector3(7f, 0f, 0f); // off the crate, still inside the wide footprint
            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "wide orb" }, backend.Spoken);
        }

        [Fact]
        public void ElevatedReachableThing_AtTheCursorsXZ_IsStillSelected()
        {
            // A reachable thing whose geometry sits well above the ground (a staircase, an exit whose trigger
            // origin floats above the steps) at the cursor's own XZ: the hit test is XZ-only, so the cursor is
            // on it despite the height. A 3D distance would have put it 3 m off and named bare ground instead.
            // The height gate keeps it because it stands on connected ground (ReachableFrom).
            var backend = new FakeBackend();
            var (overlay, _, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "stairs", Position = new Vector3(5f, 3f, 0f), Reachable = true });

            overlay.Cursor.Position = new Vector3(5f, 0f, 0f); // directly under it, 3 m below
            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "stairs" }, backend.Spoken);
        }

        [Fact]
        public void ElevatedUnreachableThing_AtTheCursorsXZ_IsNotSelected()
        {
            // The Whirling balcony door: its body hangs metres above the plaza the cursor is clamped to, on
            // an island reached only from inside the Whirling. XZ-only detection would otherwise name it as
            // under the cursor and Enter would say "can't reach". Off the cursor's level AND off reachable
            // ground, so the height gate drops it and the cursor names bare ground instead.
            var backend = new FakeBackend();
            var (overlay, _, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "balcony door", Position = new Vector3(5f, 3f, 0f), Reachable = false });

            overlay.Cursor.Position = new Vector3(5f, 0f, 0f); // directly under it, 3 m below
            overlay.AnnounceCurrent();
            Assert.Empty(backend.Spoken);
        }

        [Fact]
        public void ElevatedUnreachableThing_WithinReach_IsStillSelected()
        {
            // The gate only fires past the same-level slack: a thing just above the cursor (a lever above
            // waist height) that every path test rejects is still named, so the gate never hides same-level
            // things standing on a navmesh pocket (an NPC behind a bar counter).
            var backend = new FakeBackend();
            var (overlay, _, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "lever", Position = new Vector3(5f, 0.8f, 0f), Reachable = false });

            overlay.Cursor.Position = new Vector3(5f, 0f, 0f); // just below it, within the slack
            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "lever" }, backend.Spoken);
        }

        [Fact]
        public void ThingOnThePlayer_IsSkipped_UnlessItRides()
        {
            // The cursor's near-player skip drops the character's own entity (so it is never hover-named when
            // the cursor is centred), but a thought-cabinet orb that legitimately rides the character must
            // still be found sitting right on top of it. Both are at the player (origin); only the rider names.
            var backend = new FakeBackend();
            var (overlay, _, model, _, _) = Build(backend);
            model.List.Add(new FakeItem { Name = "character", Position = Vector3.Zero });
            model.List.Add(new FakeItem { Name = "paralyzer", Position = Vector3.Zero, Rides = true });

            overlay.Cursor.Position = Vector3.Zero; // centred on the character
            overlay.AnnounceCurrent();
            Assert.Equal(new[] { "paralyzer" }, backend.Spoken);
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
        public void InaccessibleScenery_NotHovered()
        {
            // The cursor senses only the actionable set, so scenery you cannot act on never blips - the whole
            // point of the refactor: the cursor and Enter see the same set.
            var (overlay, audio, model, _, _) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(3f, 0f, 0f), Accessible = false });

            Glide(overlay, 0f);
            Glide(overlay, 3f);
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void AccessibleOrb_IsHovered()
        {
            // Orbs join the sensed set: a world-anchored orb whose conditions are met blips and names like any
            // interactable, so the cursor can find it (the Enter verb walks the character up and triggers it).
            var (overlay, audio, model, _, _) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(3f, 0f, 0f), Cat = WorldTaxonomy.Orb });

            Glide(overlay, 0f);
            Glide(overlay, 3f);
            Assert.Equal(new[] { AudioCue.CursorEnter }, audio.Cues);
        }

        [Fact]
        public void InaccessibleOrb_NotHovered()
        {
            // A locked orb (conditions unmet, or a morsel it does not offer) reads inaccessible, so the gate
            // hides it exactly like inaccessible scenery - the cursor never names an orb the player cannot act
            // on yet.
            var (overlay, audio, model, _, _) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(3f, 0f, 0f), Cat = WorldTaxonomy.Orb, Accessible = false });

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
            var overlay = new Overlay(env, new SpeechPipeline(backend),
                                      new SpatialSources(new FakeAudioEngine(), _ => { }));
            var objects = new ObjectCueSystem(model, new SpatialSources(new FakeAudioEngine(), _ => { }));
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
