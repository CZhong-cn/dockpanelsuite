using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace DockPanelSuite.Wpf
{
    public enum DockPosition { Center, Left, Right, Top, Bottom }
    public enum DockSplitOrientation { Horizontal, Vertical }

    [XmlInclude(typeof(DockTabGroup)), XmlInclude(typeof(DockSplit))]
    public abstract class DockLayoutNode { }

    public sealed class DockTabGroup : DockLayoutNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public List<string> ItemIds { get; set; } = new List<string>();
        public string SelectedId { get; set; }
    }

    public sealed class DockSplit : DockLayoutNode
    {
        public DockSplitOrientation Orientation { get; set; }
        public double Ratio { get; set; } = .5;
        public DockLayoutNode First { get; set; }
        public DockLayoutNode Second { get; set; }
    }

    public sealed class DockRegionLayout
    {
        public DockState State { get; set; }
        public DockLayoutNode Root { get; set; }
    }
}
