using System;
using DiscoAccess.Core.UI.Nav;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// A <see cref="Container"/> whose label is produced live by a delegate (never cached), for a grouping
    /// whose name tracks game state. The inventory's item list uses it so the list announces the active
    /// category (Tools, Clothes, ...) the filter is set to, re-read each time focus enters it.
    /// </summary>
    internal sealed class LiveLabelContainer : Container
    {
        private readonly Func<string> _label;

        public LiveLabelContainer(ContainerShape shape, Func<string> label) : base(shape) => _label = label;

        public override string Label => _label();
    }
}
