using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private readonly List<DockState> _regionOrder = new List<DockState>();
        private static readonly DockState[] LegacyRegionOrder = { DockState.Top, DockState.Bottom, DockState.Left, DockState.Right };

        private void RememberRegion(DockState state)
        {
            if (IsEdge(state) && !_regionOrder.Contains(state)) _regionOrder.Add(state);
        }

        private sealed class RegionArea
        {
            public int Row, Column, RowSpan = 1, ColumnSpan = 1;
        }

        // Earlier edges consume the full remaining rectangle; later edges occupy its interior.
        // Retain the five tracks on each axis so pane sizes and splitter resizing stay independent.
        private Dictionary<DockState, RegionArea> RegionAreas()
        {
            var result = new Dictionary<DockState, RegionArea>();
            int top = 0, bottom = 4, left = 0, right = 4;
            foreach (var state in _regionOrder.Concat(LegacyRegionOrder.Where(state => !_regionOrder.Contains(state))))
            {
                bool horizontal = state == DockState.Top || state == DockState.Bottom;
                result[state] = new RegionArea
                {
                    Row = horizontal ? (state == DockState.Top ? 0 : 4) : top,
                    Column = horizontal ? left : (state == DockState.Left ? 0 : 4),
                    RowSpan = horizontal ? 1 : bottom - top + 1,
                    ColumnSpan = horizontal ? right - left + 1 : 1
                };
                if (state == DockState.Top) top = 2;
                else if (state == DockState.Bottom) bottom = 2;
                else if (state == DockState.Left) left = 2;
                else right = 2;
            }
            result[DockState.Document] = new RegionArea { Row = 2, Column = 2 };
            return result;
        }

        private static void ValidateRegionOrder(DockLayout layout)
        {
            if (layout.Version < 5) return;
            if (layout.RegionOrder == null || layout.RegionOrder.Count > 4 ||
                layout.RegionOrder.Any(state => !IsEdge(state)) || layout.RegionOrder.Distinct().Count() != layout.RegionOrder.Count)
                throw new InvalidDataException("Invalid outer region order.");
            var used = layout.Regions.Select(region => region.State)
                .Concat(layout.Items.Select(item => Docked(item.State) ? item.State : item.LastDockState));
            if (used.Any(state => IsEdge(state) && !layout.RegionOrder.Contains(state)))
                throw new InvalidDataException("A dock region is missing from the outer region order.");
        }

        private void RestoreRegionOrder(DockLayout layout)
        {
            _regionOrder.Clear();
            // Older snapshots always had top/bottom outside left/right, regardless of item order.
            _regionOrder.AddRange(layout.Version < 5 ? (IEnumerable<DockState>)LegacyRegionOrder : layout.RegionOrder);
        }

        private Rect RegionPreviewBounds(DockState state, DockItem dragged)
        {
            var visible = LegacyRegionOrder.ToDictionary(edge => edge,
                edge => edge == state || _items.Any(item => item != dragged && item.State == edge));
            double left = visible[DockState.Left] ? System.Math.Max(Metric("Dps.PaneMinWidth"), _sizes.LeftWidth) : 0;
            double right = visible[DockState.Right] ? System.Math.Max(Metric("Dps.PaneMinWidth"), _sizes.RightWidth) : 0;
            double top = visible[DockState.Top] ? System.Math.Max(Metric("Dps.PaneMinHeight"), _sizes.TopHeight) : 0;
            double bottom = visible[DockState.Bottom] ? System.Math.Max(Metric("Dps.PaneMinHeight"), _sizes.BottomHeight) : 0;
            var widths = new[] { left, visible[DockState.Left] ? SplitterSize : 0, 0, visible[DockState.Right] ? SplitterSize : 0, right };
            var heights = new[] { top, visible[DockState.Top] ? SplitterSize : 0, 0, visible[DockState.Bottom] ? SplitterSize : 0, bottom };
            widths[2] = System.Math.Max(Metric("Dps.DocumentMinWidth"), _grid.ActualWidth - widths.Sum());
            heights[2] = System.Math.Max(Metric("Dps.DocumentMinHeight"), _grid.ActualHeight - heights.Sum());
            var area = RegionAreas()[state];
            var bounds = new Rect(widths.Take(area.Column).Sum(), heights.Take(area.Row).Sum(),
                widths.Skip(area.Column).Take(area.ColumnSpan).Sum(), heights.Skip(area.Row).Take(area.RowSpan).Sum());
            bounds.Intersect(new Rect(0, 0, _grid.ActualWidth, _grid.ActualHeight));
            return bounds.IsEmpty ? new Rect() : bounds;
        }
    }
}
