using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.Text;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Metric;
using Sunshine.Views;
using UnityEngine;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The finished-thought splash (<see cref="ThoughtSplashScreenView"/>): the full-screen takeover shown
    /// when a thought completes internalization (reached organically through its breakthrough orb, or by
    /// opening the thought cabinet with one pending). The tree is the thought's name, its completion
    /// bonuses, its completion description, and the game's accept button; Escape accepts through that same
    /// button. Auto-speak follows the Automatically read dialogue setting the way conversations and the
    /// newspaper do: entry announces the screen and lands on the name, the bonuses and description are
    /// queued only when the setting is on - off leaves them one arrow away.
    ///
    /// Accepting with more finished thoughts queued swaps the next one into the view in place (the game
    /// pops <c>discoveredThoughts</c> with no view transition), so <see cref="OnUpdate"/> watches the
    /// displayed project and re-homes to the new thought's name. The bonuses are read from the view's own
    /// composed text (each effect plus its flavor line, the exact lines a sighted player sees); the name
    /// and description come from the project model, which the view mirrors verbatim.
    /// </summary>
    internal sealed class ThoughtSplashScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.THOUGHTSPLASHSCREEN;
        public override string ScreenName => Strings.ScreenThoughtComplete;

        // The live view, held as a reference and read at speech time (never its data), and the project
        // whose body we delivered under the auto-read gate, so each thought - the entry one included - is
        // delivered exactly once. Null marks a fresh build, whose delivery the first OnUpdate makes after
        // the ScreenManager's landing announce.
        private ThoughtSplashScreenView _view;
        private ThoughtCabinetProject _delivered;
        private UIElement _name;

        public override Container BuildRoot(IModHost host)
        {
            // Found inactive too: the view object lives in the global UI canvas permanently, and its
            // activation can trail the view transition by a frame.
            _view = Object.FindObjectOfType<ThoughtSplashScreenView>(true);
            _delivered = null;
            var root = new SplashRoot(_view);
            _name = new ReadonlyTextCell(Name);
            root.Add(_name);
            root.Add(new ReadonlyTextCell(Bonuses));
            root.Add(new ReadonlyTextCell(Description));
            root.Add(new ClickButton(_view.buttonClose));
            root.SetFocusedChild(_name);
            return root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            ThoughtCabinetProject current = _view.currentProject;
            if (current == _delivered)
                return false;
            bool entry = _delivered == null;
            _delivered = current;
            bool auto = host.Settings.AutoReadDialogue.Value;
            // The accept just swapped the next finished thought in place: re-home to its name. With
            // auto-read on the whole result is queued here; off, the returned re-home has the
            // ScreenManager announce the name alone (there is no narrator VO to carry a silent swap).
            if (!entry)
            {
                nav.Focus(_name, announce: false);
                if (auto)
                    host.Speech.Speak(Name(), interrupt: false);
            }
            if (auto)
            {
                host.Speech.Speak(Bonuses(), interrupt: false);
                host.Speech.Speak(Description(), interrupt: false);
                return false;
            }
            return !entry;
        }

        private string Name() => TextFilter.Clean(_view.currentProject.GetDisplayName());
        private string Bonuses() => TextFilter.Clean(GameLocalization.Spoken(_view.propertiesText));
        private string Description() => TextFilter.Clean(_view.currentProject.completionDescription);

        /// <summary>The splash's stops. Back (Escape) accepts through the game's own button - the same
        /// handler as its controller close, advancing to the next queued thought or closing the view.</summary>
        private sealed class SplashRoot : Container
        {
            private readonly ThoughtSplashScreenView _view;

            public SplashRoot(ThoughtSplashScreenView view) : base(ContainerShape.VerticalList) => _view = view;

            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Back, () => _view.buttonClose.onClick.Invoke());
            }
        }
    }
}
