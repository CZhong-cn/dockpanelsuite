using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private DockGuideOverlay _dockGuides;
        private DockTabGroup _dragGroup;
        private bool _dragOuter;
        private static readonly DependencyProperty DockHeaderItemProperty = DependencyProperty.RegisterAttached(
            "DockHeaderItem", typeof(DockItem), typeof(DockWorkspace), new PropertyMetadata(null));

        internal Rect GroupBounds(DockItem item)
        {
            var group = FindGroup(item);
            var view = group != null && _tabs.TryGetValue(group, out var tabs) ? tabs : _tabs.Values.FirstOrDefault();
            return view == null ? new Rect(0, 0, _grid.ActualWidth, _grid.ActualHeight) : ViewBounds(view);
        }

        private Rect ViewBounds(FrameworkElement view)
        {
            var bounds = new Rect(view.TranslatePoint(new Point(), _grid), new Size(view.ActualWidth, view.ActualHeight));
            bounds.Intersect(new Rect(0, 0, _grid.ActualWidth, _grid.ActualHeight));
            return bounds;
        }

        internal DockState? UpdateDockDrag(DockItem item, Point point)
        {
            Dispatcher.VerifyAccess();
            if (item?.Owner != this || _grid == null || !InsideDockSurface(point)) { ClearDropPreview(); return null; }
            var size = new Size(_grid.ActualWidth, _grid.ActualHeight);
            var outer = (_dockGuides?.Targets ?? DockGuideGeometry.Create(size.Width, size.Height, GuideMetrics)).Where(target => target.IsOuter && target.Contains(point.X, point.Y)).ToArray();
            var hovered = _tabs.FirstOrDefault(pair => ViewBounds(pair.Value).Contains(point));
            if (hovered.Key != null && outer.Length == 0) _dragGroup = hovered.Key;
            if (_dragGroup == null || !_tabs.ContainsKey(_dragGroup)) _dragGroup = hovered.Key ?? _tabs.Keys.First();
            var bounds = ViewBounds(_tabs[_dragGroup]);
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                var visible = _tabs.FirstOrDefault(pair => { var rect = ViewBounds(pair.Value); return !rect.IsEmpty && rect.Width > 0 && rect.Height > 0; });
                if (visible.Key == null) { ClearDropPreview(); return null; }
                _dragGroup = visible.Key; bounds = ViewBounds(visible.Value);
            }
            if (_dockGuides == null)
            {
                _dockGuides = new DockGuideOverlay();
                Grid.SetRowSpan(_dockGuides, 5); Grid.SetColumnSpan(_dockGuides, 5);
                Panel.SetZIndex(_dockGuides, 30); _grid.Children.Add(_dockGuides);
            }
            var groupState = (DockState)_tabs[_dragGroup].Tag;
            _dockGuides.Update(point, size, bounds, target => target.IsOuter
                ? AllowsPlacement(item, target.State)
                : AllowsPlacement(item, groupState) && (target.State == DockState.Document || item.CanDockToSides));
            if (_dockGuides.IsBlockedTarget(point) || !AllowsPlacement(item, groupState))
            {
                if (_dropPreview != null) _dropPreview.Visibility = Visibility.Collapsed;
                return null;
            }
            var targetGuide = _dockGuides.HoveredTarget;
            _dragOuter = targetGuide?.IsOuter == true;
            var direction = targetGuide?.State ?? DockState.Document;
            if (_dropPreview == null)
            {
                _dropPreview = new Border();
                _dropPreview.SetResourceReference(StyleProperty, "Dps.DropPreview");
                Grid.SetRowSpan(_dropPreview, 5); Grid.SetColumnSpan(_dropPreview, 5);
                Panel.SetZIndex(_dropPreview, 20); _grid.Children.Add(_dropPreview);
            }
            var preview = _dragOuter ? RegionPreviewBounds(direction, item) : SplitPreviewBounds(bounds, direction, item);
            _dropPreview.Tag = direction; _dropPreview.Visibility = Visibility.Visible;
            var inset = (Thickness)FindResource("Dps.DropPreviewInset");
            _dropPreview.Width = Math.Max(0, preview.Width - inset.Left - inset.Right); _dropPreview.Height = Math.Max(0, preview.Height - inset.Top - inset.Bottom);
            _dropPreview.Margin = new Thickness(preview.X + inset.Left, preview.Y + inset.Top, 0, 0);
            return direction;
        }

        private Rect SplitPreviewBounds(Rect bounds, DockState direction, DockItem dragged)
        {
            if (!_dragGroup.ItemIds.Any(id => id != dragged.Id && _items.Any(item => item.Id == id && Docked(item.State)))) return bounds;
            double width = Math.Max(0, (bounds.Width - SplitterSize) / 2), height = Math.Max(0, (bounds.Height - SplitterSize) / 2);
            switch (direction)
            {
                case DockState.Left: return new Rect(bounds.Left, bounds.Top, width, bounds.Height);
                case DockState.Right: return new Rect(bounds.Right - width, bounds.Top, width, bounds.Height);
                case DockState.Top: return new Rect(bounds.Left, bounds.Top, bounds.Width, height);
                case DockState.Bottom: return new Rect(bounds.Left, bounds.Bottom - height, bounds.Width, height);
                default: return bounds;
            }
        }

        internal bool CompleteDockDrag(DockItem item, Point point, DockItem before = null)
        {
            var direction = UpdateDockDrag(item, point);
            var group = _dragGroup; bool outer = _dragOuter;
            bool reorder = before?.Owner == this && Docked(before.State) && !IsDockGuideHit(point);
            ClearDropPreview();
            if (!direction.HasValue) return false;
            if (reorder)
            {
                if (!AllowsPlacement(item, before.State)) return false;
                // Floating panes retain their saved group but must first return to its visible dock state.
                if (item.State != before.State || FindGroup(item) != FindGroup(before)) Dock(item, before);
                MoveBefore(item, before); Activate(item);
            }
            else if (outer) { Show(item, direction.Value); FocusContent(item); }
            else
            {
                var target = group.ItemIds.Select(id => _items.FirstOrDefault(candidate => candidate.Id == id && candidate != item && Docked(candidate.State))).FirstOrDefault(candidate => candidate != null);
                if (target != null) Dock(item, target, ToPosition(direction.Value));
                else { Show(item, (DockState)_tabs[group].Tag); FocusContent(item); }
            }
            return true;
        }

        private static DockPosition ToPosition(DockState direction)
        {
            switch (direction)
            {
                case DockState.Left: return DockPosition.Left;
                case DockState.Right: return DockPosition.Right;
                case DockState.Top: return DockPosition.Top;
                case DockState.Bottom: return DockPosition.Bottom;
                default: return DockPosition.Center;
            }
        }

        private bool InsideDockSurface(Point point) => _grid.ActualWidth > 0 && _grid.ActualHeight > 0 &&
            point.X >= 0 && point.Y >= 0 && point.X <= _grid.ActualWidth && point.Y <= _grid.ActualHeight;
        private bool IsDockGuideHit(Point point) => _dockGuides != null && _dockGuides.Targets.Any(target => target.Contains(point.X, point.Y));

        private void OnDragOver(object sender, DragEventArgs args)
        {
            var item = GetDraggedItem(args);
            if (item == null) { ClearDropPreview(); return; }
            var point = args.GetPosition(_grid);
            var state = UpdateDockDrag(item, point);
            if (HeaderItemFromSource(args.OriginalSource as DependencyObject) != null && !IsDockGuideHit(point) && _dropPreview != null)
                _dropPreview.Visibility = Visibility.Collapsed;
            args.Effects = state.HasValue ? DragDropEffects.Move : DragDropEffects.None; args.Handled = true;
        }

        private DockItem HeaderItemFromSource(DependencyObject source)
        {
            while (source != null && source != _grid)
            {
                if (source.GetValue(DockHeaderItemProperty) is DockItem item) return item;
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            return null;
        }
        private void OnDragLeave(object sender, DragEventArgs args)
        { if (!InsideDockSurface(args.GetPosition(_grid))) ClearDropPreview(); }

        internal void ClearDropPreview()
        {
            if (_dropPreview != null) _grid?.Children.Remove(_dropPreview);
            if (_dockGuides != null) _grid?.Children.Remove(_dockGuides);
            _dropPreview = null; _dockGuides = null; _dragGroup = null; _dragOuter = false;
        }
    }
}
