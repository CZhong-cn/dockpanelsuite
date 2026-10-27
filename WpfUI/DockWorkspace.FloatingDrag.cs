using System;
using System.Windows;
using System.Windows.Media;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private sealed class FloatingDrag
        {
            public DockItem Item;
            public DockItemHeader Header;
            public Window Window;
            public Point Start, Last, Origin;
            public double Opacity;
            public bool Moving;
        }
        private FloatingDrag _floatingDrag;

        // Capture the title so movement continues outside the original window bounds.
        internal bool BeginFloatingDrag(DockItem item, DockItemHeader header, Point screenPoint)
        {
            Dispatcher.VerifyAccess();
            if (item?.Owner != this || item.State != DockState.Float || header.Item != item ||
                !_floats.TryGetValue(item, out var window) || !window.IsVisible || window.WindowState != WindowState.Normal) return false;
            EndFloatingDrag(null);
            if (!header.CaptureMouse()) return false;
            _floatingDrag = new FloatingDrag
            {
                Item = item, Header = header, Window = window, Start = screenPoint, Last = screenPoint,
                Origin = new Point(window.Left, window.Top), Opacity = window.Opacity
            };
            return true;
        }

        internal void UpdateFloatingDrag(Point screenPoint)
        {
            Dispatcher.VerifyAccess();
            var drag = _floatingDrag;
            if (drag == null) return;
            var transform = PresentationSource.FromVisual(drag.Window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            if (!drag.Moving)
            {
                var distance = transform.Transform(screenPoint - drag.Start);
                if (Math.Abs(distance.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(distance.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                drag.Moving = true;
                // Keep guides on the owner visible beneath the moving floating pane.
                drag.Window.SetCurrentValue(OpacityProperty, Math.Min(drag.Opacity, .65));
            }
            var delta = transform.Transform(screenPoint - drag.Last);
            drag.Last = screenPoint;
            drag.Window.Left += delta.X;
            drag.Window.Top += delta.Y;
            if (IsLoaded && PresentationSource.FromVisual(_grid) != null)
                UpdateDockDrag(drag.Item, _grid.PointFromScreen(screenPoint));
            else ClearDropPreview();
        }

        internal void EndFloatingDrag(Point? screenPoint, bool cancel = false)
        {
            Dispatcher.VerifyAccess();
            var drag = _floatingDrag;
            if (drag == null) return;
            _floatingDrag = null;
            if (drag.Header.IsMouseCaptured) drag.Header.ReleaseMouseCapture();
            drag.Window.SetCurrentValue(OpacityProperty, drag.Opacity);
            try
            {
                if (cancel)
                {
                    drag.Window.Left = drag.Origin.X;
                    drag.Window.Top = drag.Origin.Y;
                }
                else if (drag.Moving && screenPoint.HasValue && IsLoaded && PresentationSource.FromVisual(_grid) != null)
                {
                    var point = _grid.PointFromScreen(screenPoint.Value);
                    var direction = UpdateDockDrag(drag.Item, point);
                    var before = HeaderItemFromSource(_grid.InputHitTest(point) as DependencyObject);
                    // Moving across the workspace must remain a move until an explicit docking target is chosen.
                    if (direction.HasValue && (IsDockGuideHit(point) || before != null))
                        CompleteDockDrag(drag.Item, point, before);
                }
            }
            finally
            {
                ClearDropPreview();
                // A render already follows an API mutation; do not recapture its old tracks over new sizes.
                if (!_rendering)
                {
                    CaptureGeometry();
                    if (drag.Moving) LayoutChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
}
