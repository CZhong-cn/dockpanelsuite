using System.ComponentModel;
using System.Windows;
using System.Windows.Shell;

namespace DockPanelSuite.Wpf
{
    // Native caption buttons obey the same policy as the pane header.
    internal sealed class DockFloatingWindow : Window
    {
        private readonly DockItem _item;
        public DockFloatingWindow(DockItem item)
        {
            _item = item;
            item.PropertyChanged += ItemChanged;
            Closed += (sender, args) => item.PropertyChanged -= ItemChanged;
            UpdateChrome();
        }
        private void ItemChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(DockItem.ShowCloseButton)) UpdateChrome();
        }
        private void UpdateChrome()
        {
            WindowStyle = _item.ShowCloseButton ? WindowStyle.SingleBorderWindow : WindowStyle.None;
            WindowChrome.SetWindowChrome(this, _item.ShowCloseButton ? null : new WindowChrome
            {
                CaptionHeight = 0, ResizeBorderThickness = new Thickness(5),
                GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0)
            });
        }
    }
}
