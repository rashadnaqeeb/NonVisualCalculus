using NonVisualCalculus.Core.Strings;
using Sunshine.Views;
using UnityEngine.UI;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>The load menu: the save table whose primary action loads the focused save.</summary>
    public sealed class LoadGameScreen : SaveLoadTableScreen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.LOAD;
        public override string ScreenName => Strings.ScreenLoad;

        protected override Button PrimaryButton(SaveLoadController ctrl) => ctrl.LoadButton;
    }
}
