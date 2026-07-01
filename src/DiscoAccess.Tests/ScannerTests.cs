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

        private sealed class FakeAudio : IAudioEngine
        {
            public readonly List<(AudioCue cue, float volume, float pan)> Cues =
                new List<(AudioCue, float, float)>();
            public bool Available => true;
            public void PlayOneShot(float frequency, float seconds, float volume, float pan) { }
            public void PlayCue(AudioCue cue, float volume, float pan) => Cues.Add((cue, volume, pan));
            public IWallTones CreateWallTones() => throw new NotSupportedException();
        }

        private sealed class FakeItem : IWorldItem
        {
            public string Name { get; set; } = "thing";
            public Vector3 Position { get; set; }
            public bool Visible { get; set; } = true;
            public bool Accessible { get; set; } = true;
            public string Cat { get; set; } = WorldTaxonomy.Interactable;
            public ScanBounds Bounds => ScanBounds.Point(Position);
            public string Category => Cat;
            public bool IsAccessible => Accessible;
            public bool IsVisible => Visible;
            public bool RidesPlayer => false;
            public Vector3 InteractionPoint(Vector3 from) => Position;
            public bool IsActionable(Vector3 from) => true;
            public bool Interact() => false;
        }

        private sealed class FakeModel : IWorldModel
        {
            public readonly List<IWorldItem> List = new List<IWorldItem>();
            public IReadOnlyCollection<IWorldItem> Items => List;
            public event Action<IWorldItem> Added { add { } remove { } }
            public event Action<IWorldItem> Removed { add { } remove { } }
        }

        private static (Scanner scanner, FakeModel model, FakeBackend speech, FakeAudio audio) Build()
        {
            var model = new FakeModel();
            var speech = new FakeBackend();
            var audio = new FakeAudio();
            var scanner = new Scanner(model, () => Vector3.Zero, new SpeechPipeline(speech), audio);
            return (scanner, model, speech, audio);
        }

        private static FakeItem At(float x, float z, string name = "thing",
                                   string cat = WorldTaxonomy.Interactable)
            => new FakeItem { Position = new Vector3(x, 0f, z), Name = name, Cat = cat };

        [Fact]
        public void FirstPress_LandsOnNearest_WithoutStepping()
        {
            var (scanner, model, speech, _) = Build();
            model.List.Add(At(5f, 0f, "far"));
            model.List.Add(At(1f, 0f, "near"));

            scanner.StepItem(1);
            Assert.StartsWith("near; ", speech.Spoken[^1]);
            Assert.Same(model.List[1], scanner.Selected);
        }

        [Fact]
        public void SecondPress_StepsOutward_AndWraps()
        {
            var (scanner, model, speech, _) = Build();
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
            var (scanner, model, speech, _) = Build();
            model.List.Add(At(1f, 0f, "near"));
            model.List.Add(At(5f, 0f, "far"));

            scanner.StepItem(-1);
            Assert.StartsWith("far; ", speech.Spoken[^1]);
        }

        [Fact]
        public void SelectionContinues_AcrossARebuild()
        {
            var (scanner, model, speech, _) = Build();
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
            var (scanner, model, speech, _) = Build();
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
            var (scanner, model, speech, _) = Build();
            model.List.Add(new FakeItem { Position = new Vector3(1f, 0f, 0f), Name = "litter", Accessible = false });
            model.List.Add(new FakeItem { Position = new Vector3(2f, 0f, 0f), Name = "hidden", Visible = false });
            model.List.Add(At(5f, 0f, "real"));

            scanner.StepItem(1);
            Assert.StartsWith("real; ", speech.Spoken[^1]);
            scanner.StepItem(1); // wraps on the one-item list, never reaching the gated things
            Assert.StartsWith("real; ", speech.Spoken[^1]);
        }

        [Fact]
        public void EmptyWorld_SpeaksNone()
        {
            var (scanner, model, speech, _) = Build();
            scanner.StepItem(1);
            Assert.Equal("everything, none", speech.Spoken[^1]);
            Assert.Null(scanner.Selected);
        }

        [Fact]
        public void FirstCategoryPress_AnnouncesCurrentWithoutStepping()
        {
            var (scanner, model, speech, _) = Build();
            model.List.Add(At(1f, 0f, "near"));

            scanner.StepCategory(1);
            Assert.StartsWith("everything, 1; near; ", speech.Spoken[^1]);
        }

        [Fact]
        public void CategoryStep_SkipsEmptyCategories()
        {
            var (scanner, model, speech, _) = Build();
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
            var (scanner, model, speech, _) = Build();
            model.List.Add(At(1f, 0f, "kitchen door", WorldTaxonomy.Door));
            model.List.Add(At(3f, 0f, "courtyard door", WorldTaxonomy.Exit));

            scanner.StepCategory(1); // Everything
            scanner.StepCategory(1); // exits: both the in-place door and the transition
            Assert.StartsWith("exits, 2; kitchen door; ", speech.Spoken[^1]);
        }

        [Fact]
        public void ItemStep_StaysInsideTheCategory()
        {
            var (scanner, model, speech, _) = Build();
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
        public void Landing_PingsInStereo()
        {
            var (scanner, model, _, audio) = Build();
            model.List.Add(At(3f, 0f, "east thing")); // due east of the reference

            scanner.StepItem(1);
            var (cue, volume, pan) = Assert.Single(audio.Cues);
            Assert.Equal(AudioCue.CursorEnter, cue);
            Assert.True(pan > 0.5f);   // east pans right
            Assert.True(volume > 0f);
        }

        [Fact]
        public void EmptyLanding_DoesNotPing()
        {
            var (scanner, model, _, audio) = Build();
            scanner.StepItem(1);
            Assert.Empty(audio.Cues);
        }

        [Fact]
        public void Reset_DropsSelection_KeepsCategory()
        {
            var (scanner, model, speech, _) = Build();
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
            var (scanner, model, speech, _) = Build();
            model.List.Add(At(0f, 4f, "crate")); // 4 m due north

            scanner.StepItem(1);
            Assert.Equal("crate; north, 4 meters", speech.Spoken[^1]);
        }
    }
}
