using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// Base for a navigable screen: it matches a game <see cref="ViewType"/>, carries an authored name
    /// spoken on entry, and builds its element tree fresh each time it is entered (read live, never
    /// cached). The ScreenManager resolves the active screen from <c>ViewsPagesBridge.Current</c> and
    /// attaches the navigator to the built root.
    /// </summary>
    public abstract class Screen
    {
        /// <summary>The game view this screen reads.</summary>
        public abstract ViewType ViewType { get; }

        /// <summary>Finer applicability within a <see cref="ViewType"/>, for a view the game reuses for
        /// distinct screens (MAINMENU is both the title menu and the in-game pause menu). The ScreenManager
        /// picks the first registered screen whose ViewType matches and whose <see cref="AppliesNow"/> is
        /// true, so register a more specific screen before the general fallback. Default true.</summary>
        public virtual bool AppliesNow() => true;

        /// <summary>Authored screen name spoken when the screen is entered.</summary>
        public abstract string ScreenName { get; }

        /// <summary>Build the navigable tree from live game state. Called on each entry.</summary>
        public abstract Container BuildRoot(IModHost host);

        /// <summary>Whether type-ahead search is active while this screen owns the keyboard. A screen with
        /// nothing to search through (the conversation view) overrides this to false so bare letters are
        /// free for other use. Default true.</summary>
        public virtual bool TypeAheadEnabled => true;

        /// <summary>Whether this screen wants the <see cref="NonVisualCalculus.Core.Input.InputCategory.Status"/>
        /// keys (time/money/health reads and the Left/Right quick-heals) live while it owns the keyboard.
        /// Default false; the conversation view overrides this to true. Off for menus, where the heal arrows
        /// and bare letters serve navigation/type-ahead instead.</summary>
        public virtual bool WantsStatusKeys => false;

        /// <summary>Extra key-help lines for keys this screen handles itself, outside the input registry
        /// (the dialogue viewer's number-row jump), composed and ready to speak. The key-help screen
        /// reads them ahead of the registry's own list. Empty by default.</summary>
        public virtual IEnumerable<string> KeyHelpLines() => System.Array.Empty<string>();

        /// <summary>Called every frame while this screen stands (the view is unchanged). A rich screen
        /// overrides it to refresh dynamic content in place - rebuilding a sub-tree when the game state it
        /// mirrors changes (e.g. an options tab switch) and re-homing the navigator. Returns whether focus
        /// was re-homed this frame, so the ScreenManager re-announces the landing once. It must NOT announce
        /// itself - the ScreenManager owns the post-update announce so the read is single and reflects the
        /// rebuilt tree. The default does nothing and returns false.</summary>
        public virtual bool OnUpdate(IModHost host, TraditionalNavigator nav) => false;
    }
}
