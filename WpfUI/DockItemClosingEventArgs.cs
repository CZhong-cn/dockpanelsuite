using System.ComponentModel;

namespace DockPanelSuite.Wpf
{
    public sealed class DockItemClosingEventArgs : CancelEventArgs
    {
        public DockItemClosingEventArgs(DockItem item) { Item = item; }
        public DockItem Item { get; }
    }
}
