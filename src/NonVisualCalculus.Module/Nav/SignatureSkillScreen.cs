using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.UI;
using Grid = NonVisualCalculus.Core.UI.Nav.Grid;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The Set Signature Skill step of Create Your Own: a 2D grid of the four attributes and their skills
    /// over a bottom button bar. The game lays the skills out as one GridLayoutGroup, six skills per
    /// attribute across four attribute rows (Intellect, Psyche, Physique, Motorics top to bottom); we
    /// prepend each row with its attribute (read-only here, see <see cref="AttributeCell"/>) as column zero,
    /// so the grid mirrors the screen: the attribute on the left, then its six skills. Up/Down move between
    /// attributes keeping the column, Left/Right move along a row. Enter on a skill sets it as the signature
    /// (see <see cref="SkillCell"/>) and re-announces the marker. Under the grid, in one vertical flow, sits
    /// a bottom bar (a list) holding the Back and Begin buttons: Down from the grid's bottom row spills into
    /// it (and Up returns to the grid). Back returns to adjust abilities (also the screen's Escape, see
    /// <see cref="ScreenRoot"/>); Begin starts the game, and the game activates it only once a signature is
    /// chosen.
    ///
    /// The charsheet's panels are instantiated a frame or two after the view transition, so the first build
    /// can find none: this is a rich screen whose <see cref="OnUpdate"/> populates the grid in place once
    /// the panels appear (and rebuilds if their counts change while they settle), re-homing focus.
    /// </summary>
    public sealed class SignatureSkillScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.CHARACTER_CREATION_SET_SKILL;
        public override string ScreenName => Strings.ScreenSignatureSkill;

        // The live tree we built, held as references into our own root (not cached game state). The content
        // is one vertical flow: the grid then the bottom bar, so Down spills from the grid's bottom row into
        // the bar and Up brings you back.
        private ScreenRoot _root;
        private Container _content;
        private Grid _grid;
        private bool _barAdded;
        // A signature of the panel set the grid was built from (skill count plus attribute count): the
        // charsheet's panels appear a frame or two after the transition, so this changes (e.g. 0 -> 24 skills
        // and 0 -> 4 attributes) and drives the in-place rebuild until they have settled.
        private int _builtSig;

        public override Container BuildRoot(IModHost host)
        {
            _root = new ScreenRoot();
            _content = new Container(ContainerShape.VerticalList);
            _grid = new Grid();
            _content.Add(_grid);
            _root.Add(_content);
            _barAdded = false;
            _builtSig = -1;
            Populate(host);
            return _root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            if (PanelSignature() == _builtSig)
                return false;
            // The panels appeared (or their counts changed while settling): rebuild the grid and re-home
            // focus so the landing is announced once.
            Populate(host);
            return nav.EnsureFocusValid();
        }

        // Build the grid one row per attribute - the attribute as column zero, then its six skills, in the
        // game's own row-major layout order - and add the bottom bar once the panels are available.
        private void Populate(IModHost host)
        {
            var panels = ActivePanels();
            _grid.Clear();
            if (panels.Count == 0)
            {
                _builtSig = PanelSignature();
                return;
            }
            panels.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            // The shared button that sets whichever skill is selected as the signature; each cell runs it.
            Button signature = SharedButton(panels[0], "Signature Skill Button");
            if (signature == null)
                host.LogWarning("SignatureSkillScreen: Signature Skill button not found; skills will not be settable.");

            int cols = ColumnCount(panels[0]);
            int rows = (panels.Count + cols - 1) / cols;
            // The four attributes (the skill rows' left-hand labels), in ability order so they line up with
            // the row grouping (Intellect, Psyche, Physique, Motorics). Prepend each as its row's column zero
            // only when there is exactly one per row; otherwise the rows are skills only.
            var attrs = AttributePanels();
            bool useAttrs = attrs.Count == rows;
            if (!useAttrs && attrs.Count > 0)
                host.LogWarning($"SignatureSkillScreen: {attrs.Count} attribute panels for {rows} rows; attributes omitted.");

            for (int r = 0; r < rows; r++)
            {
                var rowCells = new List<UIElement>(cols + 1);
                if (useAttrs)
                    rowCells.Add(new AttributeCell(attrs[r]));
                for (int c = 0; c < cols; c++)
                {
                    int i = r * cols + c;
                    if (i < panels.Count)
                        rowCells.Add(new SkillCell(panels[i], signature));
                }
                _grid.AddRow(rowCells.ToArray());
            }
            _builtSig = PanelSignature();

            AddBottomBar(host, panels[0]);
        }

        // The bottom bar, stacked under the grid in the content flow: a list holding Back then Begin. Back is
        // always available; the game activates Begin only once a signature is chosen, so until then it is
        // unfocusable. Down from the grid's bottom row spills into the bar. Added once.
        private void AddBottomBar(IModHost host, SkillPortraitPanel anchor)
        {
            if (_barAdded)
                return;
            var bar = new Container(ContainerShape.HorizontalList);

            Button back = SharedButton(anchor, "Back Button");
            if (back != null)
                bar.Add(new SelectableButton(back.GetComponent<Selectable>()));
            else
                host.LogWarning("SignatureSkillScreen: Back button not found; only Escape returns.");

            Button begin = SharedButton(anchor, "Start Game Button");
            if (begin != null)
                bar.Add(new SelectableButton(begin.GetComponent<Selectable>()));
            else
                host.LogWarning("SignatureSkillScreen: Start Game button not found; the player cannot begin from here.");

            if (bar.Children.Count > 0)
            {
                _content.Add(bar);
                _barAdded = true;
            }
        }

        private static List<SkillPortraitPanel> ActivePanels()
        {
            var panels = new List<SkillPortraitPanel>();
            foreach (SkillPortraitPanel p in Object.FindObjectsOfType<SkillPortraitPanel>())
                if (p.gameObject.activeInHierarchy && p.selectButton != null)
                    panels.Add(p);
            return panels;
        }

        // The four attribute panels (the ones carrying a grade flip clock; the character-sheet panels that
        // reuse the type have none), in ability order so they line up with the skill rows.
        private static List<StatPanel> AttributePanels()
        {
            var attrs = new List<StatPanel>();
            foreach (StatPanel p in Object.FindObjectsOfType<StatPanel>())
                if (p.gameObject.activeInHierarchy && p.abilityGradeFlipClock != null)
                    attrs.Add(p);
            attrs.Sort((a, b) => ((int)a.ability).CompareTo((int)b.ability));
            return attrs;
        }

        // A signature of the live panel set: the active skill count and attribute count. Both appear a frame
        // or two after the transition, so the grid rebuilds while either is still settling.
        private static int PanelSignature()
        {
            int skills = 0;
            foreach (SkillPortraitPanel p in Object.FindObjectsOfType<SkillPortraitPanel>())
                if (p.gameObject.activeInHierarchy && p.selectButton != null)
                    skills++;
            int attrs = 0;
            foreach (StatPanel p in Object.FindObjectsOfType<StatPanel>())
                if (p.gameObject.activeInHierarchy && p.abilityGradeFlipClock != null)
                    attrs++;
            return skills * 1000 + attrs;
        }

        // A shared charsheet button reached by name: climb from a skill panel to the shared Charsheet root,
        // then search its descendants. The buttons sit in different sub-panels (OverviewSection, Character
        // Sheet Info Panel, SetSignatureSkillMode), so a recursive find by name is simplest. An active match
        // is preferred over an inactive one: several charsheet modes carry a same-named button (e.g. "Back
        // Button"), only the current mode's active, while the Begin button is inactive until a signature is
        // chosen, so the inactive fallback still finds it.
        private static Button SharedButton(SkillPortraitPanel anchor, string name)
        {
            Transform t = anchor.transform;
            while (t != null && t.name != "Charsheet")
                t = t.parent;
            if (t == null)
                return null;
            Transform found = FindDescendant(t, name, requireActive: true)
                              ?? FindDescendant(t, name, requireActive: false);
            return found != null ? found.GetComponent<Button>() : null;
        }

        private static Transform FindDescendant(Transform root, string name, bool requireActive)
        {
            if (root.name == name && (!requireActive || root.gameObject.activeInHierarchy))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDescendant(root.GetChild(i), name, requireActive);
                if (hit != null)
                    return hit;
            }
            return null;
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
