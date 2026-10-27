using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace DockPanelSuite.Wpf
{
    /// <summary>Positions XAML guide chrome using the same metrics as the drop hit test.</summary>
    internal sealed class DockGuideOverlay : Canvas
    {
        private readonly List<DockGuideTile> _tiles = new List<DockGuideTile>();
        private Size _size;
        private IReadOnlyList<DockGuideTarget> _allTargets;
        private DockGuideMetrics _metrics;
        internal Rect TargetBounds { get; private set; }
        internal DockGuideTarget? HoveredTarget { get; private set; }
        internal IReadOnlyList<DockGuideTarget> Targets { get; private set; }

        internal DockGuideOverlay() => SetResourceReference(StyleProperty, "Dps.GuideOverlay");

        internal bool IsBlockedTarget(Point point) => _allTargets != null &&
            _allTargets.Any(target => target.Contains(point.X, point.Y) && !Targets.Contains(target));

        internal void Update(Point point, Size size, Rect targetBounds, Func<DockGuideTarget, bool> allowsTarget = null)
        {
            var metrics = (DockGuideMetrics)FindResource("Dps.GuideMetrics");
            if (Targets == null || _size != size || TargetBounds != targetBounds || !metrics.Equals(_metrics)) Build(size, targetBounds, metrics);
            Targets = _allTargets.Where(target => allowsTarget == null || allowsTarget(target)).ToArray();
            HoveredTarget = null;
            for (int index = 0; index < _allTargets.Count; index++)
            {
                var target = _allTargets[index];
                bool allowed = Targets.Contains(target);
                _tiles[index].Visibility = allowed ? Visibility.Visible : Visibility.Collapsed;
                bool hovered = allowed && target.Contains(point.X, point.Y);
                if (hovered) HoveredTarget = target;
                _tiles[index].IsHighlighted = hovered;
            }
        }

        private void Build(Size size, Rect targetBounds, DockGuideMetrics metrics)
        {
            _size = size; TargetBounds = targetBounds; _metrics = metrics.Copy();
            _allTargets = DockGuideGeometry.Create(size.Width, size.Height, targetBounds.X, targetBounds.Y, targetBounds.Width, targetBounds.Height, metrics);
            Targets = _allTargets;
            Children.Clear(); _tiles.Clear();
            double plateSize = metrics.PlateSize * DockGuideGeometry.Scale(targetBounds.Width, targetBounds.Height, metrics);
            var plate = new ContentControl { Width = plateSize, Height = plateSize };
            plate.SetResourceReference(StyleProperty, "Dps.GuidePlateStyle");
            SetLeft(plate, targetBounds.X + (targetBounds.Width - plateSize) / 2);
            SetTop(plate, targetBounds.Y + (targetBounds.Height - plateSize) / 2);
            Children.Add(plate);
            foreach (var target in Targets)
            {
                var tile = new DockGuideTile { State = target.State, Tag = target.State, Width = target.Size, Height = target.Size };
                tile.SetResourceReference(StyleProperty, "Dps.GuideTile");
                AutomationProperties.SetName(tile, target.State.ToString());
                SetLeft(tile, target.X); SetTop(tile, target.Y);
                Children.Add(tile); _tiles.Add(tile);
            }
        }
    }
}
