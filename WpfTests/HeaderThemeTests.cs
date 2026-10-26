using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class HeaderThemeTests
    {
        private DockWorkspace _workspace;
        private DockItem _document, _output, _solution;
        private Window _host;

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _document = new DockItem("document", new string('D', 100), new TextBox());
            _output = new DockItem("output", "Output", new TextBox());
            _solution = new DockItem("solution", "Solution Explorer", new TextBox());
            _workspace.Add(_document); _workspace.Add(_output, DockState.Right); _workspace.Add(_solution, DockState.Right);
            _host = new Window { Content = _workspace, Width = 1000, Height = 700, ShowInTaskbar = false };
            _host.Show(); Pump();
        }

        [TearDown]
        public void TearDown() { _host.Content = null; Pump(); _host.Close(); Pump(); }

        [TestCase(DockTheme.Light), TestCase(DockTheme.Dark), TestCase(DockTheme.GreenDark)]
        public void XamlThemesExposeTheSamePaletteAndSharedHeaderTemplate(DockTheme theme)
        {
            var palette = DockThemeResources.Create(theme);
            var dark = DockThemeResources.Create(DockTheme.Dark);
            StringAssert.EndsWith($"/{theme}.xaml", palette.Source.OriginalString);
            CollectionAssert.AreEquivalent(dark.Keys, palette.Keys);
            Assert.AreEqual(17, palette.Count);
            foreach (var key in palette.Keys)
            {
                Assert.IsInstanceOf<SolidColorBrush>(palette[key]);
                Assert.IsTrue(((SolidColorBrush)palette[key]).IsFrozen);
            }
            Assert.IsInstanceOf<Style>(palette["Dps.DockItemHeader"]);
            Assert.IsInstanceOf<Style>(palette["Dps.ToolTabControl"]);
            Assert.Throws<ArgumentOutOfRangeException>(() => DockThemeResources.Create((DockTheme)999));
        }

        [Test]
        public void GreenDarkRoundTripsThroughExistingThemesWithFloatingFlyoutAndGuideChromeAlive()
        {
            _workspace.Float(_document); _workspace.AutoHideGroup(_solution); _workspace.OpenAutoHide(_output); Pump();
            var floating = _host.OwnedWindows.Cast<Window>().Single();
            var caption = Caption(DockState.AutoHide);
            _workspace.Resources["Dps.HeaderTitleMargin"] = new Thickness(11);
            foreach (var theme in new[] { DockTheme.GreenDark, DockTheme.Light, DockTheme.Dark, DockTheme.GreenDark })
            {
                _workspace.Theme = theme; Pump();
                var palette = DockThemeResources.Create(theme);
                foreach (var key in palette.Keys) Assert.AreEqual(ColorOf(palette[key]), ColorOf(_workspace.FindResource(key)), $"{theme}: {key}");
                Assert.AreSame(floating, _host.OwnedWindows.Cast<Window>().Single());
                Assert.AreSame(caption, Caption(DockState.AutoHide));
                Assert.AreSame(_output, _workspace.OpenAutoHideItem);
                Assert.AreEqual(DockState.AutoHide, _solution.State);
                Assert.AreEqual(ColorOf(palette["Dps.Surface"]), ColorOf(floating.Background));
                Assert.AreEqual(ColorOf(palette["Dps.Foreground"]), ColorOf(Part<Path>(caption, "PinGlyph").Stroke));
                Assert.AreEqual(new Thickness(11), Part<TextBlock>(caption, "Title").Margin);
                var grid = ((Grid)_workspace.Content).Children.OfType<Grid>().Single();
                var bounds = _workspace.GroupBounds(_document);
                var guide = DockGuideGeometry.Create(grid.ActualWidth, grid.ActualHeight, bounds.X, bounds.Y, bounds.Width, bounds.Height, _workspace.GuideMetrics)
                    .Single(tile => !tile.IsOuter && tile.State == DockState.Right);
                _workspace.UpdateDockDrag(_document, new Point(guide.X + guide.Size / 2, guide.Y + guide.Size / 2)); Pump();
                var tile = Find<DockGuideTile>(_workspace).Single(candidate => candidate.IsHighlighted);
                Assert.AreEqual(ColorOf(palette["Dps.GuideHover"]), ColorOf(tile.Background));
                Assert.AreEqual(ColorOf(palette["Dps.GuideHoverGlyph"]), ColorOf(tile.Foreground));
                Assert.AreEqual(ColorOf(palette["Dps.Preview"]), ColorOf(grid.Children.OfType<Border>().Single().Background));
                _workspace.ClearDropPreview();
            }
            _workspace.PinGroup(_output); _workspace.Pin(_document); Pump();
            Assert.AreEqual(DockState.Right, _solution.State); Assert.AreEqual(DockState.Document, _document.State);
        }

        [Test]
        public void HeaderMetricsAndPinGeometryCanBeOverriddenWithoutRebuildingTheView()
        {
            var caption = Caption(DockState.Right);
            var tabs = Tabs(DockState.Document);
            var documentHeader = (DockItemHeader)((TabItem)tabs.SelectedItem).Header;
            var title = Part<TextBlock>(documentHeader, "Title");
            Assert.AreEqual(200, title.MaxWidth);
            Assert.AreEqual(double.PositiveInfinity, Part<TextBlock>(caption, "Title").MaxWidth);
            Assert.Less(Part<Button>(caption, "Pin").TranslatePoint(new Point(), caption).X,
                Part<Button>(caption, "Close").TranslatePoint(new Point(), caption).X);
            var geometry = Geometry.Parse("M0,0 L10,10 M10,0 L0,10");
            var margin = new Thickness(12, 3, 9, 4);
            _workspace.Resources["Dps.HeaderTitleMargin"] = margin;
            _workspace.Resources["Dps.TabTitleMaxWidth"] = 84d;
            _workspace.Resources["Dps.ToolPaneTitleMaxWidth"] = 120d;
            _workspace.Resources["Dps.ToolPaneHeaderMargin"] = new Thickness(6);
            _workspace.Resources["Dps.PinGeometry"] = geometry;
            Pump();
            Assert.AreSame(caption, Caption(DockState.Right));
            Assert.AreEqual(margin, title.Margin); Assert.AreEqual(84, title.MaxWidth);
            Assert.AreEqual(120, Part<TextBlock>(caption, "Title").MaxWidth);
            Assert.AreEqual(new Thickness(6), caption.Margin);
            Assert.AreSame(geometry, Part<Path>(caption, "PinGlyph").Data);
            Assert.IsTrue(DockItemHeader.GetIsDragHandle(title));
        }

        [Test]
        public void HeaderBindingsFollowTitleCanCloseAndFlyoutPinState()
        {
            var caption = Caption(DockState.Right);
            _solution.Title = "Renamed tool"; _solution.CanClose = false; Pump();
            Assert.AreEqual("Renamed tool", Part<TextBlock>(caption, "Title").Text);
            Assert.IsFalse(Part<Button>(caption, "Close").IsEnabled);
            Assert.IsNull(Part<Path>(caption, "PinGlyph").RenderTransform as RotateTransform);
            _workspace.AutoHideGroup(_solution); _workspace.OpenAutoHide(_solution); Pump();
            caption = Caption(DockState.AutoHide);
            Assert.AreEqual(45, ((RotateTransform)Part<Path>(caption, "PinGlyph").RenderTransform).Angle);
            StringAssert.Contains("Pin group", (string)Part<Button>(caption, "Pin").ToolTip);
        }

        [Test]
        public void AnApplicationXamlTemplateCanRearrangeButtonsAndKeepGroupCommands()
        {
            var style = (Style)XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       xmlns:local='clr-namespace:DockPanelSuite.Wpf;assembly=WpfUI'
       TargetType='{x:Type local:DockItemHeader}'>
  <Setter Property='Template'><Setter.Value>
    <ControlTemplate TargetType='{x:Type local:DockItemHeader}'>
      <StackPanel Orientation='Horizontal'>
        <Button x:Name='CustomClose' Content='Close' Command='{x:Static local:DockItemHeader.CloseCommand}'
                CommandTarget='{Binding RelativeSource={RelativeSource TemplatedParent}}'/>
        <TextBlock x:Name='CustomTitle' Text='{Binding Item.Title, RelativeSource={RelativeSource TemplatedParent}}'
                   local:DockItemHeader.IsDragHandle='True'/>
        <Button x:Name='CustomPin' Content='Pin' Command='{x:Static local:DockItemHeader.TogglePinCommand}'
                CommandTarget='{Binding RelativeSource={RelativeSource TemplatedParent}}'/>
      </StackPanel>
    </ControlTemplate>
  </Setter.Value></Setter>
</Style>");
            var original = Caption(DockState.Right);
            _workspace.Resources["Dps.DockItemHeader"] = style; Pump();
            Assert.AreSame(original, Caption(DockState.Right));
            Assert.AreEqual("Solution Explorer", Part<TextBlock>(original, "CustomTitle").Text);
            Assert.Less(Part<Button>(original, "CustomClose").TranslatePoint(new Point(), original).X,
                Part<Button>(original, "CustomPin").TranslatePoint(new Point(), original).X);
            Invoke(Part<Button>(original, "CustomPin")); Pump();
            Assert.AreEqual(DockState.AutoHide, _output.State); Assert.AreEqual(DockState.AutoHide, _solution.State);
            _workspace.OpenAutoHide(_solution); Pump();
            Invoke(Part<Button>(Caption(DockState.AutoHide), "CustomPin")); Pump();
            Assert.AreEqual(DockState.Right, _output.State); Assert.AreEqual(DockState.Right, _solution.State);
            DoubleClick(Part<TextBlock>(Caption(DockState.Right), "CustomTitle")); Pump();
            Assert.AreEqual(DockState.Float, _solution.State);
            var floating = _host.OwnedWindows.Cast<Window>().Single();
            var floatingHeader = Find<DockItemHeader>(floating).Single();
            DoubleClick(Part<TextBlock>(floatingHeader, "CustomTitle")); Pump();
            Assert.AreEqual(DockState.Right, _solution.State);
            Assert.AreEqual(0, _host.OwnedWindows.Count);
            Invoke(Part<Button>(Caption(DockState.Right), "CustomClose")); Pump();
            Assert.AreEqual(DockState.Hidden, _solution.State); Assert.AreEqual(DockState.Right, _output.State);
        }

        [Test]
        public void ThemeChangesUpdateOpenHeaderGlyphsAndPreserveLocalOverrides()
        {
            _workspace.AutoHideGroup(_solution); _workspace.OpenAutoHide(_solution); Pump();
            var caption = Caption(DockState.AutoHide);
            var glyph = Part<Path>(caption, "PinGlyph");
            var before = ((SolidColorBrush)glyph.Stroke).Color;
            _workspace.Resources["Dps.HeaderTitleMargin"] = new Thickness(11);
            _workspace.Theme = DockTheme.Dark; Pump();
            Assert.AreSame(caption, Caption(DockState.AutoHide));
            Assert.AreNotEqual(before, ((SolidColorBrush)glyph.Stroke).Color);
            Assert.AreEqual(((SolidColorBrush)_workspace.FindResource("Dps.Foreground")).Color, ((SolidColorBrush)glyph.Stroke).Color);
            Assert.AreEqual(new Thickness(11), Part<TextBlock>(caption, "Title").Margin);
            _workspace.Theme = DockTheme.Light; Pump();
            Assert.AreEqual(before, ((SolidColorBrush)glyph.Stroke).Color);
        }

        private static Color ColorOf(object brush) => ((SolidColorBrush)brush).Color;
        private TabControl Tabs(DockState state) => Find<TabControl>(_workspace).Single(tab => Equals(tab.Tag, state));
        private DockItemHeader Caption(DockState state) => (DockItemHeader)Tabs(state).Resources["Dps.ToolPaneHeader"];
        private static T Part<T>(DockItemHeader header, string name) where T : DependencyObject => (T)header.Template.FindName(name, header);
        private static void DoubleClick(UIElement title)
        {
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent };
            // Synthesize the routed double-click without depending on desktop pointer position.
            typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount)).SetValue(args, 2);
            title.RaiseEvent(args);
        }
        private static void Invoke(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke();
        private static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match) yield return match;
                foreach (var next in Find<T>(child)) yield return next;
            }
        }
        private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    }
}
