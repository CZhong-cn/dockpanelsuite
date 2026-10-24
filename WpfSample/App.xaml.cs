using System;
using System.IO;
using System.Linq;
using System.Windows;
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
                    window.SetTheme(DockTheme.Light);
                    Capture(window, Path.Combine(directory, "wpf-sample-light.png"));
                    window.SetTheme(DockTheme.Dark);
                    Capture(window, Path.Combine(directory, "wpf-sample-dark.png"));
                    Capture(window, Path.Combine(directory, "wpf-sample.png"));
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

        private static void Capture(Window window, string path)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
        }
    }
}
