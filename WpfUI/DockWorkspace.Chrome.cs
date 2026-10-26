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
            _sizes.LeftWidth = _sizes.RightWidth = Metric("Dps.DefaultToolWidth");
            _sizes.TopHeight = _sizes.BottomHeight = Metric("Dps.DefaultToolHeight");
        }
    }
}
