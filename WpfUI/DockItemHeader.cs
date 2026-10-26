using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DockPanelSuite.Wpf
{
    /// <summary>Template-driven chrome shared by document tabs and tool-pane captions.</summary>
    public sealed class DockItemHeader : Control
    {
        public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
            nameof(Item), typeof(DockItem), typeof(DockItemHeader), new PropertyMetadata(null));
        public static readonly DependencyProperty IsToolTabProperty = DependencyProperty.Register(
            nameof(IsToolTab), typeof(bool), typeof(DockItemHeader), new PropertyMetadata(false));
        public static readonly DependencyProperty IsToolPaneHeaderProperty = DependencyProperty.Register(
            nameof(IsToolPaneHeader), typeof(bool), typeof(DockItemHeader), new PropertyMetadata(false));
        public static readonly DependencyProperty IsDragHandleProperty = DependencyProperty.RegisterAttached(
            "IsDragHandle", typeof(bool), typeof(DockItemHeader), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static readonly RoutedUICommand CloseCommand = new RoutedUICommand("Close pane", nameof(CloseCommand), typeof(DockItemHeader));
        public static readonly RoutedUICommand TogglePinCommand = new RoutedUICommand("Toggle group pin", nameof(TogglePinCommand), typeof(DockItemHeader));

        public DockItem Item { get => (DockItem)GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
        public bool IsToolTab { get => (bool)GetValue(IsToolTabProperty); set => SetValue(IsToolTabProperty, value); }
        public bool IsToolPaneHeader { get => (bool)GetValue(IsToolPaneHeaderProperty); set => SetValue(IsToolPaneHeaderProperty, value); }

        /// <summary>Marks the title area of a custom template as the drag/double-click handle.</summary>
        public static bool GetIsDragHandle(DependencyObject element) => (bool)element.GetValue(IsDragHandleProperty);
        public static void SetIsDragHandle(DependencyObject element, bool value) => element.SetValue(IsDragHandleProperty, value);
    }
}
