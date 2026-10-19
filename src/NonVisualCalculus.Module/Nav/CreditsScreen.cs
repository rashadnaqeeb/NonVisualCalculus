using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using Credits = Sunshine.Credits;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The end credits (<see cref="Sunshine.Credits"/>) that roll after the final ending: the game's
    /// <c>EndgameManager.CreditsEndgame</c> calls <c>ShowCredits</c> once the closing conversation ends,
    /// which plays a ten-minute timeline scrolling a column of legacy Text labels (a title, then the names
    /// under it) up the screen, then returns to the main menu on its own. The view stays CLEAR throughout,
    /// so this screen registers for CLEAR and applies while that timeline plays.
    ///
    /// Each label is spoken, queued, as its top edge scrolls in over the bottom of the screen, so speech
    /// follows the scroll in the order a sighted viewer sees it. Nothing is navigable: the tree is an empty
    /// root whose Back (Escape) runs the game's close button, wired to <c>Credits.ReturnToLobby</c> - the
    /// fade to the main menu the timeline itself runs at its end. The game never shows that button and
    /// swallows Escape while the credits roll, and its Pause action would open the pause menu over the
    /// still-rolling credits, so this screen claims Escape (<see cref="HandlesBack"/>).
    /// </summary>
    internal sealed class CreditsScreen : Screen
    {
        public override ViewType ViewType => ViewType.CLEAR;
        public override string ScreenName => Strings.ScreenCredits;
        public override bool TypeAheadEnabled => false;
        public override bool HandlesBack => true;

        // The credits sit on the global UI canvas from boot with their Scroller already active, so only the
        // playing show timeline marks them rolling; Stop (on close or at the end) drops it.
        public override bool AppliesNow()
        {
            Credits credits = Credits.Singleton;
            if (credits == null)
                return false;
            PlayableDirector director = credits.animationDirector;
            return director.state == PlayState.Playing && director.playableAsset == credits.showState;
        }

        // The live components, held as references and read at speech time.
        private Credits _credits;
        private RectTransform _frame;
        // The labels not yet spoken, in no particular order: each frame speaks the ones that have scrolled
        // into view, top first, and drops them from here.
        private readonly List<Text> _pending = new List<Text>();
        private readonly List<(float Top, Text Label)> _entered = new List<(float, Text)>();

        public override Container BuildRoot(IModHost host)
        {
            _credits = Credits.Singleton;
            _frame = _credits.GetComponent<RectTransform>();
            _pending.Clear();
            // Inactive ones included: a label the credits leave switched off stays pending and unspoken.
            // Positions are not judged here: the build lands on the roll's first frame, before the timeline
            // has moved the scroller back from where the previous roll left it.
            foreach (Text label in _credits.transform.Find("Scroller").GetComponentsInChildren<Text>(true))
                _pending.Add(label);
            return new CreditsRoot(_credits, host);
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            // Closing (Escape, or the timeline's own end) fades to black while the roll keeps moving for a
            // few seconds; what scrolls on under the fade is not read.
            if (_credits.isTransitionToLobby)
                return false;
            float bottom = _frame.rect.yMin;
            _entered.Clear();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Text label = _pending[i];
                if (!label.isActiveAndEnabled)
                    continue;
                float top = TopInFrame(label.rectTransform);
                if (top < bottom)
                    continue;
                _entered.Add((top, label));
                _pending.RemoveAt(i);
            }
            if (_entered.Count == 0)
                return false;
            _entered.Sort((a, b) => b.Top.CompareTo(a.Top));
            // The raw text, not GameLocalization.Cased: a block of names sits one per line, and the
            // pipeline's filter turns those breaks into pauses, where Cased's bracket strip would run the
            // names together. The credit titles' own terms are all-caps, so there is nothing to recase.
            foreach ((float _, Text label) in _entered)
                host.Speech.Speak(label.text, interrupt: false);
            return false;
        }

        // A label's top edge in the credits frame's space, where the frame's rect is the visible screen.
        // From the rect rather than GetWorldCorners, whose array the interop layer does not copy back.
        private float TopInFrame(RectTransform label)
        {
            Rect r = label.rect;
            Vector3 world = label.TransformPoint(new Vector3(r.center.x, r.yMax, 0f));
            return _frame.InverseTransformPoint(world).y;
        }

        /// <summary>The credits' root: nothing to navigate, and Back closes the credits through the game's
        /// own close button, cutting off the credits still queued in speech.</summary>
        private sealed class CreditsRoot : Container
        {
            private readonly Credits _credits;
            private readonly IModHost _host;

            public CreditsRoot(Credits credits, IModHost host) : base(ContainerShape.VerticalList)
            {
                _credits = credits;
                _host = host;
            }

            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Back, Close);
            }

            private void Close()
            {
                _host.Speech.Stop();
                _credits.closeButton.onClick.Invoke();
            }
        }
    }
}
