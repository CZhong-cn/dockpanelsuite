using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DockPanelSuite.Wpf.Sample
{
    public partial class MainWindow : Window
    {
        private int _document;
        private readonly string _layoutFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DockPanelSuite.Wpf.Sample", "layout.xml");
        public DockWorkspace Workspace => Docking;

        public MainWindow()
        {
            InitializeComponent();
            AddDocument();
            var tree = new TreeView();
            var project = new TreeViewItem { Header = "Native WPF project", IsExpanded = true };
            project.Items.Add(new TreeViewItem { Header = "MainWindow.xaml" });
            project.Items.Add(new TreeViewItem { Header = "DockWorkspace.cs" });
            tree.Items.Add(project);
            Docking.Add(new DockItem("explorer", "Explorer", tree), DockState.Left);
            Docking.Add(new DockItem("properties", "Properties", new TextBlock { Text = "Native WPF controls\nNo WinForms host required", Margin = new Thickness(16) }), DockState.Right);
            Docking.Add(new DockItem("output", "Output", new TextBox { Text = "Workspace ready.\n.NET Core 3.1 / .NET 6", IsReadOnly = true, AcceptsReturn = true }), DockState.Bottom);
        }

        private void AddDocument()
        {
            var number = ++_document;
            Docking.Add(new DockItem("document-" + number, "Document " + number,
                new TextBox { Text = "Edit this native WPF document. Its content survives docking, floating and layout restore.", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(18) }));
        }
        private void NewDocument(object sender, RoutedEventArgs args) => AddDocument();
        private void SaveLayout(object sender, RoutedEventArgs args) => WithErrorDialog(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_layoutFile));
            using (var stream = File.Create(_layoutFile)) Docking.SaveLayout(stream);
        });
        private void RestoreLayout(object sender, RoutedEventArgs args) => WithErrorDialog(() =>
        {
            using (var stream = File.OpenRead(_layoutFile)) Docking.LoadLayout(stream);
        });
        private void ShowHidden(object sender, RoutedEventArgs args)
        {
            foreach (var item in Docking.Items.Where(item => item.State == DockState.Hidden).ToArray()) Docking.Show(item);
        }
        private void WithErrorDialog(Action action)
        {
            try { action(); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Layout", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}
