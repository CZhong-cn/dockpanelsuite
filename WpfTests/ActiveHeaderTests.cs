using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class ActiveHeaderTests
    {
        private DockWorkspace _workspace;
        private DockItem _left, _right, _otherTab, _output, _solution;
        private Window _host;

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _left = Add("Left", DockState.Document);
            _right = Add("Right", DockState.Document);
            _workspace.Dock(_right, _left, DockPosition.Right);
            _otherTab = Add("Other tab", DockState.Document);
            _workspace.Dock(_otherTab, _right);
            _output = Add("Output", DockState.Right);
            _solution = Add("Solution", DockState.Right);
            _host = new Window { Content = _workspace, Width = 1100, Height = 700, ShowInTaskbar = false };
            _host.Show();
            _workspace.Activate(_right); Pump();
        }

        [TearDown]
        public void TearDown() { _host.Content = null; Pump(); _host.Close(); Pump(); }

        [TestCase(DockTheme.Light), TestCase(DockTheme.Dark), TestCase(DockTheme.GreenDark)]
        public void ContentFocusDistinguishesTheActivePaneFromOtherSelectedTabs(DockTheme theme)
        {
            _workspace.Theme = theme; Pump();
            var left = Page(_left); var right = Page(_right); var other = Page(_otherTab);
            Keyboard.Focus((TextBox)_left.Content); Pump();
            AssertActive(_left); AssertTab(left, true); AssertTab(right, false);
            Assert.IsFalse(other.IsSelected);
            Assert.AreEqual(Colors.Transparent, ColorOf(TabFrame(other).Background));
            Assert.AreNotEqual(ColorOf(TabFrame(left).Background), ColorOf(TabFrame(right).Background));
            Assert.AreNotEqual(ColorOf(TabFrame(left).BorderBrush), ColorOf(TabFrame(right).BorderBrush));
            Assert.AreEqual(ColorOf(left.Foreground), ColorOf(HeaderPart<Button>((DockItemHeader)left.Header, "Close").Foreground));

            Keyboard.Focus((TextBox)_right.Content); Pump();
            AssertActive(_right); AssertTab(left, false); AssertTab(right, true);
            Assert.AreSame(left, Page(_left)); Assert.AreSame(right, Page(_right));
            Assert.IsTrue(left.IsSelected); Assert.IsTrue(right.IsSelected);
        }

        [Test]
        public void ClickingAnAlreadySelectedTitleActivatesItsPaneWithoutRebuildingIt()
        {
            var left = Page(_left); var right = Page(_right);
            ClickTitle((DockItemHeader)left.Header); Pump();
            AssertActive(_left); Assert.IsTrue(((TextBox)_left.Content).IsKeyboardFocusWithin);
            AssertTab(left, true); AssertTab(right, false); Assert.AreSame(left, Page(_left));
            var tools = Tools(); var caption = Caption(tools);
            ClickTitle(caption); Pump();
            AssertActive(_solution); Assert.IsTrue(((TextBox)_solution.Content).IsKeyboardFocusWithin);
            Assert.AreSame(tools, Tools()); AssertCaption(tools, true); AssertTab(left, false);
        }

        [Test]
        public void TabSelectionAndKeyboardFocusActivateTheCorrectGroup()
        {
            var right = Page(_right); var other = Page(_otherTab);
            ((TabControl)ItemsControl.ItemsControlFromItemContainer(right)).SelectedItem = other; Pump();
            AssertActive(_otherTab); AssertTab(other, true); Assert.IsFalse(right.IsSelected);
            Keyboard.Focus(Page(_left)); Pump();
            AssertActive(_left); AssertTab(Page(_left), true); AssertTab(other, false);
            _workspace.ActivateNextDocument(); Pump();
            AssertActive(_right); AssertTab(Page(_right), true); Assert.IsFalse(Page(_otherTab).IsSelected);
        }

        [Test]
        public void ToolCaptionAndBottomTabFollowFocusAndLivePaletteOverrides()
        {
            var tools = Tools(); var selected = Page(_solution); var caption = Caption(tools);
            Keyboard.Focus((TextBox)_solution.Content); Pump();
            foreach (var theme in new[] { DockTheme.Light, DockTheme.Dark, DockTheme.GreenDark })
            {
                _workspace.Theme = theme; Pump();
                AssertActive(_solution); AssertCaption(tools, true); AssertTab(selected, true);
                Assert.AreEqual(BrushColor("Dps.ActiveTabForeground"), ColorOf(HeaderPart<Button>(caption, "Pin").Foreground));
                AssertTab(Page(_left), false); AssertTab(Page(_right), false);
            }
            var custom = new SolidColorBrush(Colors.Purple);
            _workspace.Resources["Dps.ActiveTabBackground"] = custom; Pump();
            Assert.AreSame(custom, CaptionFrame(tools).Background);
            Assert.AreSame(custom, TabFrame(selected).Background);
            Keyboard.Focus((TextBox)_right.Content); Pump();
            AssertActive(_right); AssertCaption(tools, false); AssertTab(selected, false);
            Assert.AreSame(caption, Caption(tools));
        }

        [Test]
        public void FloatingActivationAndOpenFlyoutUseTheSameActiveHeaderColors()
        {
            _workspace.Float(_right); Pump();
            var floating = _host.OwnedWindows.Cast<Window>().Single();
            _workspace.Activate(_left); Pump();
            var header = Find<DockItemHeader>(floating).Single();
            Assert.IsFalse(_right.IsActive);
            floating.Activate(); Pump();
            AssertActive(_right);
            Assert.AreEqual(BrushColor("Dps.ActiveTabBackground"), ColorOf(header.Background));
            Assert.AreEqual(BrushColor("Dps.ActiveTabForeground"), ColorOf(HeaderPart<Button>(header, "Close").Foreground));
            _workspace.Activate(_left); Pump();
            Assert.IsFalse(_right.IsActive); Assert.IsNull(header.Background);
            _workspace.AutoHideGroup(_solution); _workspace.OpenAutoHide(_output); Pump();
            var flyout = Tools(DockState.AutoHide);
            AssertActive(_output); AssertCaption(flyout, true); AssertTab(Page(_output), true);
            _workspace.DismissAutoHide(); Pump();
            Assert.IsFalse(_output.IsActive); Assert.IsFalse(_solution.IsActive);
            Assert.AreEqual(1, _workspace.Items.Count(item => item.IsActive));
        }

        [Test]
        public void ActiveStateNotifiesBindingsAndIsClearedOnHideRemoveAndLayoutRestore()
        {
            var changes = new List<string>();
            _left.PropertyChanged += (sender, args) => { if (args.PropertyName == nameof(DockItem.IsActive)) changes.Add("left:" + _left.IsActive); };
            _right.PropertyChanged += (sender, args) => { if (args.PropertyName == nameof(DockItem.IsActive)) changes.Add("right:" + _right.IsActive); };
            Keyboard.Focus((TextBox)_left.Content); Pump();
            CollectionAssert.AreEquivalent(new[] { "left:True", "right:False" }, changes);
            Keyboard.Focus((TextBox)_left.Content); Pump(); Assert.AreEqual(2, changes.Count);
            using (var snapshot = new MemoryStream())
            {
                _workspace.SaveLayout(snapshot);
                _workspace.Hide(_left); Pump(); Assert.IsFalse(_left.IsActive);
                snapshot.Position = 0; _workspace.LoadLayout(snapshot); Pump(); AssertActive(_left);
            }
            _workspace.Remove(_left); Pump(); Assert.IsFalse(_left.IsActive);
            Assert.AreEqual(1, _workspace.Items.Count(item => item.IsActive));
            _workspace.ActivateNextDocument(); Pump(); AssertActive(_workspace.ActiveItem);
        }

        private DockItem Add(string id, DockState state)
        { var item = new DockItem(id, id, new TextBox { Text = id }); _workspace.Add(item, state); return item; }
        private void AssertActive(DockItem expected)
        { Assert.AreSame(expected, _workspace.ActiveItem); CollectionAssert.AreEqual(new[] { expected }, _workspace.Items.Where(item => item.IsActive)); }
        private void AssertTab(TabItem page, bool active)
        {
            Assert.IsTrue(page.IsSelected);
            string prefix = active ? "Dps.ActiveTab" : "Dps.InactiveTab";
            Assert.AreEqual(BrushColor(prefix + "Background"), ColorOf(TabFrame(page).Background));
            Assert.AreEqual(BrushColor(prefix + "Foreground"), ColorOf(page.Foreground));
            Assert.AreEqual(BrushColor(prefix + "Border"), ColorOf(TabFrame(page).BorderBrush));
        }
        private void AssertCaption(TabControl tools, bool active)
        {
            Assert.AreEqual(BrushColor(active ? "Dps.ActiveTabBackground" : "Dps.Header"), ColorOf(CaptionFrame(tools).Background));
            Assert.AreEqual(BrushColor(active ? "Dps.ActiveTabForeground" : "Dps.Foreground"), ColorOf(Caption(tools).Foreground));
        }
        private Color BrushColor(string key) => ColorOf(_workspace.FindResource(key));
        private static Color ColorOf(object brush) => ((SolidColorBrush)brush).Color;
        private TabItem Page(DockItem item) => Find<TabItem>(_workspace).Single(page => ReferenceEquals(page.Tag, item));
        private TabControl Tools(DockState state = DockState.Right) => Find<TabControl>(_workspace).Single(tabs => Equals(tabs.Tag, state));
        private static DockItemHeader Caption(TabControl tools) => (DockItemHeader)tools.Resources["Dps.ToolPaneHeader"];
        private static Border CaptionFrame(TabControl tools) => (Border)tools.Template.FindName("ToolCaptionFrame", tools);
        private static Border TabFrame(TabItem page) => (Border)page.Template.FindName("Tab", page);
        private static T HeaderPart<T>(DockItemHeader header, string name) where T : DependencyObject => (T)header.Template.FindName(name, header);
        private static void ClickTitle(DockItemHeader header)
        { HeaderPart<TextBlock>(header, "Title").RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent }); }
        private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var next in Find<T>(child)) yield return next;
            }
        }
        private static void Pump()
        { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    }
}
