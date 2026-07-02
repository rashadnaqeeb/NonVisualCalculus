using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DiscoAccess.Core.Audio;
using DiscoAccess.Core.Speech;
using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// The scanner: a categorized, distance-sorted browse of the actionable things in the area that drives
    /// the movement cursor directly - every landing plants the cursor on the thing, announces its name and
    /// its bearing and distance from the player, and pings it in stereo. Acting on the landed thing is the
    /// ordinary interact verb on the cursor, so the scanner needs no act verbs of its own.
    ///
    /// The list is rebuilt from the live registry on every keypress, never held across presses - the world
    /// set changes as rooms reveal and orbs stream - and the browse position is continued by proxy identity,
    /// which the registry keeps stable. The set is what a sighted player could see and act on right now
    /// (<see cref="ScanScope"/>), judged from the PLAYER: membership, sort, and the spoken readout all
    /// measure from where the character stands, never the cursor, because acting on a scanned thing is a
    /// walk that starts at the character. Sorted nearest-first from the player, so "next" walks outward.
    ///
    /// Two cycle shapes share one browse position: the browse category (<see cref="WorldTaxonomy.Scan"/>
    /// plus a synthetic Everything at index 0; stepping categories skips empty ones, Everything always
    /// lands), and the quick-nav groups (<see cref="ScanGroup"/>), each a fixed filter cycled by its own
    /// key, independent of the category state.
    /// </summary>
    public sealed class Scanner
    {
        private readonly IWorldModel _model;
        private readonly Overlays.IWorldEnvironment _env;
        private readonly Func<Vector3> _scanFrom;
        private readonly Action<Vector3> _plantCursor;
        private readonly SpeechPipeline _speech;
        private readonly SpatialSources _cues;

        // Category index: 0 = the synthetic Everything, 1.. = WorldTaxonomy.Scan. The browse position is the
        // landed proxy itself, held by identity (the registry keeps one stable proxy per thing), so it
        // survives the per-press rebuild and re-sort; _entered is WOTR's first-press rule - the first scanner
        // key announces the current spot without stepping, so entering the scanner is never a blind step.
        private int _catIndex;
        private IWorldItem? _current;
        private bool _entered;

        // The landing ping's volume, read live from the sonar-volume setting (one knob for both senses'
        // pings); the WOTR level until bound.
        private Func<float> _volume = () => WorldCues.DefaultVolume;

        public Scanner(IWorldModel model, Overlays.IWorldEnvironment env, Func<Vector3> scanFrom,
                       Action<Vector3> plantCursor, SpeechPipeline speech, SpatialSources cues)
        {
            _model = model;
            _env = env;
            _scanFrom = scanFrom;
            _plantCursor = plantCursor;
            _speech = speech;
            _cues = cues;
        }

        /// <summary>Bind the live 0..1 ping volume (the sonar-volume setting, shared with the sweep).</summary>
        public void BindVolume(Func<float> provider)
        {
            if (provider != null) _volume = provider;
        }

        /// <summary>Step through the current browse category (+1 next, -1 previous), nearest-first from the
        /// player. The first press lands on the nearest thing without stepping.</summary>
        public void StepItem(int dir) => Step(dir, BrowseCategories(), CategoryLabel());

        /// <summary>Step through a quick-nav group (+1 next, -1 previous), the group's own fixed filter,
        /// leaving the browse category untouched. A browse position held outside the group enters the group
        /// at its nearest thing.</summary>
        public void StepGroup(int dir, ScanGroup group)
            => Step(dir, WorldTaxonomy.GroupCategories(group), GroupLabel(group));

        /// <summary>Step the browse category (+1 next, -1 previous), skipping empty ones (the synthetic
        /// Everything at index 0 always lands), then land on the new category's nearest thing. The first
        /// press announces the current category without stepping.</summary>
        public void StepCategory(int dir)
        {
            Vector3 from = _scanFrom();
            if (_entered) _catIndex = NextCategoryIndex(from, dir);
            _entered = true;
            _current = null; // a fresh category enters at its nearest thing

            List<IWorldItem> list = Build(from, BrowseCategories());
            string line = WorldScanCategoryCount(CategoryLabel(), list.Count);
            if (list.Count == 0)
            {
                _speech.Speak(line, interrupt: true);
                return;
            }
            Land(list[0], from, line + "; ");
        }

        /// <summary>Drop the browse position (the overlay disengaged, the area changed). The category is
        /// kept - a browse position is a preference, not state about the old area.</summary>
        public void Reset()
        {
            _current = null;
            _entered = false;
        }

        // The shared step: rebuild the filtered list, continue from the held browse position when it is in
        // the list (entering at the nearest - or, stepping backward into a fresh list, the farthest - when it
        // is not), and land. The first press after entering never steps.
        private void Step(int dir, IReadOnlyList<string>? cats, string label)
        {
            Vector3 from = _scanFrom();
            List<IWorldItem> list = Build(from, cats);
            if (list.Count == 0)
            {
                _entered = true;
                _current = null;
                _speech.Speak(WorldScanCategoryCount(label, 0), interrupt: true);
                return;
            }

            int idx = _current != null ? list.IndexOf(_current) : -1;
            if (idx < 0)
                idx = dir >= 0 ? 0 : list.Count - 1;
            else if (_entered)
                idx = ((idx + dir) % list.Count + list.Count) % list.Count;
            _entered = true;

            Land(list[idx], from);
        }

        // Land on a thing: plant the movement cursor on it (the landing IS being there - Enter then acts on
        // exactly what was announced), ping it in stereo at its nearest part, and announce its name and its
        // bearing and distance - measured to the interaction point, the spot the player would navigate to in
        // order to act (computed for the landed thing only; the sort uses cheap body positions).
        private void Land(IWorldItem item, Vector3 from, string prefix = "")
        {
            _current = item;
            _plantCursor(item.Position);
            Ping(item);
            string spatial = SpatialReadout.Describe(from, item.InteractionPoint(from));
            string name = string.IsNullOrEmpty(item.Name) ? WorldThingObject : item.Name;
            _speech.Speak(prefix + name + "; " + spatial, interrupt: true);
        }

        // The landing ping: the thing's own category sound placed at its nearest part relative to the scan
        // reference, so the ear hears where the readout says it is. The one shared WorldCues.Ping the
        // sonar sweep also plays, so scanner and sweep speak one sound language with one falloff.
        private void Ping(IWorldItem item) => WorldCues.Ping(_cues, item, _scanFrom, _volume);

        // The current filter's live list: the accessible-and-visible things inside the visible frame
        // (what a sighted player could see and act on right now), filtered through the door-folds-into-exit
        // mapping (null = everything), sorted nearest-first from the player by body position. Rebuilt on
        // every press; never cached.
        private List<IWorldItem> Build(Vector3 from, IReadOnlyList<string>? cats)
        {
            var list = new List<IWorldItem>();
            foreach (IWorldItem it in _model.Items)
            {
                if (!Offered(it, from)) continue;
                if (cats != null && !cats.Contains(WorldTaxonomy.ScanCategory(it.Category))) continue;
                list.Add(it);
            }
            list.Sort((a, b) => Geo.Distance(a.Position, from).CompareTo(Geo.Distance(b.Position, from)));
            return list;
        }

        // The current browse category as a filter: null for the synthetic Everything.
        private IReadOnlyList<string>? BrowseCategories()
            => _catIndex <= 0 ? null : new[] { WorldTaxonomy.Scan[_catIndex - 1] };

        // The one offering gate Build and CountIn share, so the category counts can never disagree with the
        // list - and the same gate the sonar sweeps (ScanScope), so what pings is always what can be browsed.
        private bool Offered(IWorldItem it, Vector3 from) => ScanScope.Offered(it, from, _env);

        // The next category index with things in it, walking dir-wise with wrap-around; Everything (index 0)
        // always qualifies, so the walk terminates. Counted against the same live filter the list uses.
        private int NextCategoryIndex(Vector3 from, int dir)
        {
            int n = WorldTaxonomy.Scan.Count + 1;
            int i = _catIndex;
            for (int step = 0; step < n; step++)
            {
                i = ((i + dir) % n + n) % n;
                if (i == 0 || CountIn(WorldTaxonomy.Scan[i - 1], from) > 0) return i;
            }
            return _catIndex;
        }

        private int CountIn(string cat, Vector3 from)
        {
            int count = 0;
            foreach (IWorldItem it in _model.Items)
                if (Offered(it, from) && WorldTaxonomy.ScanCategory(it.Category) == cat) count++;
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

        private static string GroupLabel(ScanGroup group)
        {
            switch (group)
            {
                case ScanGroup.People: return WorldScanPeopleGroup;
                case ScanGroup.Items: return WorldScanItemsGroup;
                default: return WorldScanExits;
            }
        }
    }
}
