using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NonVisualCalculus.Core.Audio;
using NonVisualCalculus.Core.Speech;
using static NonVisualCalculus.Core.Strings.Strings;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// The review cursor: a categorized, distance-sorted browse of the actionable things in the area, the
    /// WOTR scanner model. Its selection is a second point of attention alongside the movement cursor - the
    /// look-without-moving counterpart (NVDA object-navigator style): cycling it announces a thing's name
    /// and its bearing and distance from the player and pings it in stereo, without moving the cursor or
    /// the character. Acting on the selection is the caller's job (the module binds a walk-interact verb
    /// to it), so this class stays engine-free and unit-testable.
    ///
    /// The list is rebuilt from the live registry on every keypress, never held across presses - the world
    /// set changes as rooms reveal and orbs stream - and the selection is continued by proxy identity,
    /// which the registry keeps stable. The set is what a sighted player could see and act on right now
    /// (<see cref="ScanScope"/>), judged from the PLAYER: membership always measures from where the
    /// character stands, because acting on a scanned thing is a walk that starts at the character. The
    /// readout - the spoken bearing and distance, the review ping's ear, and the nearest-first browse
    /// order - measures from the bindable measure reference (<see cref="BindMeasureFrom"/>), the same
    /// player anchor until bound; the module binds the cursor there when the cursor-relative setting is
    /// on (the sonar's own listening-ear rule), so "next" walks outward from wherever the readout measures.
    ///
    /// Two cycle shapes share one selection: the browse category (<see cref="WorldTaxonomy.Scan"/>
    /// plus a synthetic Everything at index 0; stepping categories skips empty ones, Everything always
    /// lands), and the quick-nav groups (<see cref="ScanGroup"/>), each a fixed filter cycled by its own
    /// key, independent of the category state.
    /// </summary>
    public sealed class Scanner
    {
        private readonly IWorldModel _model;
        private readonly Overlays.IWorldEnvironment _env;
        private readonly Func<Vector3> _scanFrom;
        private readonly SpeechPipeline _speech;
        private readonly SpatialSources _cues;

        // Category index: 0 = the synthetic Everything, 1.. = WorldTaxonomy.Scan. The selection is the
        // reviewed proxy itself, held by identity (the registry keeps one stable proxy per thing), so it
        // survives the per-press rebuild and re-sort; _entered is WOTR's first-press rule - the first scanner
        // key announces the current spot without stepping, so entering the scanner is never a blind step.
        private int _catIndex;
        private IWorldItem? _selected;
        private bool _entered;

        // The review ping's volume, read live from the sonar-volume setting (one knob for both senses'
        // pings); the WOTR level until bound.
        private Func<float> _volume = () => WorldCues.DefaultVolume;

        // The measure reference the readout, browse order, and review ping use; the scan anchor until bound.
        private Func<Vector3> _measureFrom;

        public Scanner(IWorldModel model, Overlays.IWorldEnvironment env, Func<Vector3> scanFrom,
                       SpeechPipeline speech, SpatialSources cues)
        {
            _model = model;
            _env = env;
            _scanFrom = scanFrom;
            _measureFrom = scanFrom;
            _speech = speech;
            _cues = cues;
        }

        /// <summary>The reviewed thing, for the act verb (walk-interact). Null until the scanner has landed
        /// on something. Read live by the caller at act time; a despawned selection is the act verb's
        /// attempt-and-report problem, never pre-judged here.</summary>
        public IWorldItem? Selected => _selected;

        /// <summary>Bind the live 0..1 ping volume (the sonar-volume setting, shared with the sweep).</summary>
        public void BindVolume(Func<float> provider)
        {
            if (provider != null) _volume = provider;
        }

        /// <summary>Bind the live measure reference the readout, browse order, and review ping use (the
        /// cursor-relative setting binds the cursor here). Membership stays judged from the scan anchor:
        /// the walk an offered thing supports starts at the character, wherever the readout measures.</summary>
        public void BindMeasureFrom(Func<Vector3> provider)
        {
            if (provider != null) _measureFrom = provider;
        }

        /// <summary>Step through the current browse category (+1 next, -1 previous), nearest-first from
        /// the measure reference. The first press lands on the nearest thing without stepping.</summary>
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
            Vector3 measure = _measureFrom();
            if (_entered) _catIndex = NextCategoryIndex(from, dir);
            _entered = true;
            _selected = null; // a fresh category enters at its nearest thing

            List<IWorldItem> list = Build(from, measure, BrowseCategories());
            string line = WorldScanCategoryCount(CategoryLabel(), list.Count);
            if (list.Count == 0)
            {
                _speech.Speak(line, interrupt: true);
                return;
            }
            Land(list[0], measure, line + "; ");
        }

        /// <summary>Drop the selection (the overlay disengaged, the area changed). The category is kept -
        /// a browse position is a preference, not state about the old area.</summary>
        public void Reset()
        {
            _selected = null;
            _entered = false;
        }

        // The shared step: rebuild the filtered list, continue from the held selection when it is in the
        // list (entering at the nearest - or, stepping backward into a fresh list, the farthest - when it
        // is not), and land. The first press after entering never steps.
        private void Step(int dir, IReadOnlyList<string>? cats, string label)
        {
            Vector3 from = _scanFrom();
            Vector3 measure = _measureFrom();
            List<IWorldItem> list = Build(from, measure, cats);
            if (list.Count == 0)
            {
                _entered = true;
                _selected = null;
                _speech.Speak(WorldScanCategoryCount(label, 0), interrupt: true);
                return;
            }

            int idx = _selected != null ? list.IndexOf(_selected) : -1;
            if (idx < 0)
                idx = dir >= 0 ? 0 : list.Count - 1;
            else if (_entered)
                idx = ((idx + dir) % list.Count + list.Count) % list.Count;
            _entered = true;

            Land(list[idx], measure);
        }

        // Land on a thing: select it, ping it in stereo at its nearest part, and announce its name and its
        // bearing and distance - measured from the measure reference to that same nearest part, so the
        // words agree with the ear and describe where the THING is (the balcony smoker reads "above"), not
        // where acting on it would stand the player; the stand spot is the cursor move's business
        // (IWorldItem.InteractionPoint).
        private void Land(IWorldItem item, Vector3 measure, string prefix = "")
        {
            _selected = item;
            Ping(item);
            string spatial = SpatialReadout.Describe(measure, item.Bounds.NearestPoint(measure));
            // The shared name-plus-state composition (ItemLabel), so the scanner and the cursor readout
            // can never disagree about a door standing open.
            string name = ItemLabel.For(item, string.IsNullOrEmpty(item.Name) ? WorldThingObject : item.Name);
            _speech.Speak(prefix + name + "; " + spatial, interrupt: true);
        }

        // The review ping: the thing's own category sound placed at its nearest part relative to the
        // measure reference, so the ear hears where the readout says it is. The one shared WorldCues.Ping
        // the sonar sweep also plays, so review and sweep speak one sound language with one falloff.
        private void Ping(IWorldItem item) => WorldCues.Ping(_cues, item, _measureFrom, _volume);

        // The current filter's live list: the accessible-and-visible things inside the visible frame
        // (what a sighted player could see and act on right now, judged from the player anchor), filtered
        // through the door-folds-into-exit mapping (null = everything), sorted nearest-first from the
        // measure reference by body position. Rebuilt on every press; never cached.
        private List<IWorldItem> Build(Vector3 from, Vector3 measure, IReadOnlyList<string>? cats)
        {
            var list = new List<IWorldItem>();
            foreach (IWorldItem it in _model.Items)
            {
                if (!Offered(it, from)) continue;
                if (cats != null && !cats.Contains(WorldTaxonomy.ScanCategory(it.Category))) continue;
                list.Add(it);
            }
            list.Sort((a, b) => Geo.Distance(a.Position, measure).CompareTo(Geo.Distance(b.Position, measure)));
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
