using System.Windows;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private bool _initialSizingApplied;
        internal DockGuideMetrics GuideMetrics => (DockGuideMetrics)FindResource("Dps.GuideMetrics");
        private double Metric(string key) => (double)FindResource(key);
        private double SplitterSize => ((GridLength)FindResource("Dps.SplitterLength")).Value;

        private void ApplyDefaultSizes()
        {
            if (!_explicitRegionSizes.Contains(DockState.Left)) _sizes.LeftWidth = Metric("Dps.DefaultToolWidth");
            if (!_explicitRegionSizes.Contains(DockState.Right)) _sizes.RightWidth = Metric("Dps.DefaultToolWidth");
            if (!_explicitRegionSizes.Contains(DockState.Top)) _sizes.TopHeight = Metric("Dps.DefaultToolHeight");
            if (!_explicitRegionSizes.Contains(DockState.Bottom)) _sizes.BottomHeight = Metric("Dps.DefaultToolHeight");
        }
    }
}
