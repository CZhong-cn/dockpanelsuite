using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DockPanelSuite.Wpf.Sample
{
    public partial class App : Application
    {
        private void OnStartup(object sender, StartupEventArgs args)
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            if (!args.Args.Contains("--smoke")) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var directory = Environment.GetEnvironmentVariable("DPS_SMOKE_OUTPUT") ?? ".";
                try
                {
                    Directory.CreateDirectory(directory);
                    var item = window.Workspace.Items.First();
                    window.Workspace.Float(item);
                    window.Workspace.Show(item, DockState.Document);
                    var second = window.Workspace.Items.First(candidate => candidate != item && candidate.State == DockState.Document);
                    window.Workspace.MoveBefore(second, item);
                    window.Workspace.Activate(item);
                    var tool = window.Workspace.Items.Single(candidate => candidate.Id == "history");
                    window.Workspace.OpenAutoHide(tool);
                    Capture(window, Path.Combine(directory, "wpf-auto-hide.png"));
                    window.Workspace.DismissAutoHide();
                    using (var stream = new MemoryStream())
                    {
                        window.Workspace.SaveLayout(stream);
                        window.Workspace.Pin(tool);
                        stream.Position = 0;
                        window.Workspace.LoadLayout(stream);
                        if (tool.State != DockState.AutoHide) throw new InvalidOperationException("Auto-hide layout did not restore.");
                    }
                    window.UpdateLayout();
                    var previewScroll = (ScrollViewer)((PreviewView)item.Content).Content;
                    if (previewScroll.ScrollableHeight > 1) throw new InvalidOperationException("Default preview should fit without scrolling.");
                    window.SetTheme(DockTheme.Light);
                    Capture(window, Path.Combine(directory, "wpf-sample-light.png"));
                    window.SetTheme(DockTheme.Dark);
                    Capture(window, Path.Combine(directory, "wpf-sample-dark.png"));
                    Capture(window, Path.Combine(directory, "wpf-sample.png"));
                    CaptureDockGuides(window, item, DockTheme.Light, DockState.Left, Path.Combine(directory, "wpf-docking-guides-light.png"));
                    CaptureDockGuides(window, item, DockTheme.Dark, DockState.Right, Path.Combine(directory, "wpf-docking-guides-dark.png"));
                    var themeButton = (Button)window.FindName("ThemeButton");
                    themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var themeMenu = themeButton.ContextMenu;
                    if (themeMenu.Items.Count != 3) throw new InvalidOperationException("Sample theme menu should expose all three themes.");
                    themeMenu.Items.Cast<MenuItem>().Single(command => Equals(command.Tag, DockTheme.GreenDark)).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    themeMenu.IsOpen = false;
                    if (window.Workspace.Theme != DockTheme.GreenDark) throw new InvalidOperationException("GreenDark theme menu did not select the theme.");
                    Capture(window, Path.Combine(directory, "wpf-sample-greendark.png"));
                    CaptureDockGuides(window, item, DockTheme.GreenDark, DockState.Right, Path.Combine(directory, "wpf-docking-guides-greendark.png"));
                    window.SetTheme(DockTheme.Dark);
                    window.Width = window.MinWidth; window.Height = window.MinHeight;
                    Capture(window, Path.Combine(directory, "wpf-sample-compact.png"));
                    if (previewScroll.ScrollableHeight <= 0) throw new InvalidOperationException("Compact preview should allow scrolling.");
                    previewScroll.ScrollToBottom();
                    window.UpdateLayout();
                    if (previewScroll.VerticalOffset <= 0) throw new InvalidOperationException("Compact preview did not scroll.");
                    Capture(window, Path.Combine(directory, "wpf-sample-compact-scrolled.png"));
                    window.Width = 1360; window.Height = 860;
                    window.ArrangeNestedLayout();
                    Capture(window, Path.Combine(directory, "wpf-nested-layout-dark.png"));
                    CaptureDockGuides(window, window.Workspace.Items.Single(candidate => candidate.Id == "nested-document-2"), DockTheme.Dark, DockState.Right,
                        Path.Combine(directory, "wpf-nested-docking-guides.png"), window.Workspace.Items.Single(candidate => candidate.Id == "nested-document-4"));
                    using (var nested = new MemoryStream())
                    {
                        window.Workspace.SaveLayout(nested);
                        var fourth = window.Workspace.Items.Single(candidate => candidate.Id == "nested-document-4");
                        var fifth = window.Workspace.Items.Single(candidate => candidate.Id == "nested-document-5");
                        window.Workspace.Dock(fourth, fifth);
                        nested.Position = 0; window.Workspace.LoadLayout(nested);
                    }
                    window.SetTheme(DockTheme.Light);
                    Capture(window, Path.Combine(directory, "wpf-nested-layout-light.png"));
                    window.ArrangeTabbedToolLayout();
                    window.SetTheme(DockTheme.Dark);
                    Capture(window, Path.Combine(directory, "wpf-tool-group-dark.png"));
                    window.SetTheme(DockTheme.Light);
                    Capture(window, Path.Combine(directory, "wpf-tool-group-light.png"));
                    window.SetTheme(DockTheme.GreenDark);
                    Capture(window, Path.Combine(directory, "wpf-tool-group-greendark.png"));
                    window.SetTheme(DockTheme.Dark);
                    var groupedSolution = window.Workspace.Items.Single(candidate => candidate.Id == "group-solution");
                    var groupedOutput = window.Workspace.Items.Single(candidate => candidate.Id == "group-output");
                    window.Workspace.AutoHideGroup(groupedSolution);
                    if (groupedOutput.State != DockState.AutoHide) throw new InvalidOperationException("Tool group did not auto-hide together.");
                    Capture(window, Path.Combine(directory, "wpf-tool-group-autohide.png"));
                    using (var grouped = new MemoryStream())
                    {
                        window.Workspace.SaveLayout(grouped);
                        window.Workspace.PinGroup(groupedSolution);
                        grouped.Position = 0; window.Workspace.LoadLayout(grouped);
                    }
                    if (groupedOutput.State != DockState.AutoHide || groupedSolution.State != DockState.AutoHide)
                        throw new InvalidOperationException("Auto-hidden tool group did not restore.");
                    window.Workspace.OpenAutoHide(groupedSolution);
                    Capture(window, Path.Combine(directory, "wpf-tool-group-flyout.png"));
                    window.SetTheme(DockTheme.GreenDark);
                    Capture(window, Path.Combine(directory, "wpf-tool-group-flyout-greendark.png"));
                    window.Workspace.PinGroup(groupedSolution);
                    if (groupedOutput.State != DockState.Right || groupedSolution.State != DockState.Right)
                        throw new InvalidOperationException("Tool group did not pin together.");
                    window.Close();
                }
                catch (Exception error)
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "smoke-error.txt"), error.ToString());
                    Shutdown(1);
                }
            }));
        }

        private static void CaptureDockGuides(MainWindow window, DockItem item, DockTheme theme, DockState state, string path, DockItem target = null)
        {
            window.SetTheme(theme);
            window.UpdateLayout();
            var grid = ((Grid)window.Workspace.Content).Children.OfType<Grid>().Single();
            var bounds = window.Workspace.GroupBounds(target ?? item);
            var guide = DockGuideGeometry.Create(grid.ActualWidth, grid.ActualHeight, bounds.X, bounds.Y, bounds.Width, bounds.Height, window.Workspace.GuideMetrics).Single(target => target.State == state && !target.IsOuter);
            var point = new Point(guide.X + guide.Size / 2, guide.Y + guide.Size / 2);
            if (window.Workspace.UpdateDockDrag(item, point) != state) throw new InvalidOperationException("Dock guide preview chose the wrong target.");
            Capture(window, path);
            window.Workspace.ClearDropPreview();
        }
        private static void Capture(Window window, string path)
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            var dpi = VisualTreeHelper.GetDpi(content);
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX),
                (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
        }
    }
}
