using System.Windows;

namespace DockPanelSuite.Wpf
{
    /// <summary>Layout state exposed to the shared chrome styles.</summary>
    public static class DockChrome
    {
        public static readonly DependencyProperty EdgeProperty = DependencyProperty.RegisterAttached(
            "Edge", typeof(DockState), typeof(DockChrome), new FrameworkPropertyMetadata(DockState.Document, FrameworkPropertyMetadataOptions.Inherits));
        public static DockState GetEdge(DependencyObject element) => (DockState)element.GetValue(EdgeProperty);
        public static void SetEdge(DependencyObject element, DockState value) => element.SetValue(EdgeProperty, value);
    }
}
