using System.Windows;
using System.Windows.Controls;

namespace DockPanelSuite.Wpf
{
    /// <summary>A docking target whose visuals and highlighted state are defined in XAML.</summary>
    public sealed class DockGuideTile : Control
    {
        public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
            nameof(State), typeof(DockState), typeof(DockGuideTile), new PropertyMetadata(DockState.Document));
        public static readonly DependencyProperty IsHighlightedProperty = DependencyProperty.Register(
            nameof(IsHighlighted), typeof(bool), typeof(DockGuideTile), new PropertyMetadata(false));
        public DockState State { get => (DockState)GetValue(StateProperty); set => SetValue(StateProperty, value); }
        public bool IsHighlighted { get => (bool)GetValue(IsHighlightedProperty); set => SetValue(IsHighlightedProperty, value); }
    }
}
