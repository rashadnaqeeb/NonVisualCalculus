using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;
using Newspaper = Sunshine.Newspaper;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The endgame newspaper (<see cref="Sunshine.Newspaper"/>): the full-screen article the game shows
    /// after a death (and the game-completion ending), floating in the lobby scene under the LOBBY view
    /// before the main menu engages. The tree is the headline, the article text, the page number and
    /// paging arrows (present only with several queued articles), and the game's close button; Escape
    /// closes through that button, the same handler as the game's own cancel input (which is muted while
    /// we own the keyboard). The narrator reads each article aloud (the game requests VO as the article
    /// shows), so auto-speak follows the Automatically read dialogue setting the same way conversations
    /// do: entry announces the screen and lands on the headline, the article body is queued only when the
    /// setting is on, and paging to another article delivers the new headline and body under the same
    /// gate - off leaves the narrator to carry it, with the cursor on the headline for on-demand reading.
    /// </summary>
    internal sealed class NewspaperScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.LOBBY;
        public override string ScreenName => Strings.ScreenNewspaper;

        public override bool AppliesNow() => Find() != null;

        // The live component, held as a reference and read at speech time (never its data).
        private Newspaper _paper;
        private UIElement _headline;
        // The article index whose body we delivered under the auto-read gate, so each article - the entry
        // one included - is delivered exactly once. MinValue marks a fresh build, whose headline the
        // ScreenManager's landing announce covers.
        private int _deliveredIndex;

        // Active-in-hierarchy only (FindObjectOfType skips inactive), so this goes false the moment the
        // game closes the newspaper and the main menu takes over.
        private static Newspaper Find() => Object.FindObjectOfType<Newspaper>();

        public override Container BuildRoot(IModHost host)
        {
            _paper = Find();
            _deliveredIndex = int.MinValue;
            // The newspaper can drop between AppliesNow and the build (closed the same frame); the empty
            // root then detaches next frame when AppliesNow goes false.
            if (_paper == null)
                return new Container(ContainerShape.VerticalList);

            // The close is wired on a child button the component keeps no field for: its onClick carries
            // CloseNewspaper plus the click sound, the exact handler a sighted click runs.
            var close = _paper.transform.Find("Button").GetComponent<UnityEngine.UI.Button>();
            var root = new NewspaperRoot(close);
            _headline = new ReadonlyTextCell(() => TextFilter.Clean(GameLocalization.Cased(_paper.title)));
            root.Add(_headline);
            root.Add(new ReadonlyTextCell(() => TextFilter.Clean(GameLocalization.Cased(_paper.opener))));
            // "2/3" while several articles are queued; empty (and skipped) with one.
            root.Add(new ReadonlyTextCell(() => TextFilter.Clean(_paper.pageNr.text)));
            root.Add(new ArrowButton(_paper.back, Strings.NewspaperPreviousArticle));
            root.Add(new ArrowButton(_paper.forward, Strings.NewspaperNextArticle));
            root.Add(new ClickButton(close));
            root.SetFocusedChild(_headline);
            return root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            int index = _paper.currentArticleIndex;
            if (index == _deliveredIndex)
                return false;
            bool entry = _deliveredIndex == int.MinValue;
            _deliveredIndex = index;
            bool auto = host.Settings.AutoReadDialogue.Value;
            // A paging landed a new article in place: re-home to its headline silently and deliver it
            // under the gate (the entry article's headline was the landing announce instead).
            if (!entry)
            {
                nav.Focus(_headline, announce: false);
                if (auto)
                    host.Speech.Speak(TextFilter.Clean(GameLocalization.Cased(_paper.title)), interrupt: false);
            }
            if (auto)
                host.Speech.Speak(TextFilter.Clean(GameLocalization.Cased(_paper.opener)), interrupt: false);
            return false;
        }

        /// <summary>The article list. Back (Escape) closes through the game's own close button - the same
        /// handler and sound as clicking it.</summary>
        private sealed class NewspaperRoot : Container
        {
            private readonly UnityEngine.UI.Button _close;

            public NewspaperRoot(UnityEngine.UI.Button close) : base(ContainerShape.VerticalList) => _close = close;

            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Back, () => _close.onClick.Invoke());
            }
        }

        /// <summary>An article-paging arrow: image-only with no caption term anywhere under it, so the
        /// label is authored; activation invokes the game button's own onClick (the paging and its
        /// sound). The game hides each arrow at its end of the list, so it drops out of nav there.</summary>
        private sealed class ArrowButton : UIElement
        {
            private readonly UnityEngine.UI.Button _button;
            private readonly string _label;

            public ArrowButton(UnityEngine.UI.Button button, string label)
            {
                _button = button;
                _label = label;
            }

            public override bool CanFocus => _button != null && _button.isActiveAndEnabled && _button.interactable;
            public override string Label => _label;
            public override string Role => Strings.RoleButton;

            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Activate, () => _button.onClick.Invoke());
            }
        }
    }
}
