using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.UI;
using Grid = DiscoAccess.Core.UI.Nav.Grid;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The in-game character sheet. Its root is a Panel with two Tab-stops: the main block (the 2D grid of
    /// attributes and skills, with the experience/points readout and the Revert Level Up button stacked
    /// under it) and the focused skill's detail region. Tab moves between the two; arrows move within the
    /// grid, and Down from the grid's bottom row spills down through the status readout and the revert
    /// button (Up returns). The game's Overview/Info tabs are not surfaced - the detail region already reads
    /// both their contents, so the toggles would do nothing for a screen-reader user.
    ///
    /// The grid mirrors the screen - each attribute (read-only, see <see cref="AttributeCell"/>) as column
    /// zero, then its six skills across, four attribute rows top to bottom (Intellect, Psyche, Physique,
    /// Motorics). Up/Down move between attributes keeping the column, Left/Right along a row. Each skill
    /// (<see cref="LevelUpSkillCell"/>) reads its name, value, signature and "can raise" markers, and short
    /// description, drives the detail panel to itself on focus, and on Enter spends a skill point when one is
    /// available. The detail region (<see cref="SkillDetailCell"/>) then reads that skill's bonus breakdown
    /// and long description. The status readout (<see cref="CharStatusCell"/>) is experience and unspent
    /// skill points. Escape closes the sheet (<see cref="ScreenRoot"/>).
    ///
    /// The charsheet's panels are instantiated a frame or two after the view transition, so the first build
    /// can find none: <see cref="OnUpdate"/> repopulates in place once they appear (and while their counts
    /// settle), re-homing focus. Nothing is focusable until the grid is populated, so focus lands on the
    /// grid, not the detail region, on entry.
    /// </summary>
    public sealed class CharacterSheetScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.CHARACTERSHEET;
        public override string ScreenName => Strings.ScreenCharacterSheet;

        private ScreenRoot _root;
        private IModHost _host;
        // A signature of the panel set the content was built from (skill count plus attribute count): the
        // charsheet's panels appear a frame or two after the transition, so this changes (e.g. 0 -> 24
        // skills and 0 -> 4 attributes) and drives the in-place rebuild until they have settled.
        private int _builtSig;

        public override Container BuildRoot(IModHost host)
        {
            _host = host;
            _root = new ScreenRoot();
            _builtSig = -1;
            Populate();
            return _root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            if (PanelSignature() == _builtSig)
                return false;
            // The panels appeared (or their counts changed while settling): rebuild and re-home focus so the
            // landing is announced once.
            Populate();
            return nav.EnsureFocusValid();
        }

        // Rebuild the root once the panels are available: the main block (grid, then the status readout,
        // then the Revert button - one vertical flow so Down from the grid spills down through them) as one
        // Tab-stop, and the skill detail region as the other. Built whole (not incrementally) and only while
        // the panel counts settle; after
        // that the cells read live with no rebuild, so spending a point - which the cells reflect live and
        // which does not change the panel counts - does not churn the tree. Until the panels appear the root
        // stays empty, so nothing is focusable and entry focus lands on the grid once it fills.
        private void Populate()
        {
            _root.Clear();
            _builtSig = PanelSignature();

            var panels = ActivePanels();
            if (panels.Count == 0)
                return;
            panels.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            // The detail region, built first so the grid cells can point it at (or away from) themselves on
            // focus: a skill makes itself its subject, an attribute clears it (the panel cannot show one).
            var detail = new SkillDetailCell();

            // Main block: the grid, then the experience/points readout, then the Revert Level Up button
            // (active only when level-ups are pending), one vertical flow so Down from the grid spills down
            // through them.
            var main = new Container(ContainerShape.VerticalList);
            main.Add(BuildGrid(panels, detail));
            main.Add(new CharStatusCell());
            AddButton(main, Charsheet(panels[0]), "Revert Level Up Button");
            _root.Add(main);

            _root.Add(detail); // the focused skill's breakdown then long description
        }

        // Add a charsheet button (icon-only, captioned through its localized image term) by name, found from
        // the shared Charsheet root. Wrapped whether active or not - the leveling buttons are inactive until
        // a level-up is pending, and SelectableButton drops out of nav while a button is inactive, so it
        // appears the moment the button does.
        private void AddButton(Container into, Transform charsheet, string name)
        {
            if (charsheet == null)
                return;
            Transform found = FindByName(charsheet, name, requireActive: true)
                              ?? FindByName(charsheet, name, requireActive: false);
            Selectable s = found != null ? found.GetComponent<Selectable>() : null;
            if (s != null)
                into.Add(new SelectableButton(s));
            else
                _host.LogWarning($"CharacterSheetScreen: '{name}' not found; it will be unreachable.");
        }

        // The shared Charsheet ancestor of a skill panel, the common root the leveling buttons hang under.
        private static Transform Charsheet(SkillPortraitPanel anchor)
        {
            Transform t = anchor.transform;
            while (t != null && t.name != "Charsheet")
                t = t.parent;
            return t;
        }

        private static Transform FindByName(Transform root, string name, bool requireActive)
        {
            if (root.name == name && (!requireActive || root.gameObject.activeInHierarchy))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindByName(root.GetChild(i), name, requireActive);
                if (hit != null)
                    return hit;
            }
            return null;
        }

        // The grid: one row per attribute - the attribute as column zero, then its six skills, in the game's
        // own row-major layout order.
        private Grid BuildGrid(List<SkillPortraitPanel> panels, SkillDetailCell detail)
        {
            var grid = new Grid();
            int cols = ColumnCount(panels[0]);
            int rows = (panels.Count + cols - 1) / cols;

            // The four attributes (the skill rows' left-hand labels) in ability order so they line up with
            // the row grouping. Prepend each as its row's column zero only when there is exactly one per row.
            var attrs = AttributePanels();
            bool useAttrs = attrs.Count == rows;
            if (!useAttrs && attrs.Count > 0)
                _host.LogWarning($"CharacterSheetScreen: {attrs.Count} attribute panels for {rows} rows; attributes omitted.");

            for (int r = 0; r < rows; r++)
            {
                var rowCells = new List<UIElement>(cols + 1);
                if (useAttrs)
                    rowCells.Add(new CharAttributeCell(attrs[r], detail));
                for (int c = 0; c < cols; c++)
                {
                    int i = r * cols + c;
                    if (i < panels.Count)
                        rowCells.Add(new LevelUpSkillCell(panels[i], _host, detail));
                }
                grid.AddRow(rowCells.ToArray());
            }
            return grid;
        }

        private static List<SkillPortraitPanel> ActivePanels()
        {
            var panels = new List<SkillPortraitPanel>();
            foreach (SkillPortraitPanel p in Object.FindObjectsOfType<SkillPortraitPanel>())
                if (p.gameObject.activeInHierarchy && p.selectButton != null)
                    panels.Add(p);
            return panels;
        }

        // The four attribute panels in ability order. In-game they carry no grade flip clock (the abilities
        // are fixed), so unlike Create Your Own they are taken as every active StatPanel.
        private static List<StatPanel> AttributePanels()
        {
            var attrs = new List<StatPanel>();
            foreach (StatPanel p in Object.FindObjectsOfType<StatPanel>())
                if (p.gameObject.activeInHierarchy)
                    attrs.Add(p);
            attrs.Sort((a, b) => ((int)a.ability).CompareTo((int)b.ability));
            return attrs;
        }

        // A signature of the live panel set: the active skill count and attribute count. Both appear a frame
        // or two after the transition, so the content rebuilds while either is still settling.
        private static int PanelSignature()
        {
            int skills = 0;
            foreach (SkillPortraitPanel p in Object.FindObjectsOfType<SkillPortraitPanel>())
                if (p.gameObject.activeInHierarchy && p.selectButton != null)
                    skills++;
            int attrs = 0;
            foreach (StatPanel p in Object.FindObjectsOfType<StatPanel>())
                if (p.gameObject.activeInHierarchy)
                    attrs++;
            return skills * 1000 + attrs;
        }

        // The grid's column count = the Skills GridLayoutGroup's fixed column count, the game's own width.
        // Falls back to six (the per-attribute skill count) if the layout is not the expected fixed-column
        // kind.
        private static int ColumnCount(SkillPortraitPanel anchor)
        {
            Transform skills = anchor.transform.parent;
            GridLayoutGroup glg = skills != null ? skills.GetComponent<GridLayoutGroup>() : null;
            if (glg != null && glg.constraint == GridLayoutGroup.Constraint.FixedColumnCount && glg.constraintCount > 0)
                return glg.constraintCount;
            return 6;
        }
    }
}
