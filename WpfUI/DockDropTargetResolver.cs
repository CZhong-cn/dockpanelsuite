using System;

namespace DockPanelSuite.Wpf
{
    /// <summary>Platform-independent hit testing shared by previews and actual drops.</summary>
    public static class DockDropTargetResolver
    {
        public static DockState Resolve(double x, double y, double width, double height)
            => Resolve(x, y, width, height, new DockGuideMetrics());

        public static DockState Resolve(double x, double y, double width, double height, DockGuideMetrics metrics)
        {
            if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Finite coordinates and positive dimensions are required.");
            var guide = DockGuideGeometry.HitTest(x, y, width, height, metrics ?? throw new ArgumentNullException(nameof(metrics)));
            if (guide.HasValue) return guide.Value.State;
            x /= width;
            y /= height;
            return x < .18 ? DockState.Left : x > .82 ? DockState.Right :
                y < .18 ? DockState.Top : y > .82 ? DockState.Bottom : DockState.Document;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
