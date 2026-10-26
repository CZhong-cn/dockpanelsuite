using System;
using System.Collections.Generic;

namespace DockPanelSuite.Wpf
{
    // Shared DIP geometry: the rendered tiles and drop hit tests use these same bounds.
    internal readonly struct DockGuideTarget
    {
        internal DockGuideTarget(DockState state, double x, double y, double size, bool outer = false)
        { State = state; X = x; Y = y; Size = size; IsOuter = outer; }
        internal DockState State { get; }
        internal double X { get; }
        internal double Y { get; }
        internal double Size { get; }
        internal bool IsOuter { get; }
        internal bool Contains(double x, double y) => x >= X && y >= Y && x < X + Size && y < Y + Size;
    }

    internal static class DockGuideGeometry
    {
        internal static double Scale(double width, double height, DockGuideMetrics metrics = null) => Math.Min(1, Math.Min(width, height) / (metrics ?? new DockGuideMetrics()).ScaleThreshold);

        internal static IReadOnlyList<DockGuideTarget> Create(double width, double height, DockGuideMetrics metrics = null)
            => Create(width, height, 0, 0, width, height, metrics);

        internal static IReadOnlyList<DockGuideTarget> Create(double width, double height, double targetX, double targetY, double targetWidth, double targetHeight, DockGuideMetrics metrics = null)
        {
            if (double.IsNaN(width) || double.IsNaN(height) || double.IsInfinity(width) || double.IsInfinity(height) || width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (double.IsNaN(targetX) || double.IsNaN(targetY) || double.IsNaN(targetWidth) || double.IsNaN(targetHeight) ||
                double.IsInfinity(targetX) || double.IsInfinity(targetY) || double.IsInfinity(targetWidth) || double.IsInfinity(targetHeight) ||
                targetX < 0 || targetY < 0 || targetWidth <= 0 || targetHeight <= 0 || targetX + targetWidth > width + .000001 || targetY + targetHeight > height + .000001)
                throw new ArgumentOutOfRangeException(nameof(targetWidth));
            metrics = metrics ?? new DockGuideMetrics();
            metrics.Validate();
            double scale = Scale(targetWidth, targetHeight, metrics), size = metrics.TileSize * scale, step = metrics.Step * scale;
            double x = targetX + (targetWidth - size) / 2, y = targetY + (targetHeight - size) / 2;
            var targets = new List<DockGuideTarget>
            {
                new DockGuideTarget(DockState.Left, x - step, y, size),
                new DockGuideTarget(DockState.Top, x, y - step, size),
                new DockGuideTarget(DockState.Document, x, y, size),
                new DockGuideTarget(DockState.Right, x + step, y, size),
                new DockGuideTarget(DockState.Bottom, x, y + step, size)
            };
            // Keep the cross usable in small hosts without overlapping outer guides.
            if (width >= metrics.ScaleThreshold && height >= metrics.ScaleThreshold)
            {
                double margin = metrics.OuterMargin, outerSize = metrics.TileSize;
                double outerX = (width - outerSize) / 2, outerY = (height - outerSize) / 2;
                targets.Add(new DockGuideTarget(DockState.Left, margin, outerY, outerSize, true));
                targets.Add(new DockGuideTarget(DockState.Top, outerX, margin, outerSize, true));
                targets.Add(new DockGuideTarget(DockState.Right, width - margin - outerSize, outerY, outerSize, true));
                targets.Add(new DockGuideTarget(DockState.Bottom, outerX, height - margin - outerSize, outerSize, true));
            }
            // In very narrow panes an outer tile can cover the local cross. Keep
            // the full local cross usable and omit only the conflicting outer tile.
            double left = targetX + targetWidth / 2 - metrics.PlateSize / 2 * scale, top = targetY + targetHeight / 2 - metrics.PlateSize / 2 * scale;
            targets.RemoveAll(tile => tile.IsOuter && tile.X < left + metrics.PlateSize * scale && tile.X + tile.Size > left &&
                tile.Y < top + metrics.PlateSize * scale && tile.Y + tile.Size > top);
            return targets;
        }

        internal static DockGuideTarget? HitTest(double x, double y, double width, double height, DockGuideMetrics metrics = null)
        {
            foreach (var target in Create(width, height, metrics))
                if (target.Contains(x, y)) return target;
            return null;
        }
    }
}
