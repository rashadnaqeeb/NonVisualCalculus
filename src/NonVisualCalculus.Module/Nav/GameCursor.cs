using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// Syncs the game's own cursor (NavigationManager's selection) to our focus, so the game's selection
    /// follows ours as we navigate. Centralized here so the one focus-follow call - and its guard for a
    /// NavigationManager that is not up yet - lives in one place rather than in each leaf element.
    /// </summary>
    internal static class GameCursor
    {
        public static void Follow(Selectable selectable)
        {
            NavigationManager nav = NavigationManager.Singleton;
            if (nav != null && selectable != null)
                nav.Select(selectable);
        }
    }
}
