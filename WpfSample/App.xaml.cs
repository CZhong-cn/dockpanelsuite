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
                try
                {
                    var item = window.Workspace.Items.First();
                    window.Workspace.Float(item);
                    window.Workspace.Show(item, DockState.Document);
                    using (var stream = new MemoryStream())
                    {
                        window.Workspace.SaveLayout(stream);
                        stream.Position = 0;
                        window.Workspace.LoadLayout(stream);
                    }
                    window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    var directory = Environment.GetEnvironmentVariable("DPS_SMOKE_OUTPUT") ?? ".";
                    Directory.CreateDirectory(directory);
                    using (var file = File.Create(Path.Combine(directory, "wpf-sample.png"))) encoder.Save(file);
                    window.Close();
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error);
                    Shutdown(1);
                }
            }));
        }
    }
}
