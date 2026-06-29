using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Journal;
using Sunshine.Journal.Controller;
using Sunshine.Views;
using TMPro;
using UnityEngine;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The journal. Its root is a Panel of parallel Tab-stops: the tasks/map tab list first (the game's own
    /// two tabs, each an <see cref="OptionTab"/> that marks itself selected and runs the game's switch, the
    /// same pattern the options screen uses), then the content of whichever tab is shown. On the tasks tab
    /// that is the active/done filter (the game's own two view toggles, again as <see cref="OptionTab"/>s),
    /// the task list, the focused task's detail read line by line, and the officer profile; on the map tab it
    /// is the found white checks and the quicktravel points. Entry lands on the tab list; Tab moves across
    /// the stops, arrows move within a list, Enter activates (switch tab/filter, fast-travel), Escape closes.
    ///
    /// The tab and filter lists and the detail-line cells are built once and persist, so switching a tab or
    /// filter rebuilds only the affected list contents in place while focus stays on the cell that triggered
    /// it. The task rows read their localized name and state live; the detail cells read the task the list
    /// cells point them at (<see cref="JournalDetail"/>) live, so moving the list focus changes the detail
    /// with no rebuild; the officer profile is read from the live stat console when the clipboard is
    /// discovered (<see cref="CopotypeVisualizer.IsProfileDiscovered"/>) and otherwise reads the game's own
    /// "incomplete" notice. The lists appear a frame or two after the view transition and change with the
    /// filter and tab, so <see cref="OnUpdate"/> rebuilds in place keyed on a signature of the tab, filter,
    /// and live counts; per-row navigation does not change it, so moving through a list never re-homes focus.
    /// </summary>
    public sealed class JournalScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.JOURNAL;
        public override string ScreenName => Strings.ScreenJournal;

        // The game caps a task's revealed subtasks at this many (the detail scroll has ten subtask slots), so
        // a fixed set of subtask cells, each gated on the focused task having one at its index, covers any
        // task without rebuilding the detail panel as focus moves.
        private const int MaxSubtasks = 10;

        private ScreenRoot _root;
        private JournalController _jc;
        private int _builtSig;

        // Persistent: the tab/filter lists and the detail-line cells survive a restructure, so focus on a
        // tab or filter stays valid across a tab/filter change, and the detail follows the focused task with
        // no rebuild. The content lists are refilled in place.
        private JournalDetail _holder;
        private Container _tabs;
        private Container _filter;
        private Container _detailList;
        private Container _taskList;
        private Container _profile;
        private Container _whiteChecks;
        private Container _fastTravel;
        private Container _mapStatus;

        public override Container BuildRoot(IModHost host)
        {
            _root = new ScreenRoot();
            _jc = Object.FindObjectOfType<JournalController>();
            _holder = new JournalDetail();
            _tabs = BuildTabs();
            _filter = BuildFilter();
            _detailList = BuildDetailList();
            _holder.AttachList(_detailList);
            _taskList = new Container(ContainerShape.VerticalList, Strings.JournalTasksLabel);
            _profile = new Container(ContainerShape.VerticalList, Strings.JournalOfficerProfileLabel);
            _whiteChecks = new Container(ContainerShape.VerticalList, Strings.JournalWhiteChecksLabel);
            _fastTravel = new Container(ContainerShape.VerticalList, Strings.JournalFastTravelLabel);
            _mapStatus = new Container(ContainerShape.VerticalList);
            _builtSig = -1;
            Restructure();
            return _root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            if (Signature() == _builtSig)
                return false;
            Restructure();
            return nav.EnsureFocusValid();
        }

        // The two game tabs as OptionTabs: each reads the game's tab button label, marks itself selected when
        // its tab is shown, and on activate turns its toggle on (the game switches and our content follows).
        private Container BuildTabs()
        {
            var tabs = new Container(ContainerShape.VerticalList);
            if (_jc == null)
                return tabs;
            if (_jc.tasksToggle != null)
                tabs.Add(new OptionTab(_jc.tasksToggle,
                    () => _jc.tasksTab != null && _jc.tasksTab.activeInHierarchy,
                    () => _jc.tasksToggle.isOn = true));
            if (_jc.checksToggle != null)
                tabs.Add(new OptionTab(_jc.checksToggle,
                    () => _jc.checksTab != null && _jc.checksTab.activeInHierarchy,
                    () => _jc.checksToggle.isOn = true));
            return tabs;
        }

        // The active/done filter as two OptionTabs over the game's own view toggles, the same selectable-view
        // pattern as the tabs.
        private Container BuildFilter()
        {
            var filter = new Container(ContainerShape.VerticalList);
            if (_jc == null)
                return filter;
            if (_jc.showActiveToggle != null)
                filter.Add(new OptionTab(_jc.showActiveToggle,
                    () => _jc.showActiveToggle.isOn, () => _jc.showActiveToggle.isOn = true));
            if (_jc.showDoneToggle != null)
                filter.Add(new OptionTab(_jc.showDoneToggle,
                    () => _jc.showDoneToggle.isOn, () => _jc.showDoneToggle.isOn = true));
            return filter;
        }

        // The detail panel as its own lines: the description, then the subtask slots, then the filed and
        // resolved times. Each cell reads the focused task live and gates its own focusability, so the set is
        // fixed and never rebuilt as the list focus moves.
        private Container BuildDetailList()
        {
            var detail = new Container(ContainerShape.VerticalList, Strings.JournalTaskInfoLabel);
            detail.Add(new JournalDescriptionCell(_holder));
            for (int i = 0; i < MaxSubtasks; i++)
                detail.Add(new JournalSubtaskCell(_holder, i));
            detail.Add(new JournalFiledCell(_holder));
            detail.Add(new JournalResolutionCell(_holder));
            return detail;
        }

        // Lay out the root for the active tab, refilling the content lists from live state. The tab list is
        // always first; the rest follow for the tasks tab or the map tab. Reusing the persistent cells (not
        // rebuilding them) keeps focus put when only list contents changed.
        private void Restructure()
        {
            _builtSig = Signature();
            _root.Clear();
            _root.Add(_tabs);
            if (_jc == null)
                return;

            if (OnMap)
            {
                // Fast travel first, then the white checks; the map-incomplete notice (if any) last. The
                // fast-travel and status lists are added only when they have something to show, so an empty
                // section never appears.
                FillFastTravel();
                FillWhiteChecks();
                FillMapStatus();
                if (_fastTravel.Children.Count > 0)
                    _root.Add(_fastTravel);
                _root.Add(_whiteChecks);
                if (_mapStatus.Children.Count > 0)
                    _root.Add(_mapStatus);
            }
            else
            {
                FillTaskList();
                FillProfile();
                _root.Add(_filter);
                _root.Add(_taskList);
                _root.Add(_detailList);
                _root.Add(_profile);
            }
        }

        private bool OnMap => _jc.checksTab != null && _jc.checksTab.activeInHierarchy;

        private void FillTaskList()
        {
            _taskList.Clear();
            foreach (JournalTaskUI ui in ActiveTaskUIs())
                _taskList.Add(new JournalTaskCell(ui, _holder));
        }

        // The active task rows in on-screen order (sibling order under the shared list content). The filter
        // toggles which rows are active, so this already reflects active-versus-done.
        private static List<JournalTaskUI> ActiveTaskUIs()
        {
            var rows = new List<JournalTaskUI>();
            foreach (JournalTaskUI ui in Object.FindObjectsOfType<JournalTaskUI>())
                if (ui.gameObject.activeInHierarchy && ui.task != null)
                    rows.Add(ui);
            rows.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            return rows;
        }

        // The officer profile: name and title then the copotype/ledger stat rows when the clipboard is
        // discovered, otherwise the game's own incomplete notice. Blank spacer rows in the stat console
        // carry empty text, so their cells drop out of navigation on their own.
        private void FillProfile()
        {
            _profile.Clear();
            if (CopotypeVisualizer.IsProfileDiscovered())
            {
                CopotypeVisualizer cv = Object.FindObjectOfType<CopotypeVisualizer>();
                if (cv != null)
                {
                    _profile.Add(new JournalTextCell(cv.officerProfileName, cv.officerProfileTitle));
                    foreach (Transform line in cv.GetComponentsInChildren<Transform>(false))
                    {
                        if (line.name != "Copotype Profile Line(Clone)")
                            continue;
                        TMP_Text label = Child(line, "Label");
                        TMP_Text value = Child(line, "Value");
                        if (label != null)
                            _profile.Add(new JournalTextCell(label, value));
                    }
                }
            }
            else if (_jc.profileMissing != null)
            {
                foreach (TMP_Text t in _jc.profileMissing.GetComponentsInChildren<TMP_Text>(true))
                    _profile.Add(new JournalTextCell(t));
            }
        }

        private void FillWhiteChecks()
        {
            _whiteChecks.Clear();
            if (_jc.shownWhiteChecksList == null)
                return;
            var checks = new List<JournalWhiteCheckUI>();
            foreach (JournalWhiteCheckUI ui in _jc.shownWhiteChecksList)
                if (ui != null)
                    checks.Add(ui);
            checks.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            foreach (JournalWhiteCheckUI ui in checks)
                _whiteChecks.Add(new WhiteCheckCell(ui));
        }

        // The fast-travel destinations, shown only when the game reports quicktravel available right now (the
        // in-game unlock plus being somewhere you can depart from); otherwise the section stays empty and is
        // not shown. Each discovered location is one entry, an undiscovered one is omitted. Church and the
        // fishing village are gated by their discovery flags; the central Martinaise point carries none, so
        // it is listed whenever fast travel is available.
        private void FillFastTravel()
        {
            _fastTravel.Clear();
            QuicktravelController qc = QuicktravelController.Singleton;
            if (qc == null || !qc.IsQuicktravelAvailable())
                return;
            AddDestination(qc.churchButton, qc.wasQuicktravelChurchDiscovered, Strings.JournalLocChurch);
            AddDestination(qc.fishingVillageButton, qc.wasQuicktravelFishingVillageDiscovered, Strings.JournalLocFishingVillage);
            AddDestination(qc.martinaiseButton, true, Strings.JournalLocWaterfront);
        }

        private void AddDestination(QuicktravelButton button, bool discovered, string name)
        {
            if (discovered && Active(button))
                _fastTravel.Add(new QuicktravelCell(button, name));
        }

        // The game's own "map incomplete" notice, shown only when the city map item is not yet acquired.
        private void FillMapStatus()
        {
            _mapStatus.Clear();
            if (_jc.mapMissing != null && _jc.mapMissing.activeInHierarchy)
                foreach (TMP_Text t in _jc.mapMissing.GetComponentsInChildren<TMP_Text>(true))
                    _mapStatus.Add(new JournalTextCell(t));
        }

        private static bool Active(QuicktravelButton b) => b != null && b.gameObject.activeInHierarchy;

        // How many discovered fast-travel destinations exist right now (for the rebuild signature, so the
        // section appears or disappears as availability and discovery change).
        private static int DiscoveredDestinationCount(QuicktravelController qc)
        {
            int n = 0;
            if (qc.wasQuicktravelChurchDiscovered && Active(qc.churchButton)) n++;
            if (qc.wasQuicktravelFishingVillageDiscovered && Active(qc.fishingVillageButton)) n++;
            if (Active(qc.martinaiseButton)) n++;
            return n;
        }

        private static TMP_Text Child(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        // The structural shape that drives an in-place rebuild: the active tab, and for it the filter mode
        // and live counts (tasks/profile, or checks/quicktravel/map-missing). Per-row navigation and the
        // detail following the focused task leave this unchanged, so neither rebuilds; flipping the filter or
        // tab, or a list appearing, does. Tasks and map use disjoint ranges so a tab switch always reads as a
        // change.
        private int Signature()
        {
            if (_jc == null)
                return -1;
            if (OnMap)
            {
                int wc = _jc.shownWhiteChecksList != null ? _jc.shownWhiteChecksList.Count : 0;
                QuicktravelController qc = QuicktravelController.Singleton;
                bool avail = qc != null && qc.IsQuicktravelAvailable();
                int ft = avail ? DiscoveredDestinationCount(qc) : 0;
                int missing = _jc.mapMissing != null && _jc.mapMissing.activeInHierarchy ? 1 : 0;
                return 2_000_000 + missing * 200_000 + (avail ? 1 : 0) * 100_000 + ft * 1_000 + wc;
            }
            int done = _jc.showDoneToggle != null && _jc.showDoneToggle.isOn ? 1 : 0;
            int profile = CopotypeVisualizer.IsProfileDiscovered() ? 1 : 0;
            int tasks = 0;
            foreach (JournalTaskUI ui in Object.FindObjectsOfType<JournalTaskUI>())
                if (ui.gameObject.activeInHierarchy && ui.task != null) tasks++;
            return 1_000_000 + done * 100_000 + profile * 10_000 + tasks;
        }
    }
}
