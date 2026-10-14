using System.Collections.Generic;

namespace DockPanelSuite.Wpf
{
    // Only identities and geometry are serialized; arbitrary WPF content is never deserialized.
    public sealed class DockLayout
    {
        public int Version { get; set; } = 1;
        public List<DockPlacement> Items { get; set; } = new List<DockPlacement>();
        public List<string> SelectedIds { get; set; } = new List<string>();
        public double LeftWidth { get; set; } = 220;
        public double RightWidth { get; set; } = 220;
        public double TopHeight { get; set; } = 160;
        public double BottomHeight { get; set; } = 160;
    }

    public sealed class DockPlacement
    {
        public string Id { get; set; }
        public DockState State { get; set; }
        public DockState LastDockState { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
