using System.Collections.Generic;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using Xunit;

namespace DiscoAccess.Tests
{
    public class NavigatorTests
    {
        // A focusable leaf with a label, a "button" role, and a recorded activation.
        private sealed class Button : UIElement
        {
            private readonly string _label;
            public int Activations;
            public Button(string label) { _label = label; }
            public override string Label => _label;
            public override string Role => "button";
            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Activate, () => Activations++);
            }
        }

        // A focusable stepper that clamps at a floor and a cap, using the universal default adjust
        // announcement (a move reads the value; an adjust that hits an end names the bound).
        private sealed class Stepper : UIElement
        {
            private readonly int _min, _max;
            public int Val;
            public Stepper(int val, int min, int max) { Val = val; _min = min; _max = max; }
            public override string Value => Val.ToString();
            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Decrease, () => { if (Val > _min) Val--; });
                yield return new ElementAction(ActionIds.Increase, () => { if (Val < _max) Val++; });
            }
        }

        // A stepper that never moves and overrides the adjust announcement, as the ability control (its
        // "no more points" cue) and the option dropdown do.
        private sealed class CustomAdjustStepper : UIElement
        {
            public override string Value => "5";
            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Increase, () => { });
            }
            public override string GetAdjustText(string actionId, bool changed)
                => changed ? GetValueText() : "blocked here";
        }

        private static (Container root, Stepper stepper) StepperTree(int val, int min, int max)
        {
            var root = new Container(ContainerShape.Panel);
            var list = new Container(ContainerShape.VerticalList);
            var stepper = new Stepper(val, min, max);
            list.Add(stepper);
            root.Add(list);
            return (root, stepper);
        }

        private readonly List<string> _spoken = new List<string>();
        private TraditionalNavigator NewNav() => new TraditionalNavigator((t, i) => _spoken.Add(t));

        // Panel root > labeled vertical list "main menu" of [Continue, New Game, Load Game].
        private static (Container root, Container list, Button[] items) MainMenuTree()
        {
            var root = new Container(ContainerShape.Panel);
            var list = new Container(ContainerShape.VerticalList, "main menu");
            var items = new[] { new Button("Continue"), new Button("New Game"), new Button("Load Game") };
            foreach (var b in items) list.Add(b);
            root.Add(list);
            return (root, list, items);
        }

        [Fact]
        public void Attach_LandsOnFirstItem_AnnounceReadsPathThenLeaf()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);
            Assert.Same(items[0], nav.Current);

            nav.AnnounceCurrent();
            Assert.Equal(new[] { "main menu, Continue, button" }, _spoken);
        }

        [Fact]
        public void Down_MovesToNextItem_AnnouncesOnlyTheLeaf()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);

            Assert.True(nav.Handle(UiActions.Down));
            Assert.Same(items[1], nav.Current);
            Assert.Equal(new[] { "New Game, button" }, _spoken);
        }

        [Fact]
        public void Increase_WhenValueMoves_AnnouncesNewValue()
        {
            var (root, stepper) = StepperTree(val: 1, min: 1, max: 3);
            var nav = NewNav();
            nav.Attach(root);

            Assert.True(nav.Handle(UiActions.Right));
            Assert.Equal(2, stepper.Val);
            Assert.Equal("2", _spoken[^1]);
        }

        [Fact]
        public void Increase_AtMaximum_AnnouncesMaximum()
        {
            var (root, stepper) = StepperTree(val: 3, min: 1, max: 3); // already at the cap
            var nav = NewNav();
            nav.Attach(root);

            Assert.True(nav.Handle(UiActions.Right)); // consumed: the action exists
            Assert.Equal(3, stepper.Val);             // but the value did not move
            Assert.Equal(Strings.StatusMaximum, _spoken[^1]);
        }

        [Fact]
        public void Decrease_AtMinimum_AnnouncesMinimum()
        {
            var (root, stepper) = StepperTree(val: 1, min: 1, max: 3); // already at the floor
            var nav = NewNav();
            nav.Attach(root);

            Assert.True(nav.Handle(UiActions.Left));
            Assert.Equal(1, stepper.Val);
            Assert.Equal(Strings.StatusMinimum, _spoken[^1]);
        }

        [Fact]
        public void BlockedAdjust_ElementMayOverrideTheAnnouncement()
        {
            var root = new Container(ContainerShape.Panel);
            var list = new Container(ContainerShape.VerticalList);
            var stepper = new CustomAdjustStepper();
            list.Add(stepper);
            root.Add(list);
            var nav = NewNav();
            nav.Attach(root);

            Assert.True(nav.Handle(UiActions.Right));
            Assert.Equal("blocked here", _spoken[^1]); // the override replaces the default "maximum"
        }

        [Fact]
        public void Up_AtTop_DoesNotMove_AndIsNotConsumed()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);

            Assert.False(nav.Handle(UiActions.Up)); // nothing above the first item
            Assert.Same(items[0], nav.Current);
            Assert.Empty(_spoken);
        }

        [Fact]
        public void End_JumpsToLast_Home_JumpsToFirst()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);

            Assert.True(nav.Handle(UiActions.End));
            Assert.Same(items[2], nav.Current);
            Assert.Equal("Load Game, button", _spoken[^1]);

            Assert.True(nav.Handle(UiActions.Home));
            Assert.Same(items[0], nav.Current);
            Assert.Equal("Continue, button", _spoken[^1]);
        }

        [Fact]
        public void Activate_FiresFocusedLeafAction()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);

            Assert.True(nav.Handle(UiActions.Activate));
            Assert.Equal(1, items[0].Activations);
            Assert.Equal(0, items[1].Activations);
        }

        [Fact]
        public void Tab_MovesBetweenLists_InAPanel()
        {
            var root = new Container(ContainerShape.Panel);
            var menu = new Container(ContainerShape.VerticalList, "main menu");
            menu.Add(new Button("Continue"));
            var side = new Container(ContainerShape.VerticalList, "details");
            var detail = new Button("Version");
            side.Add(detail);
            root.Add(menu);
            root.Add(side);

            var nav = NewNav();
            nav.Attach(root);
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Next));
            Assert.Same(detail, nav.Current);
            Assert.Equal(new[] { "details, Version, button" }, _spoken);

            Assert.True(nav.Handle(UiActions.Prev));
            Assert.Equal("main menu, Continue, button", _spoken[^1]);
        }

        [Fact]
        public void Back_ConsumedOnlyWhenRootAdvertisesBack()
        {
            var (root, _, _) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);
            Assert.False(nav.Handle(UiActions.Back)); // plain root has no back action

            int backs = 0;
            var withBack = new BackContainer(() => backs++);
            var list = new Container(ContainerShape.VerticalList, "settings");
            list.Add(new Button("Volume"));
            withBack.Add(list);
            var nav2 = NewNav();
            nav2.Attach(withBack);
            Assert.True(nav2.Handle(UiActions.Back));
            Assert.Equal(1, backs);
        }

        [Fact]
        public void EnsureFocusValid_NoOp_WhenFocusStillReachable()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);

            Assert.False(nav.EnsureFocusValid()); // focus intact, nothing to do
            Assert.Same(items[0], nav.Current);
        }

        [Fact]
        public void EnsureFocusValid_Rehomes_WhenFocusOrphanedByRebuild()
        {
            var root = new Container(ContainerShape.Panel);
            var list = new Container(ContainerShape.VerticalList, "settings");
            list.Add(new Button("A"));
            list.Add(new Button("B"));
            root.Add(list);
            var nav = NewNav();
            nav.Attach(root);
            Assert.True(nav.Handle(UiActions.Down)); // focus the second item
            Assert.Equal("B", nav.Current!.Label);

            // A dynamic rebuild clears the list and refills it, orphaning the focused element.
            list.Clear();
            list.Add(new Button("C"));

            Assert.True(nav.EnsureFocusValid()); // orphaned -> re-homed
            Assert.Equal("C", nav.Current!.Label); // re-landed on the rebuilt list's first focusable
        }

        [Fact]
        public void EnsureFocusValid_Rehomes_WhenAncestorReplacedButImmediateParentIntact()
        {
            // The options-screen rebuild shape: a content Panel holds an inner list which holds the controls.
            // A tab switch replaces the WHOLE inner list (clear the panel, add a fresh list), so the focused
            // control's immediate parent (the old list) still lists it, but that old list is no longer under
            // the panel. An immediate-parent-only orphan check misses this; the full-chain walk catches it.
            var root = new Container(ContainerShape.Panel);
            var content = new Container(ContainerShape.Panel);
            var oldList = new Container(ContainerShape.VerticalList, "settings");
            oldList.Add(new Button("A"));
            oldList.Add(new Button("B"));
            content.Add(oldList);
            root.Add(content);
            var nav = NewNav();
            nav.Attach(root);
            Assert.True(nav.Handle(UiActions.Down));
            Assert.Equal("B", nav.Current!.Label);

            // Tab switch: the content panel drops the old list (whose children keep their back-pointers) and
            // gets a brand-new list. oldList still .Contains the focused "B", but content no longer .Contains oldList.
            content.Clear();
            var newList = new Container(ContainerShape.VerticalList, "controls");
            newList.Add(new Button("C"));
            content.Add(newList);

            Assert.True(nav.EnsureFocusValid()); // ancestor link broken -> orphaned -> re-homed
            Assert.Equal("C", nav.Current!.Label);
        }

        [Fact]
        public void TypeSearch_LandsOnMatchingItem()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);
            _spoken.Clear();

            nav.TypeSearchChar('n'); // "New Game" starts with n
            Assert.Same(items[1], nav.Current);
            Assert.Equal("New Game, button", _spoken[^1]);
            Assert.True(nav.SearchActive);
        }

        [Fact]
        public void WhileSearching_DownStepsResults_NotFocus()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);
            nav.TypeSearchChar('e'); // matches Continue and New Game (substring); shortest-name first
            var afterType = nav.Current;
            _spoken.Clear();

            // Down is consumed as a result step (search active), landing on the other match, not item below.
            Assert.True(nav.Handle(UiActions.Down));
            Assert.NotSame(afterType, nav.Current);
            Assert.True(nav.SearchActive);
        }

        [Fact]
        public void Escape_WhileSearching_ClearsSearch_AndAnnounces()
        {
            var (root, _, _) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);
            nav.TypeSearchChar('n');
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Back)); // Escape clears the live search first
            Assert.False(nav.SearchActive);
            Assert.Equal("search cleared", _spoken[^1]);
        }

        [Fact]
        public void NonSearchKey_WhileSearching_EndsSearch_AndActsNormally()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);
            nav.TypeSearchChar('n'); // lands on New Game

            // Enter is not a search key: it ends the search and activates the found item.
            Assert.True(nav.Handle(UiActions.Activate));
            Assert.False(nav.SearchActive);
            Assert.Equal(1, items[1].Activations); // New Game activated
        }

        [Fact]
        public void TypeSearch_NoMatch_AnnouncesBuffer_AndKeepsFocus()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);
            _spoken.Clear();

            nav.TypeSearchChar('z'); // nothing in the list matches
            Assert.Same(items[0], nav.Current); // focus unchanged
            Assert.Equal("z, no match", _spoken[^1]);
        }

        [Fact]
        public void TypeSearch_WorksWhenRootIsTheListItself()
        {
            // The title main menu's root is a bare vertical list (no Panel wrapper). Search must scope to
            // the whole list, not collapse to the single focused item.
            var list = new Container(ContainerShape.VerticalList);
            var items = new[] { new Button("Continue"), new Button("New Game"), new Button("Options"), new Button("Quit") };
            foreach (var b in items) list.Add(b);
            var nav = NewNav();
            nav.Attach(list); // the root IS the list
            _spoken.Clear();

            nav.TypeSearchChar('o'); // "Options" - not the focused item (focus lands on Continue)
            Assert.Same(items[2], nav.Current);
            Assert.Equal("Options, button", _spoken[^1]);
        }

        [Fact]
        public void Backspace_ReMatchesShorterBuffer()
        {
            var root = new Container(ContainerShape.Panel);
            var list = new Container(ContainerShape.VerticalList, "fruit");
            var apple = new Button("Apple");
            var apricot = new Button("Apricot");
            list.Add(apple);
            list.Add(apricot);
            root.Add(list);
            var nav = NewNav();
            nav.Attach(root);

            nav.TypeSearchChar('a');
            nav.TypeSearchChar('p');
            nav.TypeSearchChar('r');
            nav.TypeSearchChar('i'); // "apri" -> only Apricot
            Assert.Same(apricot, nav.Current);

            nav.BackspaceSearch();
            nav.BackspaceSearch(); // back to "ap" -> Apple (shorter) ranks first again
            Assert.Same(apple, nav.Current);
            Assert.True(nav.SearchActive);
        }

        [Fact]
        public void Backspace_EmptyingBuffer_ClearsAndAnnounces()
        {
            var (root, _, _) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);
            nav.TypeSearchChar('n'); // one-character buffer
            _spoken.Clear();

            nav.BackspaceSearch(); // deletes the last (only) character -> search ends
            Assert.False(nav.SearchActive);
            Assert.Equal("search cleared", _spoken[^1]);
        }

        [Fact]
        public void Backspace_WithNoBuffer_DoesNothing()
        {
            var (root, _, items) = MainMenuTree();
            var nav = NewNav();
            nav.Attach(root);
            _spoken.Clear();

            nav.BackspaceSearch(); // no live search -> a no-op
            Assert.Same(items[0], nav.Current);
            Assert.False(nav.SearchActive);
            Assert.Empty(_spoken);
        }

        // A screen root that advertises a back action (Escape closes it).
        private sealed class BackContainer : Container
        {
            private readonly System.Action _back;
            public BackContainer(System.Action back) : base(ContainerShape.Panel) { _back = back; }
            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Back, _back);
            }
        }
    }
}
