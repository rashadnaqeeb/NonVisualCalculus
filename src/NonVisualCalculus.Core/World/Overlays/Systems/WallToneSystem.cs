using System;
using System.Numerics;
using NonVisualCalculus.Core.Audio;
using static NonVisualCalculus.Core.Strings.Strings;

namespace NonVisualCalculus.Core.World.Overlays.Systems
{
    /// <summary>
    /// Directional wall tones: four looping voices (north, south, east, west) whose volume rises as a wall
    /// nears in that direction, giving the cursor a sense of the room's edges. Each frame it casts the
    /// navmesh in the four cardinals from the cursor and turns the distance-to-wall into a 0..1 volume on the
    /// <see cref="Spatial.ProximityVolume"/> curve (quadratic, biting close in), then drives the audio
    /// engine's wall-tone voices, which place each at its fixed compass pan. Ported from the WOTR exploration
    /// mod with the range in metres.
    ///
    /// It mutes (zeroing the volumes, keeping the voices) rather than going silent abruptly when it should
    /// not sound: the play gate is off (Off, or WhenMoving with the cursor at rest), or the player has lost
    /// control (a cutscene or conversation). It releases its voices on overlay exit.
    /// </summary>
    public sealed class WallToneSystem : OverlaySystem
    {
        // WOTR's 10 ft sense range, converted to Disco's metres (1 unit = 1 m). The curve bites at the same
        // physical distance, so the soundscape feels identical.
        private const float RangeMetres = 3.048f;

        // Cardinal unit directions on the XZ ground plane, in the voice order the engine expects
        // (0 = north = +Z, 1 = south, 2 = east = +X, 3 = west).
        private static readonly Vector3[] Cardinals =
        {
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
        };

        private readonly IWorldEnvironment _env;
        private readonly IAudioEngine _audio;
        private readonly float[] _volumes = new float[4];

        private Func<float> _volume = () => 1f;
        private IWallTones? _tones;

        public WallToneSystem(IWorldEnvironment env, IAudioEngine audio)
        {
            _env = env;
            _audio = audio;
        }

        public override string Name => WorldSystemWallTones;
        public override string Key => "walltones";

        /// <summary>Bind the live 0..1 volume source (the host wires it to the wall-tone volume setting).
        /// Until bound, the tones play at full.</summary>
        public void BindVolume(Func<float> provider)
        {
            if (provider != null) _volume = provider;
        }

        public override void OnExit(Overlay overlay)
        {
            _tones?.Dispose();
            _tones = null;
        }

        public override void Tick(float dt, Overlay overlay)
        {
            // Stand down when the play gate is closed, over a scripted scene (control lost), or while a menu
            // owns input over the world, but keep the voices alive and silent so they resume seamlessly.
            if (!ShouldPlay(overlay) || !overlay.HasControl || !overlay.InputActive)
            {
                Mute();
                return;
            }

            // Build the voices on first use (and after an overlay exit nulled them).
            _tones ??= _audio.CreateWallTones();

            Vector3 cursor = overlay.Cursor.Position;
            float volume = _volume();
            for (int i = 0; i < Cardinals.Length; i++)
            {
                float dist = _env.WallDistance(cursor, Cardinals[i], RangeMetres);
                _volumes[i] = Spatial.ProximityVolume(dist, RangeMetres) * volume;
            }
            _tones.Update(_volumes);
        }

        private void Mute()
        {
            if (_tones == null) return;
            Array.Clear(_volumes, 0, _volumes.Length);
            _tones.Update(_volumes);
        }
    }
}
