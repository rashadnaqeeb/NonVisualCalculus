using System.Collections.Generic;
using NonVisualCalculus.Core.Input;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class KeyHelpTests
    {
        // A keyboard-typed binding: the snapshot selects by InputBinding.KeyboardType and speaks
        // DisplayName, which doubles as the chord id (two bindings sharing a name collide, the
        // shadowing case).
        private sealed class KeyBinding : InputBinding
        {
            private readonly string _display;
            public KeyBinding(string display) { _display = display; }
            public override string DisplayName => _display;
            public override bool JustPressed() => false;
            public override bool Held() => false;
            public override bool Released() => false;
            public override string Type => KeyboardType;
            public override string Serialize() => _display;
        }

        // A non-keyboard binding (a pad control): live, but never listed by the snapshot.
        private sealed class PadBinding : InputBinding
        {
            private readonly string _display;
            public PadBinding(string display) { _display = display; }
            public override string DisplayName => _display;
            public override bool JustPressed() => false;
            public override bool Held() => false;
            public override bool Released() => false;
            public override string Type => "pad";
            public override string Serialize() => _display;
        }

        // ---- SnapshotLiveKeys ----

        [Fact]
        public void Snapshot_ListsActiveCategoriesThenGlobal_ExcludingInactive()
        {
            var mgr = new InputManager();
            mgr.Register("world.a", "world a", InputCategory.World).AddBinding(new KeyBinding("W"));
            mgr.Register("ui.a", "ui a", InputCategory.UI).AddBinding(new KeyBinding("UpArrow"));
            mgr.Register("global.a", "global a", InputCategory.Global).AddBinding(new KeyBinding("F12"));
            mgr.ActiveCategoriesProvider = () => new[] { InputCategory.World };

            var entries = mgr.SnapshotLiveKeys();

            Assert.Equal(new[] { "world.a", "global.a" }, entries.ConvertAll(e => e.ActionKey));
        }

        [Fact]
        public void Snapshot_ExcludesChordShadowedByHigherCategory()
        {
            var mgr = new InputManager();
            // Status precedes UI (the dialogue case): the heal claims LeftArrow, the UI direction loses it.
            mgr.Register("heal", "heal", InputCategory.Status).AddBinding(new KeyBinding("LeftArrow"));
            mgr.Register("ui.left", "ui left", InputCategory.UI).AddBinding(new KeyBinding("LeftArrow"));
            mgr.ActiveCategoriesProvider = () => new[] { InputCategory.Status, InputCategory.UI };

            var entries = mgr.SnapshotLiveKeys();

            Assert.Equal(new[] { "heal" }, entries.ConvertAll(e => e.ActionKey));
        }

        [Fact]
        public void Snapshot_ListsKeyboardChordsOnly()
        {
            var mgr = new InputManager();
            mgr.Register("mixed", "mixed", InputCategory.Global)
                .AddBinding(new KeyBinding("Return")).AddBinding(new PadBinding("Action1"));
            mgr.Register("padonly", "pad only", InputCategory.Global).AddBinding(new PadBinding("Action2"));

            var entries = mgr.SnapshotLiveKeys();

            var entry = Assert.Single(entries);
            Assert.Equal("mixed", entry.ActionKey);
            Assert.Equal(new[] { "Return" }, entry.Chords);
        }

        // ---- Compose ----

        private static KeyHelpEntry E(string key, string label, params string[] chords)
            => new KeyHelpEntry(key, label, chords);

        private static readonly KeyHelpGroup[] Arrows =
        {
            new KeyHelpGroup("Navigate", "arrow keys", new[] { "ui.up", "ui.down", "ui.left", "ui.right" }),
        };

        [Fact]
        public void Compose_CollapsesCompleteGroupAtFirstMemberPosition()
        {
            var lines = KeyHelp.Compose(new[]
            {
                E("ui.up", "Navigate up", "UpArrow"),
                E("ui.down", "Navigate down", "DownArrow"),
                E("ui.tab", "Next control", "Tab"),
                E("ui.left", "Navigate left", "LeftArrow"),
                E("ui.right", "Navigate right", "RightArrow"),
            }, Arrows);

            Assert.Equal(new[] { "Navigate, arrow keys", "Next control, Tab" }, lines);
        }

        [Fact]
        public void Compose_PartialGroupReadsMembersIndividually()
        {
            // The dialogue case: Left/Right were claimed by the heals, only Up/Down survive.
            var lines = KeyHelp.Compose(new[]
            {
                E("ui.up", "Navigate up", "UpArrow"),
                E("ui.down", "Navigate down", "DownArrow"),
            }, Arrows);

            Assert.Equal(new[] { "Navigate up, Up arrow", "Navigate down, Down arrow" }, lines);
        }

        [Fact]
        public void Compose_DerivesPairGroupKeysFromLiveBindings()
        {
            // No authored keys phrase: the pair's line reads the members' own chords, in entry
            // (registration) order, through the speakable key names.
            var jump = new[] { new KeyHelpGroup("Jump to first or last", null, new[] { "ui.home", "ui.end" }) };
            var lines = KeyHelp.Compose(new[]
            {
                E("ui.home", "Jump to first", "Home"),
                E("ui.end", "Jump to last", "End"),
            }, jump);

            Assert.Equal(new[] { "Jump to first or last, Home, End" }, lines);
        }

        [Fact]
        public void Compose_DerivedGroupKeysRenderSpeakableWords()
        {
            var scan = new[] { new KeyHelpGroup("Scanner next and previous thing", null, new[] { "scan.next", "scan.prev" }) };
            var lines = KeyHelp.Compose(new[]
            {
                E("scan.next", "Scanner next thing", "PageDown"),
                E("scan.prev", "Scanner previous thing", "PageUp"),
            }, scan);

            Assert.Equal(new[] { "Scanner next and previous thing, Page down, Page up" }, lines);
        }

        [Fact]
        public void Compose_JoinsMultipleChords()
        {
            var lines = KeyHelp.Compose(new[] { E("save", "Quick save", "F5", "Alt+S") }, new KeyHelpGroup[0]);

            Assert.Equal(new[] { "Quick save, F5, Alt S" }, lines);
        }

        [Theory]
        [InlineData("Ctrl+PageDown", "Control Page down")]
        [InlineData("Shift+F1", "Shift F1")]
        [InlineData("Alpha1", "1")]
        [InlineData("T", "T")]
        [InlineData("KeypadEnter", "Numpad enter")]
        public void SpokenChord_RendersSpeakableWords(string display, string spoken)
        {
            Assert.Equal(spoken, KeyHelp.SpokenChord(display));
        }
    }
}
