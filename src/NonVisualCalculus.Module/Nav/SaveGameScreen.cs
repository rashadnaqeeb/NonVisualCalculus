using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using Sunshine.Views;
using UnityEngine.UI;
using Table = NonVisualCalculus.Core.UI.Nav.Table;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The save menu: the save table whose primary action saves over the focused slot. The first row is the
    /// game's create-new slot (announced "new save"); activating its Save cell creates a new save with the
    /// game's generated default name, and activating an existing save's Save cell opens the game's overwrite
    /// confirmation. The shared save button routes create vs overwrite from the selected slot itself. Only
    /// the create-new slot gets a Rename column (to type the new save's name); existing saves cannot be
    /// renamed - the game does not persist an edit to an existing save's name - so they get no Rename cell.
    /// </summary>
    public sealed class SaveGameScreen : SaveLoadTableScreen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.SAVE;
        public override string ScreenName => Strings.ScreenSave;

        protected override Button PrimaryButton(SaveLoadController ctrl) => ctrl.saveButton;

        // The create-new slot gets a third Rename column (right of Save and Delete) so the player can name
        // the save before creating it; existing saves and the load menu do not (rename does not persist on
        // an existing save). The table allows this shorter-elsewhere row: out-of-range cells read as empty.
        protected override void BuildRow(IModHost host, Table table, SaveGameListEntry entry, Selectable row, Button primary, Button delete)
        {
            if (entry.IsNewSave)
                table.AddRow(
                    new SaveActionCell(entry, row, primary),
                    new DeleteCell(entry, row, delete),
                    new RenameCell(host, entry, row));
            else
                table.AddRow(
                    new SaveActionCell(entry, row, primary),
                    new DeleteCell(entry, row, delete));
        }
    }
}
