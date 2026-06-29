using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.UI;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The options screen as a single flat list: the game's category sections (Graphics, Audio, ...)
    /// flattened into option controls read live through <see cref="OptionControl"/>, with the Reset button
    /// as the last item. The game's own Controls tab is the keyboard/controller reference, which a blind
    /// player reaches through the mod's own help overlay and whose bindings we are reworking anyway, so it
    /// is not surfaced here.
    /// </summary>
    public sealed class OptionsScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.OPTIONS;
        public override string ScreenName => Strings.ScreenOptions;

        public override Container BuildRoot(IModHost host)
        {
            var root = new ScreenRoot();

            var h = SettingsHeaderController.singleton;
            if (h == null)
            {
                host.LogWarning("OptionsScreen: SettingsHeaderController.singleton is null; empty screen.");
                return root;
            }

            // We only present the Settings view, so make sure it is the one shown (its controls are
            // inactive otherwise and the sweep below finds nothing).
            if (!h.settingsView.activeInHierarchy)
                h.SelectSettingsView();

            // Every option control under the Settings view in visual order. DE's own category grouping is
            // not read out - it added verbosity and the game's grouping does not map cleanly (e.g. the
            // dyslexic-font toggle sits under Audio).
            var list = new Container(ContainerShape.VerticalList);
            foreach (var osc in h.settingsView.transform.GetComponentsInChildren<OptionSelectableController>(false))
                list.Add(new OptionControl(osc));

            // Reset settings as the last item in the list (its activation opens the game's confirm popup,
            // which the dialog reader announces).
            Selectable reset = ResetButton(h);
            if (reset != null)
                list.Add(new SelectableButton(reset));
            else
                host.LogWarning("OptionsScreen: Reset settings button not found; it will be unreachable.");

            if (list.Children.Count == 0)
                host.LogWarning("OptionsScreen: Settings view built no navigable content.");
            root.Add(list);

            return root;
        }

        private static Selectable ResetButton(SettingsHeaderController h)
        {
            Transform t = h.settingsView != null ? h.settingsView.transform : null;
            while (t != null && t.name != "Options Screen")
                t = t.parent;
            if (t == null)
                return null;
            Transform reset = t.Find("Content/ResetSettings");
            return reset != null ? reset.GetComponent<Selectable>() : null;
        }
    }
}
