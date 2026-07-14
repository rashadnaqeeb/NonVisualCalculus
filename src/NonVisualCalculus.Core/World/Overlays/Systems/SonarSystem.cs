using System;
using System.Collections.Generic;
using System.Numerics;
using NonVisualCalculus.Core.Audio;
using static NonVisualCalculus.Core.Strings.Strings;

namespace NonVisualCalculus.Core.World.Overlays.Systems
{
    /// <summary>
    /// The staggered sonar sweep, the WOTR SonarSystem model: rather than sounding every sonifiable thing
    /// at once - which phantom-centres two same-type sounds at left+right into one averaged source - it
    /// pings them one at a time, ordered west to east so the pan glides across the sweep, each placed by
    /// distance (volume) and bearing (pan) from the cursor, then rests and repeats. The per-ping gap
    /// shrinks as the crowd grows (<see cref="Spatial.SweepGap"/>): a few things feel spacious and many
    /// compress toward the audible floor, so the whole sweep lengthens with count but nothing is ever
    /// dropped (the scanner remains the tool for exact enumeration).
    ///
    /// The sweep set is the scanner's own offering gate (<see cref="ScanScope"/>, judged from the PLAYER
    /// like every membership question) filtered by the per-category toggles - so what pings is always
    /// exactly what can be browsed. The CURSOR is only the listening ear: pings place and fall off around
    /// it, and the <see cref="SweepRadius"/> cap is measured from it, so gliding the cursor moves the
    /// soundscape without ever changing what the world offers. The snapshot is an ordering for one sweep
    /// only, never state: each ping re-reads the thing live (a despawned thing goes silent, a moved one
    /// sounds where it is), and each is a tracked source re-placed while it sounds, following a gliding
    /// cursor. Stands down like the wall tones - play gate closed, control lost (a cutscene), or a menu
    /// floating over the world - resetting the sweep so control's return starts fresh.
    /// </summary>
    public sealed class SonarSystem : OverlaySystem
    {
        // WOTR's sweep tuning, converted to Disco's metres (1 unit = 1 m): pings reach 12 m (40 ft) from
        // the cursor (placement and falloff are the shared WorldCues.Ping), and the sweep spreads
        // SpreadSec of gap across its count, each gap clamped to GapMin..GapMax.
        private const float SweepRadius = 12f;
        private const float SpreadSec = 0.75f;
        private const float GapMinSec = 0.1f;
        private const float GapMaxSec = 0.2f;

        /// <summary>Rest between sweeps until the setting is bound (WOTR's default, in seconds).</summary>
        public const float DefaultRestSec = 0.4f;

        private readonly IWorldModel _model;
        private readonly IWorldEnvironment _env;
        private readonly SpatialSources _cues;
        private readonly Action<string> _warn;

        // The ordered snapshot for the current sweep: live proxies held only until the sweep completes
        // (an ordering, not cached state - every read at fire time is live).
        private readonly List<IWorldItem> _sweep = new List<IWorldItem>();
        private int _index;   // next thing in _sweep to ping
        private float _timer; // seconds until the next ping / until the end-of-sweep rest elapses

        // What to sonify (keyed by browse category), the rest between sweeps, and the ping volume, read
        // live from the mod settings so a menu change takes effect mid-sweep. Everything sounds, at the
        // WOTR level, until bound.
        private Func<string, bool> _categories = _ => true;
        private Func<float> _rest = () => DefaultRestSec;
        private Func<float> _volume = () => WorldCues.DefaultVolume;

        public SonarSystem(IWorldModel model, IWorldEnvironment env, SpatialSources cues, Action<string> warn)
        {
            _model = model;
            _env = env;
            _cues = cues;
            _warn = warn;
        }

        public override string Name => WorldSystemSonar;
        public override string Key => "sonar";

        /// <summary>Bind the live per-category toggles: whether the given <see cref="WorldTaxonomy.Scan"/>
        /// browse category key should sound in the sweep.</summary>
        public void BindCategories(Func<string, bool> provider)
        {
            if (provider != null) _categories = provider;
        }

        /// <summary>Bind the live rest between sweeps, in seconds.</summary>
        public void BindRest(Func<float> provider)
        {
            if (provider != null) _rest = provider;
        }

