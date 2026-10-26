using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private readonly Dictionary<DockState, DockLayoutNode> _roots = new Dictionary<DockState, DockLayoutNode>();
        private readonly Dictionary<DockSplit, Grid> _splitViews = new Dictionary<DockSplit, Grid>();

        /// <summary>Merge into a target tab group, or split that group on the requested side.</summary>
        public void Dock(DockItem item, DockItem target, DockPosition position = DockPosition.Center, double proportion = .5)
        {
            RequireItem(item); RequireItem(target);
            if (item == target) throw new ArgumentException("Choose a different target item.", nameof(target));
            if (!Enum.IsDefined(typeof(DockPosition), position)) throw new ArgumentOutOfRangeException(nameof(position));
            if (!Finite(proportion) || proportion < .05 || proportion > .95) throw new ArgumentOutOfRangeException(nameof(proportion));
            if (!Docked(target.State)) throw new ArgumentException("The target must be docked and visible.", nameof(target));
            CaptureGeometry();
            var group = FindGroup(target);
            var region = target.State;
            if (position != DockPosition.Center && (DepthOf(_roots[region], group) >= 32 || _roots.Values.Sum(NodeCount) + 2 > 1024))
                throw new InvalidOperationException("The layout tree limit has been reached.");
            RemoveFromGroups(item.Id);
            if (position == DockPosition.Center) group.ItemIds.Add(item.Id);
            else
            {
                var added = new DockTabGroup { ItemIds = new List<string> { item.Id }, SelectedId = item.Id };
                bool first = position == DockPosition.Left || position == DockPosition.Top;
                var split = new DockSplit
                {
                    Orientation = position == DockPosition.Left || position == DockPosition.Right ? DockSplitOrientation.Horizontal : DockSplitOrientation.Vertical,
                    Ratio = first ? proportion : 1 - proportion,
                    First = first ? added : (DockLayoutNode)group,
                    Second = first ? (DockLayoutNode)group : added
                };
                _roots[region] = ReplaceNode(_roots[region], group, split);
                group = added;
            }
            item.SetState(region);
            group.SelectedId = item.Id;
            _selected[region] = item.Id;
            _autoHideItem = null;
            SetActiveItem(item);
            Render(); FocusContent(item);
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        private static int NodeCount(DockLayoutNode node) => node is DockSplit split ? 1 + NodeCount(split.First) + NodeCount(split.Second) : 1;

        private static int DepthOf(DockLayoutNode node, DockTabGroup target)
        {
            if (node == target) return 0;
            if (!(node is DockSplit split)) return -1;
            int first = DepthOf(split.First, target), second = DepthOf(split.Second, target);
            return first < 0 && second < 0 ? -1 : 1 + Math.Max(first, second);
        }
        private static bool Docked(DockState state) => state == DockState.Document || IsEdge(state);

        private static IEnumerable<DockTabGroup> Groups(DockLayoutNode node)
        {
            if (node is DockTabGroup group) yield return group;
            else if (node is DockSplit split)
            {
                foreach (var child in Groups(split.First)) yield return child;
                foreach (var child in Groups(split.Second)) yield return child;
            }
        }

        private DockTabGroup FindGroup(DockItem item) => _roots.Values.SelectMany(Groups).FirstOrDefault(group => group.ItemIds.Contains(item.Id));

        private DockTabGroup DefaultGroup(DockState state)
        {
            if (!_roots.TryGetValue(state, out var root))
            {
                root = new DockTabGroup(); _roots[state] = root;
            }
            _selected.TryGetValue(state, out var selected);
            return Groups(root).FirstOrDefault(group => group.ItemIds.Contains(selected ?? "")) ?? Groups(root).First();
        }

        private void EnsureItemGroup(DockItem item, DockState state)
        {
            var region = Docked(state) ? state : item.LastDockState;
            var existing = FindGroup(item);
            if (existing != null && _roots.TryGetValue(region, out var root) && Groups(root).Contains(existing)) return;
            RemoveFromGroups(item.Id);
            DefaultGroup(region).ItemIds.Add(item.Id);
        }

        private void SelectInGroup(DockItem item)
        {
            if (Docked(item.State) && FindGroup(item) is DockTabGroup group) group.SelectedId = item.Id;
        }

        private void RemoveFromGroups(string id)
        {
            foreach (var state in _roots.Keys.ToArray())
            {
                _roots[state] = RemoveFromNode(_roots[state], id);
                if (_roots[state] == null) _roots.Remove(state);
            }
        }

        private static DockLayoutNode RemoveFromNode(DockLayoutNode node, string id)
        {
            if (node is DockTabGroup group)
            {
                group.ItemIds.Remove(id);
                if (group.SelectedId == id) group.SelectedId = null;
                return group.ItemIds.Count == 0 ? null : group;
            }
            var split = (DockSplit)node;
            split.First = RemoveFromNode(split.First, id);
            split.Second = RemoveFromNode(split.Second, id);
            return split.First == null ? split.Second : split.Second == null ? split.First : split;
        }

        private static DockLayoutNode ReplaceNode(DockLayoutNode root, DockLayoutNode oldNode, DockLayoutNode replacement)
        {
            if (root == oldNode) return replacement;
            if (root is DockSplit split)
            {
                split.First = ReplaceNode(split.First, oldNode, replacement);
                split.Second = ReplaceNode(split.Second, oldNode, replacement);
            }
            return root;
        }

        private void AddZone(DockState state, int row, int column, int span = 1)
        {
            bool visible = state == DockState.Document || _items.Any(item => item.State == state);
            if (!visible)
            {
                if (state == DockState.Left || state == DockState.Right) _grid.ColumnDefinitions[column].Width = new GridLength(0);
                else _grid.RowDefinitions[row].Height = new GridLength(0);
                return;
            }
            DefaultGroup(state);
            if (state == DockState.Left || state == DockState.Right) _grid.ColumnDefinitions[column].SetResourceReference(ColumnDefinition.MinWidthProperty, "Dps.PaneMinWidth");
            else if (state != DockState.Document) _grid.RowDefinitions[row].SetResourceReference(RowDefinition.MinHeightProperty, "Dps.PaneMinHeight");
            var view = RenderNode(_roots[state], state, true);
            Place(view, row, column, span);
        }

        private FrameworkElement RenderNode(DockLayoutNode node, DockState state, bool allowEmpty = false)
        {
            if (node is DockTabGroup group)
            {
                var items = group.ItemIds.Select(id => _items.FirstOrDefault(item => item.Id == id && item.State == state)).Where(item => item != null).ToList();
                return items.Count == 0 && !allowEmpty ? null : CreateTabs(group, state, items);
            }
            var split = (DockSplit)node;
            var first = RenderNode(split.First, state);
            var second = RenderNode(split.Second, state);
            if (first == null && second == null) return allowEmpty ? CreateTabs(Groups(node).First(), state, new List<DockItem>()) : null;
            if (first == null) return second;
            if (second == null) return first;
            bool horizontal = split.Orientation == DockSplitOrientation.Horizontal;
            var grid = new Grid { Tag = split };
            if (horizontal)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(split.Ratio, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions[0].SetResourceReference(ColumnDefinition.MinWidthProperty, "Dps.PaneMinWidth");
                grid.ColumnDefinitions[1].SetResourceReference(ColumnDefinition.WidthProperty, "Dps.SplitterLength");
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - split.Ratio, GridUnitType.Star) });
                grid.ColumnDefinitions[2].SetResourceReference(ColumnDefinition.MinWidthProperty, "Dps.PaneMinWidth");
                Grid.SetColumn(second, 2);
            }
            else
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(split.Ratio, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition());
                grid.RowDefinitions[0].SetResourceReference(RowDefinition.MinHeightProperty, "Dps.PaneMinHeight");
                grid.RowDefinitions[1].SetResourceReference(RowDefinition.HeightProperty, "Dps.SplitterLength");
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - split.Ratio, GridUnitType.Star) });
                grid.RowDefinitions[2].SetResourceReference(RowDefinition.MinHeightProperty, "Dps.PaneMinHeight");
                Grid.SetRow(second, 2);
            }
            var divider = new GridSplitter
            {
                ResizeDirection = horizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows
            };
            divider.SetResourceReference(StyleProperty, "Dps.Splitter");
            System.Windows.Automation.AutomationProperties.SetName(divider, horizontal ? "调整左右分组 / Resize columns" : "调整上下分组 / Resize rows");
            if (horizontal) Grid.SetColumn(divider, 1); else Grid.SetRow(divider, 1);
            divider.DragCompleted += (sender, args) => { CaptureGeometry(); LayoutChanged?.Invoke(this, EventArgs.Empty); };
            grid.Children.Add(first); grid.Children.Add(divider); grid.Children.Add(second);
            _splitViews.Add(split, grid);
            return grid;
        }

        private void CaptureSplitRatios()
        {
            foreach (var pair in _splitViews)
            {
                bool horizontal = pair.Key.Orientation == DockSplitOrientation.Horizontal;
                double first = horizontal ? pair.Value.ColumnDefinitions[0].ActualWidth : pair.Value.RowDefinitions[0].ActualHeight;
                double second = horizontal ? pair.Value.ColumnDefinitions[2].ActualWidth : pair.Value.RowDefinitions[2].ActualHeight;
                if (first > 0 && second > 0) pair.Key.Ratio = Math.Max(.05, Math.Min(.95, first / (first + second)));
            }
        }
    }
}
