using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;
using System.Windows.Threading;
using System.Xml.Serialization;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class ChromeResourceTests
    {
        private DockWorkspace _workspace;
        private DockItem _document, _second, _output, _solution;
        private Window _host;
        private Grid Surface => ((Grid)_workspace.Content).Children.OfType<Grid>().Single();

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _document = Add("document"); _second = Add("second");
            _output = Add("output", DockState.Right); _solution = Add("solution", DockState.Right);
            _host = new Window { Content = _workspace, Width = 1100, Height = 750, ShowInTaskbar = false };
            _host.Show(); Pump();
        }
        [TearDown]
        public void TearDown() { _host.Content = null; Pump(); _host.Close(); Pump(); }

        [Test]
        public void LiveFontMinimumsAndSplitterMetricsKeepPreviewAndFinalSplitAligned()
        {
            _workspace.Dock(_second, _document, DockPosition.Right); Pump();
            var splitGrid = Find<Grid>(_workspace).Single(grid => grid.Tag is DockSplit);
            _workspace.Resources["Dps.FontSize"] = 17d;
            _workspace.Resources["Dps.FontFamily"] = new FontFamily("Consolas");
            _workspace.Resources["Dps.SplitterLength"] = new GridLength(11);
            _workspace.Resources["Dps.PaneMinWidth"] = 96d; _workspace.Resources["Dps.PaneMinHeight"] = 72d;
            _workspace.Resources["Dps.DocumentMinWidth"] = 180d;
            _workspace.Resources["Dps.DropPreviewInset"] = new Thickness(4, 3, 6, 5);
            _workspace.Resources["Dps.DropPreview"] = BasedOn("Dps.DropPreview", typeof(Border), new Setter(Border.CornerRadiusProperty, new CornerRadius(7)));
            Pump();
            Assert.AreEqual(17, _workspace.FontSize); Assert.AreEqual("Consolas", _workspace.FontFamily.Source);
            Assert.AreEqual(11, splitGrid.ColumnDefinitions[1].ActualWidth);
            Assert.AreEqual(11, Surface.ColumnDefinitions[3].ActualWidth);
            Assert.AreEqual(180, Surface.ColumnDefinitions[2].MinWidth);
            foreach (var tabs in Find<TabControl>(_workspace)) { Assert.AreEqual(96, tabs.MinWidth); Assert.AreEqual(72, tabs.MinHeight); }
            var bounds = _workspace.GroupBounds(_document);
            var point = PointFor(DockState.Right, bounds);
            _workspace.UpdateDockDrag(_output, point); Pump();
            var preview = Surface.Children.OfType<Border>().Single();
            double paneWidth = (bounds.Width - 11) / 2;
            Assert.AreEqual(new CornerRadius(7), preview.CornerRadius);
            Assert.AreEqual(paneWidth - 10, preview.Width, .01);
            Assert.AreEqual(bounds.Right - paneWidth + 4, preview.Margin.Left, .01);
            Assert.IsTrue(_workspace.CompleteDockDrag(_output, point)); Pump();
            var actual = _workspace.GroupBounds(_output);
            Assert.AreEqual(paneWidth, actual.Width, 1);
            Assert.AreEqual(bounds.Right - paneWidth, actual.Left, 1);
        }

        [Test]
        public void CustomGuideMetricsAndXamlGlyphsDriveRenderingHighlightAndDropTogether()
        {
            var metrics = new DockGuideMetrics { TileSize = 40, Step = 46, PlateSize = 140, ScaleThreshold = 280, OuterMargin = 20 };
            var arrow = Geometry.Parse("M1,1 L12,12 L1,23 Z");
            _workspace.Resources["Dps.GuideMetrics"] = metrics;
            _workspace.Resources["Dps.GuideArrowRight"] = arrow;
            _workspace.Resources["Dps.GuideTile"] = BasedOn("Dps.GuideTile", typeof(DockGuideTile), new Setter(Control.BorderThicknessProperty, new Thickness(3)));
            var point = PointFor(DockState.Right, _workspace.GroupBounds(_document));
            Assert.AreEqual(DockState.Right, _workspace.UpdateDockDrag(_second, point)); Pump();
            var overlay = Surface.Children.OfType<DockGuideOverlay>().Single();
            var tile = overlay.Children.OfType<DockGuideTile>().Single(candidate => candidate.IsHighlighted);
            Assert.AreEqual(40, tile.Width); Assert.AreEqual(new Thickness(3), tile.BorderThickness);
            Assert.AreSame(arrow, ((Path)tile.Template.FindName("Mark", tile)).Data);
            metrics.TileSize = 44;
            point = PointFor(DockState.Right, _workspace.GroupBounds(_document));
            _workspace.UpdateDockDrag(_second, point); Pump();
            Assert.AreSame(overlay, Surface.Children.OfType<DockGuideOverlay>().Single());
            tile = overlay.Children.OfType<DockGuideTile>().Single(candidate => candidate.IsHighlighted);
            Assert.AreEqual(44, tile.ActualWidth);
            var expected = DockGuideGeometry.Create(Surface.ActualWidth, Surface.ActualHeight, overlay.TargetBounds.X,
                overlay.TargetBounds.Y, overlay.TargetBounds.Width, overlay.TargetBounds.Height, metrics);
            CollectionAssert.AreEqual(expected.Select(target => target.Size), overlay.Targets.Select(target => target.Size));
            Assert.IsTrue(_workspace.CompleteDockDrag(_second, point)); Pump();
            Assert.Greater(_workspace.GroupBounds(_second).Left, _workspace.GroupBounds(_document).Right);
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top), TestCase(DockState.Bottom)]
        public void XamlRailAndFlyoutStylesFollowTheirEdgeAndKeepGroupPinBehavior(DockState edge)
        {
            _workspace.Show(_output, edge); _workspace.Show(_solution, edge);
            _workspace.Resources["Dps.RailButton"] = BasedOn("Dps.RailButton", typeof(Button), new Setter(Control.PaddingProperty, new Thickness(9)));
            _workspace.Resources["Dps.RailGroup"] = BasedOn("Dps.RailGroup", typeof(Border), new Setter(Border.CornerRadiusProperty, new CornerRadius(8)));
            _workspace.Resources["Dps.AutoHideFlyout"] = BasedOn("Dps.AutoHideFlyout", typeof(Border), new Setter(Border.BorderThicknessProperty, new Thickness(3)));
            _workspace.Resources["Dps.FlyoutMinWidth"] = 300d; _workspace.Resources["Dps.FlyoutMinHeight"] = 210d;
            _workspace.AutoHideGroup(_solution); Pump();
            var button = Find<Button>(_workspace).Single(candidate => candidate.Tag == _output);
            Assert.AreEqual(new Thickness(9), button.Padding);
            var cluster = (StackPanel)button.Parent;
            Assert.AreEqual(edge == DockState.Left || edge == DockState.Right ? Orientation.Vertical : Orientation.Horizontal, cluster.Orientation);
            Assert.AreEqual(new CornerRadius(8), ((Border)cluster.Parent).CornerRadius);
            if (edge == DockState.Left || edge == DockState.Right) Assert.AreEqual(-90, ((RotateTransform)button.LayoutTransform).Angle);
            else Assert.IsTrue(button.LayoutTransform.Value.IsIdentity);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            var flyout = ((Grid)_workspace.Content).Children.OfType<Border>().Single();
            Assert.AreEqual(new Thickness(3), flyout.BorderThickness);
            Assert.AreEqual(edge, DockChrome.GetEdge(flyout));
            if (edge == DockState.Left || edge == DockState.Right)
            {
                Assert.AreEqual(300, flyout.Width);
                Assert.AreEqual(edge == DockState.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right, flyout.HorizontalAlignment);
            }
            else
            {
                Assert.AreEqual(210, flyout.Height);
                Assert.AreEqual(edge == DockState.Top ? VerticalAlignment.Top : VerticalAlignment.Bottom, flyout.VerticalAlignment);
            }
            _workspace.PinGroup(_output); Pump();
            Assert.AreEqual(edge, _output.State); Assert.AreEqual(edge, _solution.State);
        }

        [Test]
        public void FloatingWindowsAndMenusShareLocalChromeOverridesDuringThemeChanges()
        {
            var accent = new SolidColorBrush(Colors.OrangeRed);
            _workspace.Resources["Dps.FontSize"] = 18d;
            _workspace.Resources["Dps.FloatingMinWidth"] = 700d; _workspace.Resources["Dps.FloatingMinHeight"] = 480d;
            _workspace.Resources["Dps.Accent"] = accent;
            _workspace.Resources["Dps.TabTitleMaxWidth"] = 88d;
            _workspace.Float(_solution); Pump();
            var floating = _host.OwnedWindows.Cast<Window>().Single();
            Assert.AreEqual(700, floating.MinWidth); Assert.AreEqual(480, floating.MinHeight);
            Assert.GreaterOrEqual(floating.ActualWidth, 700); Assert.GreaterOrEqual(floating.ActualHeight, 480);
            Assert.AreEqual(18, floating.FontSize);
            var header = Find<DockItemHeader>(floating).Single();
            Assert.AreEqual(88, ((TextBlock)header.Template.FindName("Title", header)).MaxWidth);
            Assert.AreSame(accent, header.ContextMenu.FindResource("Dps.Accent"));
            _workspace.Theme = DockTheme.Dark; Pump();
            Assert.AreSame(accent, floating.FindResource("Dps.Accent"));
            Assert.AreEqual(((SolidColorBrush)_workspace.FindResource("Dps.Surface")).Color, ((SolidColorBrush)floating.Background).Color);
            var layout = Snapshot(_workspace);
            Assert.GreaterOrEqual(layout.Items.Single(item => item.Id == _solution.Id).Width, 700);
        }

        [TestCase(false), TestCase(true)]
        public void InitialToolSizesComeFromResourcesWithoutOverwritingRestoredSizes(bool restore)
        {
            var workspace = new DockWorkspace();
            workspace.Resources["Dps.DefaultToolWidth"] = 280d; workspace.Resources["Dps.DefaultToolHeight"] = 190d;
            workspace.Add(new DockItem("doc", "Document", new TextBox()));
            workspace.Add(new DockItem("left", "Left", new TextBox()), DockState.Left);
            workspace.Add(new DockItem("bottom", "Bottom", new TextBox()), DockState.Bottom);
            if (restore)
            {
                var saved = Snapshot(workspace); saved.LeftWidth = 180; saved.BottomHeight = 150;
                using (var stream = new MemoryStream()) { new XmlSerializer(typeof(DockLayout)).Serialize(stream, saved); stream.Position = 0; workspace.LoadLayout(stream); }
            }
            var host = new Window { Content = workspace, Width = 1000, Height = 700, ShowInTaskbar = false };
            try
            {
                host.Show(); Pump();
                var layout = Snapshot(workspace);
                Assert.AreEqual(restore ? 180 : 280, layout.LeftWidth);
                Assert.AreEqual(restore ? 150 : 190, layout.BottomHeight);
            }
            finally { host.Content = null; host.Close(); Pump(); }
        }

        private Style BasedOn(string key, Type type, params Setter[] setters)
        { var style = new Style(type, (Style)_workspace.FindResource(key)); foreach (var setter in setters) style.Setters.Add(setter); return style; }
        private DockItem Add(string id, DockState state = DockState.Document)
        { var item = new DockItem(id, id, new TextBox()); _workspace.Add(item, state); return item; }
        private Point PointFor(DockState state, Rect bounds)
        { var guide = DockGuideGeometry.Create(Surface.ActualWidth, Surface.ActualHeight, bounds.X, bounds.Y, bounds.Width, bounds.Height, _workspace.GuideMetrics).Single(tile => !tile.IsOuter && tile.State == state); return new Point(guide.X + guide.Size / 2, guide.Y + guide.Size / 2); }
        private static DockLayout Snapshot(DockWorkspace workspace)
        { using (var stream = new MemoryStream()) { workspace.SaveLayout(stream); stream.Position = 0; return (DockLayout)new XmlSerializer(typeof(DockLayout)).Deserialize(stream); } }
        private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value; foreach (var next in Find<T>(child)) yield return next; } }
        private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    }
}
