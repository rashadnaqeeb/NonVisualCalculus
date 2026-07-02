using System;
using System.Collections.Generic;
using System.Numerics;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Speech;
using DiscoAccess.Core.World;
using Xunit;

namespace DiscoAccess.Tests
{
    // Shares the static SpeechPipeline tap, so keep it out of parallel with the other speech-using suites.
    [Collection("UsesSpeechPipeline")]
    public class ScannerTests
    {
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
            // A footprint half-extent: 0 is a point (the default), >0 a square Box, so a test can model a
            // wide thing whose edge pokes into the frame.
            public float HalfExtent { get; set; }
            public ScanBounds Bounds => HalfExtent > 0f
                ? ScanBounds.Box(Position, HalfExtent, HalfExtent)
                : ScanBounds.Point(Position);
            public string Category => Cat;
            public bool IsAccessible => Accessible;
            public bool IsVisible => Visible;
            public bool RidesPlayer => false;
            public Vector3 InteractionPoint(Vector3 from) => Position;
            public bool Actionable { get; set; } = true;
            public bool IsActionable(Vector3 from) => Actionable;
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

        // Everything in view and unfogged unless a test scripts otherwise.
        private sealed class FakeEnv : DiscoAccess.Core.World.Overlays.IWorldEnvironment
        {
            public Func<Vector3, bool> ViewFn = _ => true;
            public Func<Vector3, bool> FogFn = _ => false;
            public Vector3 PlayerPosition => Vector3.Zero;
            public bool HasControl => true;
            public Vector3 TraceMove(Vector3 from, Vector3 intended) => intended;
            public float WallDistance(Vector3 from, Vector3 direction, float range) => range;
            public bool InView(Vector3 point) => ViewFn(point);
            public Vector3 ClampToView(Vector3 point) => point;
            public bool IsFogged(Vector3 point) => FogFn(point);
            public Func<Vector3, Vector3, bool> WalkFn = (_, _) => true;
            public bool WalkExists(Vector3 from, Vector3 to) => WalkFn(from, to);
        }

        private static (Scanner scanner, FakeModel model, FakeBackend speech, FakeAudioEngine audio, FakeEnv env) Build()
        {
            var model = new FakeModel();
            var speech = new FakeBackend();
            var audio = new FakeAudioEngine();
            var env = new FakeEnv();
            var scanner = new Scanner(model, env, () => Vector3.Zero, new SpeechPipeline(speech),
                                      new SpatialSources(audio, _ => { }));
            return (scanner, model, speech, audio, env);
        }

        private static FakeItem At(float x, float z, string name = "thing",
                                   string cat = WorldTaxonomy.Interactable)
            => new FakeItem { Position = new Vector3(x, 0f, z), Name = name, Cat = cat };

        [Fact]
        public void SameLevelPerson_GatedByClickVerdict_ThingsAreNot()
        {
            var env = new FakeEnv();
            // A person on the player's own level is offered only while the click verdict says reachable
            // (Cuno beyond the yard fence is filtered until a path opens)...
            var person = At(2f, 0f, "cuno", WorldTaxonomy.Npc);
            person.Reachable = false;
            Assert.False(ScanScope.Offered(person, Vector3.Zero, env));
            person.Reachable = true;
            Assert.True(ScanScope.Offered(person, Vector3.Zero, env));
            // ...while a same-level thing is offered regardless: the walled-off woodpile still pings, and
            // only its walk-interact reports the wall.
            var thing = At(2f, 0f, "woodpile");
            thing.Reachable = false;
            Assert.True(ScanScope.Offered(thing, Vector3.Zero, env));
        }

        [Fact]
        public void FirstPress_LandsOnNearest_WithoutStepping()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(At(5f, 0f, "far"));
            model.List.Add(At(1f, 0f, "near"));

            scanner.StepItem(1);
            Assert.StartsWith("near; ", speech.Spoken[^1]);
            Assert.Same(model.List[1], scanner.Selected);
        }

        [Fact]
        public void SecondPress_StepsOutward_AndWraps()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(At(1f, 0f, "near"));
            model.List.Add(At(5f, 0f, "far"));

