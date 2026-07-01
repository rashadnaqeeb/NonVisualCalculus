using System;
using System.Collections.Generic;
using System.Numerics;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Speech;
using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// The review cursor: a categorized, distance-sorted browse of the actionable things in the area, the
    /// WOTR scanner model. Its selection is a second point of attention alongside the movement cursor - the
    /// look-without-moving counterpart (NVDA object-navigator style): cycling it announces a thing's name and
    /// its bearing and distance from the scan reference and pings it in stereo, without moving the cursor or
    /// the character. Acting on the selection is the caller's job (the module binds a walk-interact verb and
    /// a plant-the-cursor verb to it), so this class stays engine-free and unit-testable.
    ///
    /// The list is rebuilt from the live registry on every keypress, never held across presses - the world
    /// set changes as rooms reveal and orbs stream - and the selection is continued by proxy identity, which
    /// the registry keeps stable. The set is what a sighted player with this build could see and act on:
    /// <see cref="IWorldItem.IsAccessible"/> and <see cref="IWorldItem.IsVisible"/> both required, the same
    /// gate the movement cursor's own sense uses. Sorted nearest-first from the scan reference (the movement
    /// cursor), so "next" walks outward, and re-sorting from a moved cursor is the "look around from here"
    /// behaviour. Categories are <see cref="WorldTaxonomy.Scan"/> plus a synthetic Everything at index 0;
    /// stepping categories skips empty ones (Everything always lands, even empty).
    /// </summary>
    public sealed class Scanner
    {
        // The review ping's spatialization, the WOTR review-cue values in metres: pan crosses over at
        // PanWidth, volume halves every RefDistance and never falls below the floor, scaled by CueVolume so
        // the ping sits at the cursor blip's level.
        private const float PanWidth = 3f;
        private const float RefDistance = 3f;
        private const float VolumeFloor = 0.08f;
        private const float CueVolume = 0.7f;

        private readonly IWorldModel _model;
        private readonly Func<Vector3> _scanFrom;
        private readonly SpeechPipeline _speech;
        private readonly IAudioEngine _audio;

        // Category index: 0 = the synthetic Everything, 1.. = WorldTaxonomy.Scan. The selection is the
        // reviewed proxy itself, held by identity (the registry keeps one stable proxy per thing), so it
        // survives the per-press rebuild and re-sort; _entered is WOTR's first-press rule - the first scanner
        // key announces the current spot without stepping, so entering the scanner is never a blind step.
        private int _catIndex;
        private IWorldItem? _selected;
        private bool _entered;

        public Scanner(IWorldModel model, Func<Vector3> scanFrom, SpeechPipeline speech, IAudioEngine audio)
        {
            _model = model;
            _scanFrom = scanFrom;
            _speech = speech;
            _audio = audio;
        }

        /// <summary>The reviewed thing, for the act verbs (walk-interact, plant-the-cursor). Null until the
        /// scanner has landed on something. Read live by the caller at act time; a despawned selection is the
        /// act verb's attempt-and-report problem, never pre-judged here.</summary>
        public IWorldItem? Selected => _selected;

        /// <summary>Step the selection through the current category (+1 next, -1 previous), nearest-first
        /// from the scan reference. The first press lands on the nearest thing without stepping.</summary>
        public void StepItem(int dir)
        {
            Vector3 from = _scanFrom();
            List<IWorldItem> list = Build(from);
            if (list.Count == 0)
            {
                _entered = true;
                _selected = null;
                _speech.Speak(WorldScanCategoryCount(CategoryLabel(), 0), interrupt: true);
                return;
            }

            int idx = _selected != null ? list.IndexOf(_selected) : -1;
            // Enter at the nearest (or, stepping backward into a fresh list, the farthest); a held selection
            // steps from where it sits, wrapping. The first press after entering never steps.
            if (idx < 0)
                idx = dir >= 0 ? 0 : list.Count - 1;
            else if (_entered)
                idx = ((idx + dir) % list.Count + list.Count) % list.Count;
            _entered = true;

            Land(list[idx], from);
        }

        /// <summary>Step the browse category (+1 next, -1 previous), skipping empty ones (the synthetic
        /// Everything at index 0 always lands), then land on the new category's nearest thing. The first
        /// press announces the current category without stepping.</summary>
        public void StepCategory(int dir)
        {
            Vector3 from = _scanFrom();
            if (_entered) _catIndex = NextCategoryIndex(from, dir);
            _entered = true;
            _selected = null; // a fresh category enters at its nearest thing

            List<IWorldItem> list = Build(from);
            string line = WorldScanCategoryCount(CategoryLabel(), list.Count);
            if (list.Count == 0)
            {
                _speech.Speak(line, interrupt: true);
                return;
            }
            Land(list[0], from, line + "; ");
        }

        /// <summary>Drop the selection (the overlay disengaged, the area changed). The category is kept -
        /// a browse position is a preference, not state about the old area.</summary>
        public void Reset()
        {
            _selected = null;
            _entered = false;
        }

        // Land on a thing: select it, ping it in stereo at its nearest part, and announce its name and its
        // bearing and distance - measured to the interaction point, the spot the player would navigate to in
        // order to act (computed for the landed thing only; the sort uses cheap body positions).
        private void Land(IWorldItem item, Vector3 from, string prefix = "")
        {
            _selected = item;
            Ping(item, from);
            string spatial = SpatialReadout.Describe(from, item.InteractionPoint(from));
            string name = string.IsNullOrEmpty(item.Name) ? WorldThingObject : item.Name;
            _speech.Speak(prefix + name + "; " + spatial, interrupt: true);
        }

        // The review ping: a one-shot placed at the thing's nearest part relative to the scan reference, so
        // the ear hears where the readout says it is. Same cue as the cursor's enter blip until per-category
        // sounds are authored (planned with the sonar).
        private void Ping(IWorldItem item, Vector3 from)
        {
            Vector3 np = item.Bounds.NearestPoint(from);
            float dx = np.X - from.X, dz = np.Z - from.Z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            float pan = Spatial.Pan(dx, dist, PanWidth);
            float volume = CueVolume * Spatial.DistanceVolume(dist, RefDistance, VolumeFloor);
            _audio.PlayCue(AudioCue.CursorEnter, volume, pan);
        }

        // The current category's live list: the accessible-and-visible things (what a sighted player could
        // see and act on), category-filtered through the door-folds-into-exit mapping, sorted nearest-first
        // from the scan reference by body position. Rebuilt on every press; never cached.
        private List<IWorldItem> Build(Vector3 from)
        {
            string? cat = _catIndex <= 0 ? null : WorldTaxonomy.Scan[_catIndex - 1];
            var list = new List<IWorldItem>();
            foreach (IWorldItem it in _model.Items)
            {
                if (!it.IsAccessible || !it.IsVisible) continue;
                if (cat != null && WorldTaxonomy.ScanCategory(it.Category) != cat) continue;
                list.Add(it);
            }
            list.Sort((a, b) => Geo.Distance(a.Position, from).CompareTo(Geo.Distance(b.Position, from)));
            return list;
        }

        // The next category index with things in it, walking dir-wise with wrap-around; Everything (index 0)
        // always qualifies, so the walk terminates. Counted against the same live filter the list uses.
        private int NextCategoryIndex(Vector3 from, int dir)
        {
            int n = WorldTaxonomy.Scan.Count + 1;
            int i = _catIndex;
            for (int step = 0; step < n; step++)
            {
                i = ((i + dir) % n + n) % n;
                if (i == 0 || CountIn(WorldTaxonomy.Scan[i - 1]) > 0) return i;
            }
            return _catIndex;
        }

        private int CountIn(string cat)
        {
            int count = 0;
            foreach (IWorldItem it in _model.Items)
                if (it.IsAccessible && it.IsVisible && WorldTaxonomy.ScanCategory(it.Category) == cat) count++;
            return count;
        }

        private string CategoryLabel()
        {
            if (_catIndex <= 0) return WorldScanEverything;
            switch (WorldTaxonomy.Scan[_catIndex - 1])
            {
                case WorldTaxonomy.Npc: return WorldScanNpcs;
                case WorldTaxonomy.Interactable: return WorldScanInteractables;
                case WorldTaxonomy.Container: return WorldScanContainers;
                case WorldTaxonomy.Orb: return WorldScanOrbs;
                default: return WorldScanExits;
            }
        }
    }
}