        /// <summary>Bind the live 0..1 ping volume (the sonar-volume setting, shared with the scanner's
        /// review ping).</summary>
        public void BindVolume(Func<float> provider)
        {
            if (provider != null) _volume = provider;
        }

        public override void OnExit(Overlay overlay) => Reset();

        public override void Tick(float dt, Overlay overlay)
        {
            // Stand down over a scripted scene, a menu floating over the world, or a closed play gate -
            // and reset, so the sweep starts fresh (never resumes mid-list) when play returns.
            if (!ShouldPlay(overlay) || !overlay.HasControl || !overlay.InputActive)
            {
                Reset();
                return;
            }

            _timer -= dt;
            if (_timer > 0f) return;

            // Whole snapshot fired (or none yet): start a fresh sweep from the cursor's current spot.
            if (_index >= _sweep.Count)
            {
                Snapshot(overlay.Cursor.Position);
                _index = 0;
                // Nothing to ping: recheck after the rest, floored at the ping gap - the rest setting
                // reaches 0 (back-to-back sweeps, which the gap floor paces once things sound), and an
                // unfloored 0 here would rescan the whole registry every frame while silent.
                if (_sweep.Count == 0) { _timer = Math.Max(_rest(), GapMinSec); return; }
            }

            Fire(_sweep[_index++], overlay);
            _timer = _index >= _sweep.Count
                ? _rest()
                : Spatial.SweepGap(_sweep.Count, SpreadSec, GapMinSec, GapMaxSec);
        }

        private void Reset()
        {
            _sweep.Clear();
            _index = 0;
            _timer = 0f;
        }

        // The sonifiable things around the cursor, ordered west to east by body position so the pan glides
        // across the sweep (two same-type things read as "left ... right", never a centred average). The
        // radius cap keeps a far-but-in-frame thing from flooring at minimum volume and sounding
        // deceptively close - past it, it simply drops from the sweep until the cursor nears. Membership
        // is the player-anchored offering gate; only the radius (and the ping placement) is the cursor's.
        private void Snapshot(Vector3 cursor)
        {
            Vector3 player = _env.PlayerPosition;
            _sweep.Clear();
            foreach (IWorldItem it in _model.Items)
            {
                try
                {
                    if (!_categories(WorldTaxonomy.ScanCategory(it.Category))) continue;
                    if (Geo.DistanceXZ(it.Bounds.NearestPoint(cursor), cursor) > SweepRadius) continue;
                    if (!ScanScope.Offered(it, player, _env)) continue;
                }
                catch (Exception e)
                {
                    // A destroyed proxy still in the registry (its object despawned since the model's last
                    // poll) throws on any member read rather than reporting stale state - drop it.
                    _warn("[sonar] thing dropped from the sweep, died since the poll: " + e.Message);
                    continue;
                }
                _sweep.Add(it);
            }
            _sweep.Sort((a, b) => a.Position.X.CompareTo(b.Position.X));
        }

        private void Fire(IWorldItem item, Overlay overlay)
        {
            try
            {
                // Skip a thing that went away since the snapshot, or that a gliding cursor has since left
                // beyond the sweep radius - a floor-volume ping from out of range reads deceptively close.
                if (!item.IsVisible || !item.IsAccessible) return;
                Vector3 cursor = overlay.Cursor.Position;
                if (Geo.DistanceXZ(item.Bounds.NearestPoint(cursor), cursor) > SweepRadius) return;
                WorldCues.Ping(_cues, item, () => overlay.Cursor.Position, _volume);
            }
            catch (Exception e)
            {
                // A destroyed proxy throws on any member read; its slot passes silently - the same skip
                // SpatialSources gives a source that dies mid-cue.
                _warn("[sonar] ping dropped, thing died mid-sweep: " + e.Message);
            }
        }

        /// <summary>Live sweep state for the dev server, e.g. "mode=WhenMoving sweep=4 index=2".</summary>
        public string DevState() => "mode=" + Mode + " sweep=" + _sweep.Count + " index=" + _index
                                    + " timer=" + _timer.ToString("0.00");
    }
}