            scanner.StepItem(1);
            scanner.StepItem(1);
            Assert.StartsWith("far; ", speech.Spoken[^1]);
            scanner.StepItem(1); // wraps back to the nearest
            Assert.StartsWith("near; ", speech.Spoken[^1]);
        }

        [Fact]
        public void SteppingBackward_FromFresh_LandsOnFarthest()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(At(1f, 0f, "near"));
            model.List.Add(At(5f, 0f, "far"));

            scanner.StepItem(-1);
            Assert.StartsWith("far; ", speech.Spoken[^1]);
        }

        [Fact]
        public void SelectionContinues_AcrossARebuild()
        {
            var (scanner, model, speech, _, _) = Build();
            var near = At(2f, 0f, "near");
            var far = At(5f, 0f, "far");
            model.List.Add(near);
            model.List.Add(far);

            scanner.StepItem(1); // lands on near
            model.List.Insert(0, At(1f, 0f, "nearer")); // the world changed under the scanner
            scanner.StepItem(1); // steps from the held selection in the fresh sort: near -> far
            Assert.StartsWith("far; ", speech.Spoken[^1]);
        }

        [Fact]
        public void VanishedSelection_ReentersAtNearest()
        {
            var (scanner, model, speech, _, _) = Build();
            var near = At(1f, 0f, "near");
            model.List.Add(near);
            model.List.Add(At(5f, 0f, "far"));

            scanner.StepItem(1);
            model.List.Remove(near); // despawned between presses
            scanner.StepItem(1);
            Assert.StartsWith("far; ", speech.Spoken[^1]); // nearest of what remains, not a wild step
        }

        [Fact]
        public void InaccessibleAndInvisible_AreNeverOffered()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(1f, 0f, 0f), Name = "litter", Accessible = false });
            model.List.Add(new FakeItem { Position = new Vector3(2f, 0f, 0f), Name = "hidden", Visible = false });
            model.List.Add(At(5f, 0f, "real"));

            scanner.StepItem(1);
            Assert.StartsWith("real; ", speech.Spoken[^1]);
            scanner.StepItem(1); // wraps on the one-item list, never reaching the gated things
            Assert.StartsWith("real; ", speech.Spoken[^1]);
        }

        [Fact]
        public void OutOfView_IsNeverOffered_AndCountsAgree()
        {
            var (scanner, model, speech, _, env) = Build();
            env.ViewFn = p => p.X <= 10f; // the frame ends at 10 m east
            model.List.Add(At(2f, 0f, "near crate", WorldTaxonomy.Container));
            model.List.Add(At(80f, 0f, "distant crate", WorldTaxonomy.Container));

            scanner.StepCategory(1); // Everything: only the in-frame thing counts
            Assert.StartsWith("everything, 1; near crate; ", speech.Spoken[^1]);
            scanner.StepItem(1); // wraps on the one-item list, never reaching the far one
            Assert.StartsWith("near crate; ", speech.Spoken[^1]);
        }

        [Fact]
        public void WideThing_PokingIntoTheFrame_StillOffered()
        {
            var (scanner, model, speech, _, env) = Build();
            env.ViewFn = p => p.X <= 10f;
            var wide = new FakeItem
            {
                Position = new Vector3(12f, 0f, 0f), // centre out of frame...
                Name = "long fence",
            };
            model.List.Add(wide);
            // ...but its footprint reaches back inside (nearest point to the origin is X=8).
            wide.HalfExtent = 4f;

            scanner.StepItem(1);
            Assert.StartsWith("long fence; ", speech.Spoken[^1]);
        }

        [Fact]
        public void EnvFogDoesNotSecondGuessItemVisibility()
        {
            var (scanner, model, speech, _, env) = Build();
            // The bathroom-door shape: the environment reads the thing's spot as fogged (its body hangs
            // inside the unrevealed room), but the item reports visible - the boundary rule, judged at its
            // approach stand-point. Fog is IWorldItem.IsVisible's contract; the scanner takes no second
            // fog opinion, so the door is offered.
            env.FogFn = _ => true;
            model.List.Add(At(2f, 0f, "bathroom door"));

            scanner.StepItem(1);
            Assert.StartsWith("bathroom door; ", speech.Spoken[^1]);
        }

        [Fact]
        public void SameLevelCrossingWithSeveredWalk_IsNotOffered()
        {
            var (scanner, model, speech, _, env) = Build();
            // The corridor doors beyond the player's own shut door: same level, in frame, visible over the
            // walls - but the closed door carves the walkable mesh, so no complete walk reaches their
            // stand-points and a walk-interact would stall. Only crossings take the walk test: the container
            // on a mesh-carving table behind the same severance stays offered (the over-rejection trap).
            env.WalkFn = (_, to) => to.X < 3f;
            model.List.Add(At(2f, 0f, "own door", WorldTaxonomy.Door));
            model.List.Add(At(5f, 0f, "corridor door", WorldTaxonomy.Door));
            model.List.Add(At(6f, 0f, "corridor stairs", WorldTaxonomy.Exit));
            model.List.Add(At(7f, 0f, "corridor crate", WorldTaxonomy.Container));

            scanner.StepItem(1);
            Assert.StartsWith("own door; ", speech.Spoken[^1]);
            scanner.StepItem(1);
            Assert.StartsWith("corridor crate; ", speech.Spoken[^1]);
            scanner.StepItem(1); // wraps: the severed door and stairs never land
            Assert.StartsWith("own door; ", speech.Spoken[^1]);
        }

        [Fact]
        public void OffLevelUnreachableThing_IsNotOffered()
        {
            var (scanner, model, speech, _, _) = Build();
            // The balcony door seen from the plaza: hanging well above the scan reference's level, standing
            // on a disconnected island - reachable only by going elsewhere, so never offered.
            model.List.Add(new FakeItem { Position = new Vector3(1f, 5f, 0f), Name = "balcony door", Reachable = false });
            model.List.Add(At(2f, 0f, "crate"));

            scanner.StepItem(1);
            Assert.StartsWith("crate; ", speech.Spoken[^1]);
            scanner.StepItem(1); // wraps, never landing on the balcony door
            Assert.StartsWith("crate; ", speech.Spoken[^1]);
        }

        [Fact]
        public void JustOffLevelUnreachableThing_IsNotOffered()
        {
            var (scanner, model, speech, _, _) = Build();
            // The ground below a low balcony (Martinaise stacks levels as tight as 2 m): past the pivot
            // slack, off reachable ground, so hidden even though the height gap is small.
            model.List.Add(new FakeItem { Position = new Vector3(1f, -2f, 0f), Name = "tracks", Reachable = false });
            model.List.Add(At(2f, 0f, "crate"));

            scanner.StepItem(1);
            Assert.StartsWith("crate; ", speech.Spoken[^1]);
            scanner.StepItem(1); // wraps, never landing on the tracks below
            Assert.StartsWith("crate; ", speech.Spoken[^1]);
        }

        [Fact]
        public void OffLevelReachableThing_StaysOffered()
        {
            var (scanner, model, speech, _, _) = Build();
            // The crate up on the harbour gate (its platform connects via stairs) or the balcony smoker
            // (a conversation authored from the ground): off-level but ReachableFrom, so still findable.
            model.List.Add(new FakeItem { Position = new Vector3(1f, 5f, 0f), Name = "smoker", Reachable = true });

            scanner.StepItem(1);
            Assert.StartsWith("smoker; ", speech.Spoken[^1]);
        }

        [Fact]
        public void OnLevelThing_IsOfferedWithoutAPathTest()
        {
            var (scanner, model, speech, _, _) = Build();
            // The NPC behind the bar counter: on the scan reference's level but standing on a navmesh pocket
            // no path test accepts; within the pivot slack the gate never asks, so they stay offered.
            model.List.Add(new FakeItem { Position = new Vector3(1f, 0.8f, 0f), Name = "bartender", Reachable = false });

            scanner.StepItem(1);
            Assert.StartsWith("bartender; ", speech.Spoken[^1]);
        }

        [Fact]
        public void EmptyWorld_SpeaksNone()
        {
            var (scanner, model, speech, _, _) = Build();
            scanner.StepItem(1);
            Assert.Equal("everything, none", speech.Spoken[^1]);
            Assert.Null(scanner.Selected);
        }

        [Fact]
        public void FirstCategoryPress_AnnouncesCurrentWithoutStepping()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(At(1f, 0f, "near"));

            scanner.StepCategory(1);
            Assert.StartsWith("everything, 1; near; ", speech.Spoken[^1]);
        }

        [Fact]
        public void CategoryStep_SkipsEmptyCategories()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(At(1f, 0f, "crate", WorldTaxonomy.Container));
            model.List.Add(At(2f, 0f, "stairs", WorldTaxonomy.Exit));

            scanner.StepCategory(1); // first press: Everything, no step
            scanner.StepCategory(1); // npc and interactable are empty: lands on containers
            Assert.StartsWith("containers, 1; crate; ", speech.Spoken[^1]);
            scanner.StepCategory(1); // orbs empty: lands on exits
            Assert.StartsWith("exits, 1; stairs; ", speech.Spoken[^1]);
            scanner.StepCategory(1); // wraps to Everything
            Assert.StartsWith("everything, 2; ", speech.Spoken[^1]);
        }

        [Fact]
        public void DoorsListUnderExits()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(At(1f, 0f, "kitchen door", WorldTaxonomy.Door));
            model.List.Add(At(3f, 0f, "courtyard door", WorldTaxonomy.Exit));

            scanner.StepCategory(1); // Everything
            scanner.StepCategory(1); // exits: both the in-place door and the transition
            Assert.StartsWith("exits, 2; kitchen door; ", speech.Spoken[^1]);
        }

        [Fact]
        public void ItemStep_StaysInsideTheCategory()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(At(1f, 0f, "cuno", WorldTaxonomy.Npc));
            model.List.Add(At(2f, 0f, "crate", WorldTaxonomy.Container));
            model.List.Add(At(5f, 0f, "kim", WorldTaxonomy.Npc));

            scanner.StepCategory(1); // Everything
            scanner.StepCategory(1); // people
            Assert.StartsWith("people, 2; cuno; ", speech.Spoken[^1]);
            scanner.StepItem(1);
            Assert.StartsWith("kim; ", speech.Spoken[^1]); // the crate is never offered here
            scanner.StepItem(1);
            Assert.StartsWith("cuno; ", speech.Spoken[^1]);
        }

        [Fact]
        public void Landing_PingsInStereo_WithTheCategorySound()
        {
            var (scanner, model, _, audio, _) = Build();
            model.List.Add(At(3f, 0f, "east thing")); // due east of the reference

            scanner.StepItem(1);
            var (cue, volume, placement) = Assert.Single(audio.Played);
            Assert.Equal(AudioCue.ThingInteractable, cue);
            Assert.True(placement.Pan > 0.5f);        // east pans right
            Assert.True(placement.ItdSeconds > 0f);   // east leads the right ear
            Assert.Equal(0f, placement.RearShelfDb, 3); // not behind, so bright
            Assert.True(volume > 0f);
        }

        [Fact]
        public void Landing_PingsTheDoorSound_ForADoor()
        {
            var (scanner, model, _, audio, _) = Build();
            model.List.Add(At(2f, 0f, "kitchen door", WorldTaxonomy.Door));

            scanner.StepCategory(1); // Everything; lands on the door
            Assert.Equal(AudioCue.ThingDoor, Assert.Single(audio.Cues));
        }

        [Fact]
        public void EmptyLanding_DoesNotPing()
        {
            var (scanner, model, _, audio, _) = Build();
            scanner.StepItem(1);
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void Reset_DropsSelection_KeepsCategory()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(At(1f, 0f, "cuno", WorldTaxonomy.Npc));
            model.List.Add(At(2f, 0f, "crate", WorldTaxonomy.Container));

            scanner.StepCategory(1); // Everything
            scanner.StepCategory(1); // people
            scanner.Reset();
            Assert.Null(scanner.Selected);
            scanner.StepItem(1); // first press again: lands nearest in the kept category
            Assert.StartsWith("cuno; ", speech.Spoken[^1]);
        }

        [Fact]
        public void SpokenLine_CarriesBearingAndDistance()
        {
            var (scanner, model, speech, _, _) = Build();
            model.List.Add(At(0f, 4f, "crate")); // 4 m due north

            scanner.StepItem(1);
            Assert.Equal("crate; north, 4 meters", speech.Spoken[^1]);
        }
    }
}
