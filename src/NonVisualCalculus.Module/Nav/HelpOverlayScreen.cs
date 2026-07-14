using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The F1 help overlay. The screen is a single baked sprite (no live text), so its six tip cards are
    /// read from the game's own localized help terms (the <c>SETTINGS_HELP_*</c> I2 terms that also back the
    /// settings-menu tips) as one vertical list the player arrows through. The card set is fixed - the
    /// overlay is one monolithic image that cannot show a subset, so all six are always present (the
    /// progressive tips DE reveals as you play are its separate contextual tutorial tooltips, not this
    /// screen). The card text is grouped to match the sprite: the "white skill checks can be rerolled" line,
    /// which the term table files under Skill Points, sits under Skill Checks here as it does on the card.
    /// Escape closes the overlay the game's own way (<see cref="ScreenRoot"/>).
    /// </summary>
    public sealed class HelpOverlayScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.HELPOVERLAY;
        public override string ScreenName => Strings.ScreenHelp;

        public override Container BuildRoot(IModHost host)
        {
            var root = new ScreenRoot();
            var list = new Container(ContainerShape.VerticalList);

            list.Add(new HelpTipCell("SETTINGS_HELP_THOUGHTS_TITLE",
                "SETTINGS_HELP_THOUGHTS_DESCRIPTION", "SETTINGS_HELP_THOUGHTS_DESCRIPTION_2"));
            list.Add(new HelpTipCell("SETTINGS_HELP_TOOLS_TITLE",
                "SETTINGS_HELP_TOOLS_DESCRIPTION", "SETTINGS_HELP_TOOLS_DESCRIPTION_2"));
            list.Add(new HelpTipCell("SETTINGS_HELP_TIME_TITLE",
                "SETTINGS_HELP_TIME_DESCRIPTION", "SETTINGS_HELP_TIME_DESCRIPTION_2"));
            list.Add(new HelpTipCell("SETTINGS_HELP_TASKS_TITLE",
                "SETTINGS_HELP_TASKS_DESCRIPTION", "SETTINGS_HELP_TASKS_DESCRIPTION_2"));
            list.Add(new HelpTipCell("SETTINGS_HELP_SKILL_POINTS_TITLE",
                "SETTINGS_HELP_SKILL_POINTS_DESCRIPTION"));
            list.Add(new HelpTipCell("SETTINGS_HELP_SKILL_CHECKS_TITLE",
                "SETTINGS_HELP_SKILL_POINTS_DESCRIPTION_2",
                "SETTINGS_HELP_SKILL_CHECKS_DESCRIPTION", "SETTINGS_HELP_SKILL_CHECKS_DESCRIPTION_2"));

            root.Add(list);
            return root;
        }
    }
}
