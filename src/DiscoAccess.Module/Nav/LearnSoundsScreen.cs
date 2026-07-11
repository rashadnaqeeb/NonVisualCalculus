using System;
using System.Collections.Generic;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI.Nav;
using DiscoAccess.Core.World;
using DiscoAccess.Core.World.Overlays;
using DiscoAccess.Core.World.Overlays.Systems;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The learn-game-sounds menu, a <see cref="ModOverlay"/> opened from the pause menu: every audio
    /// cue the mod makes, one entry each, Enter playing it so the player can tie each sound to its
    /// meaning before meeting it in the world. Two sections read as one arrow-key flow: the cursor's
    /// own cues, then the per-category pings the sonar sweep and the scanner share (labeled with the
    /// scanner's category words, so the menu and the scanner call a category the same thing). Each cue
    /// plays centred and ahead at its in-world level - the sonar entries at the live sonar volume - and
    /// runs its natural length. The one exception is the wall tones, which are looping beds rather than
    /// one-shots: their single entry demos all four, each at its fixed compass position for half a
    /// second (see <see cref="WallTonePreview"/>).
    ///
    /// The edge-of-senses entry follows the unrestrict-cursor setting: the restricted cursor is refused
    /// at the edge with the impassable bump, the unrestricted one crosses out and back with the fog
    /// cues, so only the sounds the player can actually meet are listed.
    /// </summary>
    internal sealed class LearnSoundsScreen : ModOverlay
    {
        private WallTonePreview _preview;

        public override string Title => Strings.ScreenLearnSounds;

        public override Container BuildRoot(IModHost host, Action onClose)
        {
            _preview = new WallTonePreview(host.Audio, () => host.Settings.WallToneVolume.Fraction);

            var root = new OverlayRoot(onClose);
            var sections = new Container(ContainerShape.VerticalList);

            var cursor = new Container(ContainerShape.VerticalList, Strings.LearnSoundsCursorSection);
            cursor.Add(new SoundCell(() => Strings.SoundCursorEnter,
                () => Play(host, AudioCue.CursorEnter, ObjectCueSystem.CueVolume)));
            cursor.Add(new SoundCell(() => Strings.SoundCursorExit,
                () => Play(host, AudioCue.CursorExit, ObjectCueSystem.CueVolume)));
            if (host.Settings.UnrestrictCursor.Value)
            {
                cursor.Add(new SoundCell(() => Strings.SoundCursorFogEnter,
                    () => Play(host, AudioCue.CursorFogEnter, Overlay.FogCueVolume)));
                cursor.Add(new SoundCell(() => Strings.SoundCursorFogExit,
                    () => Play(host, AudioCue.CursorFogExit, Overlay.FogCueVolume)));
            }
            else
            {
                cursor.Add(new SoundCell(() => Strings.SoundCursorImpassable,
                    () => Play(host, AudioCue.CursorImpassable, Overlay.ImpassableVolume)));
            }
            cursor.Add(new WallTonesCell(_preview));
            sections.Add(cursor);

            var sonar = new Container(ContainerShape.VerticalList, Strings.LearnSoundsSonarSection);
            sonar.Add(new SoundCell(() => Strings.WorldScanNpcs, () => PlayThing(host, AudioCue.ThingNpc)));
            sonar.Add(new SoundCell(() => Strings.WorldScanInteractables,
                () => PlayThing(host, AudioCue.ThingInteractable)));
            sonar.Add(new SoundCell(() => Strings.WorldScanContainers,
                () => PlayThing(host, AudioCue.ThingContainer)));
            sonar.Add(new SoundCell(() => Strings.WorldScanOrbs, () => PlayThing(host, AudioCue.ThingOrb)));
            sonar.Add(new SoundCell(() => Strings.WorldScanExits, () => PlayThing(host, AudioCue.ThingDoor)));
            sonar.Add(new SoundCell(() => Strings.SoundOpenDoor, () => PlayThing(host, AudioCue.ThingDoorOpen)));
            sections.Add(sonar);

            root.Add(sections);
            return root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            // Unscaled: the pause menu stops the game clock, and the demo must run under it.
            _preview.Tick(UnityEngine.Time.unscaledDeltaTime);
            return false;
        }

        public override void OnClosed() => _preview?.Stop();

        // A cue played as if right at the listener, dead ahead: centred, no ear delay, no rear cut, so
        // the player hears the sound itself rather than a placement of it. It runs its natural length
        // and the mixer drops the voice when it drains, so the returned handle is not tracked.
        private static void Play(IModHost host, AudioCue cue, float volume)
            => host.Audio.PlayCue(cue, volume, Spatial.Cue(0f, 1f, WorldCues.PanWidth));

        // A thing ping at the live sonar volume, the level the sweep and the scanner play it right at
        // the thing (Spatial.DistanceVolume is 1 at distance 0).
        private static void PlayThing(IModHost host, AudioCue cue)
            => Play(host, cue, host.Settings.SonarVolume.Fraction);

        /// <summary>One sound: its authored name, Enter playing it. No role word - every entry here
        /// plays its sound, the screen's whole point.</summary>
        private sealed class SoundCell : UIElement
        {
            private readonly Func<string> _label;
            private readonly Action _play;

            public SoundCell(Func<string> label, Action play)
            {
                _label = label;
                _play = play;
            }

            public override string Label => _label();

            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Activate, _play);
            }
        }

        /// <summary>The wall-tones entry: its value speaks the demo's direction order, since east and
        /// west place themselves by pan but north and south differ only in timbre - without the order
        /// the two centred tones would be unattributable.</summary>
        private sealed class WallTonesCell : UIElement
        {
            private readonly WallTonePreview _preview;

            public WallTonesCell(WallTonePreview preview) => _preview = preview;

            public override string Label => Strings.WorldSystemWallTones;

            // The engine's fixed demo order as compass words: north, south, east, west.
            public override string Value => SpokenLine.Join(
                Strings.WorldCompass(0), Strings.WorldCompass(4),
                Strings.WorldCompass(2), Strings.WorldCompass(6));

            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Activate, _preview.Start);
            }
        }
    }
}
