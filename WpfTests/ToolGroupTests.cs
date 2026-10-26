using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class ToolGroupTests
    {
        private DockWorkspace _workspace;
        private DockItem _document, _output, _solution;
        private Window _host;

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _document = Add("document", DockState.Document);
            _output = Add("Output", DockState.Right);
            _solution = Add("Solution Explorer", DockState.Right);
            _host = new Window { Content = _workspace, Width = 1000, Height = 700, ShowInTaskbar = false };
            _host.Show(); Pump();
        }
        [TearDown]
        public void TearDown() { _host.Content = null; Pump(); _host.Close(); Pump(); }

        [Test]
        public void ToolPaneSharesACaptionAndShowsItsTabsBelowTheContent()
        {
            var tools = ToolTabs(DockState.Right);
            Assert.AreEqual(System.Windows.Controls.Dock.Bottom, tools.TabStripPlacement);
            var caption = (FrameworkElement)tools.Template.FindName("ToolCaption", tools);
            var body = (FrameworkElement)tools.Template.FindName("PART_SelectedContentHost", tools);
            var bottom = (FrameworkElement)tools.Template.FindName("BottomTabs", tools);
            Assert.Less(caption.TranslatePoint(new Point(), tools).Y, body.TranslatePoint(new Point(), tools).Y);
            Assert.Greater(bottom.TranslatePoint(new Point(), tools).Y, body.TranslatePoint(new Point(), tools).Y);
            Assert.AreEqual(Visibility.Visible, bottom.Visibility);
            foreach (TabItem tab in tools.Items) Assert.IsEmpty(Descendants<Button>((FrameworkElement)tab.Header).Where(button => button.IsVisible));
            Assert.AreEqual(2, Descendants<Button>(Caption(tools)).Count());
            var documents = Descendants<TabControl>(_workspace).Single(tab => Equals(tab.Tag, DockState.Document));
            Assert.AreEqual(System.Windows.Controls.Dock.Top, documents.TabStripPlacement);
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top), TestCase(DockState.Bottom)]
        public void SharedPinAutoHidesAndPinsEveryTabAtTheSameTreePosition(DockState edge)
        {
            _workspace.Show(_output, edge); _workspace.Show(_solution, edge); Pump();
            string group = Group(Snapshot(), _output).Id;
            int changes = 0; _workspace.LayoutChanged += (sender, args) => changes++;
            ClickPin(ToolTabs(edge)); Pump();
            Assert.AreEqual(1, changes);
            Assert.AreEqual(DockState.AutoHide, _output.State); Assert.AreEqual(DockState.AutoHide, _solution.State);
            Assert.IsNull(_workspace.OpenAutoHideItem);
            Assert.IsFalse(Descendants<TabControl>(_workspace).Any(tab => Equals(tab.Tag, edge)));
            _workspace.OpenAutoHide(_solution); Pump();
            var flyout = ToolTabs(DockState.AutoHide);
            Assert.AreEqual(2, flyout.Items.Count);
            ClickPin(flyout); Pump();
            Assert.AreEqual(edge, _output.State); Assert.AreEqual(edge, _solution.State);
            Assert.IsNull(_workspace.OpenAutoHideItem);
            Assert.AreEqual(group, Group(Snapshot(), _output).Id);
            Assert.AreSame(_solution, ((TabItem)ToolTabs(edge).SelectedItem).Tag);
            Assert.AreEqual("Output text", ((TextBox)_output.Content).Text);
        }

        [Test]
        public void ClickingASideTabOpensItsWholeGroupAndSwitchesTheSharedCaption()
        {
            _workspace.AutoHideGroup(_solution); Pump();
            var rail = Descendants<Button>(_workspace).Single(button => button.Tag == _output);
            rail.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            var tools = ToolTabs(DockState.AutoHide);
            Assert.AreSame(_output, _workspace.OpenAutoHideItem);
            Assert.AreSame(_output, ((TabItem)tools.SelectedItem).Tag);
            Assert.AreEqual("Output", Descendants<TextBlock>(Caption(tools)).Single(text => text.Name == "Title").Text);
            tools.SelectedItem = tools.Items.Cast<TabItem>().Single(tab => tab.Tag == _solution); Pump();
            Assert.AreSame(_solution, _workspace.OpenAutoHideItem);
            Assert.AreSame(_solution, _workspace.ActiveItem);
            Assert.AreEqual("Solution Explorer", Descendants<TextBlock>(Caption(tools)).Single(text => text.Name == "Title").Text);
            Assert.IsTrue(((TextBox)_solution.Content).IsKeyboardFocusWithin);
            Assert.AreEqual(DockState.AutoHide, _output.State); Assert.AreEqual(DockState.AutoHide, _solution.State);
        }

        [Test]
        public void ClosingASelectedToolOnlyClosesThatTabAndUpdatesTheCaption()
        {
            var tools = ToolTabs(DockState.Right);
            ClickClose(tools); Pump();
            Assert.AreEqual(DockState.Hidden, _solution.State); Assert.AreEqual(DockState.Right, _output.State);
            tools = ToolTabs(DockState.Right);
            Assert.AreEqual("Output", Descendants<TextBlock>(Caption(tools)).Single(text => text.Name == "Title").Text);
            Assert.AreEqual(Visibility.Collapsed, ((FrameworkElement)tools.Template.FindName("BottomTabs", tools)).Visibility);
        }

        [Test]
        public void CloseCancellationAndCanCloseDoNotPreventGroupAutoHide()
        {
            _solution.CanClose = false; Pump();
            var close = Descendants<Button>(Caption(ToolTabs(DockState.Right))).Single(button => (string)button.ToolTip == "隐藏面板 / Close pane (Ctrl+F4)");
            Assert.IsFalse(close.IsEnabled);
            _solution.CanClose = true;
            _workspace.ItemClosing += (sender, args) => args.Cancel = true;
            ClickClose(ToolTabs(DockState.Right)); Pump();
            Assert.AreEqual(DockState.Right, _solution.State);
            ClickPin(ToolTabs(DockState.Right)); Pump();
            Assert.AreEqual(DockState.AutoHide, _output.State); Assert.AreEqual(DockState.AutoHide, _solution.State);
        }

        [Test]
        public void GroupPinDoesNotHideASiblingSplitOrReviveClosedTabs()
        {
            var sibling = Add("sibling", DockState.Right);
            _workspace.Dock(sibling, _solution, DockPosition.Bottom);
            var closed = Add("closed", DockState.Right);
            _workspace.Dock(closed, _solution); _workspace.Hide(closed); Pump();
            _workspace.AutoHideGroup(_solution); Pump();
            Assert.AreEqual(DockState.Right, sibling.State);
            Assert.AreEqual(DockState.Hidden, closed.State);
            Assert.AreEqual(DockState.AutoHide, _output.State); Assert.AreEqual(DockState.AutoHide, _solution.State);
            _workspace.PinGroup(_solution); Pump();
            Assert.AreEqual(DockState.Hidden, closed.State);
            Assert.Greater(_workspace.GroupBounds(sibling).Top, _workspace.GroupBounds(_solution).Bottom);
        }

        [Test]
        public void AClosedFlyoutTabStaysHiddenWhenTheRemainingGroupIsPinned()
        {
            _workspace.AutoHideGroup(_solution); _workspace.OpenAutoHide(_solution); Pump();
            ClickClose(ToolTabs(DockState.AutoHide)); Pump();
            Assert.AreEqual(DockState.Hidden, _solution.State); Assert.AreEqual(DockState.AutoHide, _output.State);
            _workspace.OpenAutoHide(_output); Pump(); ClickPin(ToolTabs(DockState.AutoHide)); Pump();
            Assert.AreEqual(DockState.Hidden, _solution.State); Assert.AreEqual(DockState.Right, _output.State);
        }

        [Test]
        public void GroupStateSelectionAndOrderSurviveSnapshotRestoration()
        {
            _workspace.AutoHideGroup(_solution);
            _workspace.OpenAutoHide(_output); Pump();
            var saved = Snapshot(); var original = Group(saved, _output);
            _workspace.PinGroup(_output); _workspace.Dock(_solution, _output, DockPosition.Bottom);
            Restore(saved); Pump();
            Assert.IsNull(_workspace.OpenAutoHideItem);
            Assert.AreEqual(DockState.AutoHide, _output.State); Assert.AreEqual(DockState.AutoHide, _solution.State);
            _workspace.OpenAutoHide(_output); Pump();
            CollectionAssert.AreEqual(original.ItemIds, ToolTabs(DockState.AutoHide).Items.Cast<TabItem>().Select(tab => ((DockItem)tab.Tag).Id));
            _workspace.PinGroup(_output); Pump();
            Assert.AreEqual(original.Id, Group(Snapshot(), _output).Id);
            Assert.AreSame(_output, ((TabItem)ToolTabs(DockState.Right).SelectedItem).Tag);
        }

        [Test]
        public void SingleItemAutoHideAndPinRemainAvailable()
        {
            _workspace.AutoHide(_output); Pump();
            Assert.AreEqual(DockState.Right, _solution.State);
            _workspace.OpenAutoHide(_output); Pump();
            Assert.AreEqual(1, ToolTabs(DockState.AutoHide).Items.Count);
            _workspace.Pin(_output); Pump();
            Assert.AreEqual(2, ToolTabs(DockState.Right).Items.Count);
        }

        [Test]
        public void MovingAndRemovingFlyoutItemsReleasesTheirContentParents()
        {
            _workspace.AutoHideGroup(_solution); _workspace.OpenAutoHide(_solution); Pump();
            _workspace.Float(_solution); Pump();
            Assert.AreEqual(1, _host.OwnedWindows.Count);
            _workspace.OpenAutoHide(_output); Pump(); _workspace.Remove(_output); Pump();
            var other = new DockWorkspace(); other.Add(_output, DockState.Left);
            Assert.AreEqual(DockState.Left, _output.State);
            _workspace.Pin(_solution); Pump();
            Assert.AreEqual("Solution Explorer text", ((TextBox)_solution.Content).Text);
        }

        [Test]
        public void InvalidGroupCommandsLeaveTheLayoutUnchanged()
        {
            Assert.Throws<ArgumentException>(() => _workspace.AutoHideGroup(_document));
            Assert.Throws<ArgumentException>(() => _workspace.PinGroup(_solution));
            Assert.AreEqual(DockState.Right, _output.State); Assert.AreEqual(DockState.Right, _solution.State);
        }

        private DockItem Add(string id, DockState state)
        { var item = new DockItem(id, id, new TextBox { Text = id + " text" }); _workspace.Add(item, state); return item; }
        private TabControl ToolTabs(DockState state) => Descendants<TabControl>(_workspace).Single(tab => Equals(tab.Tag, state));
        private static FrameworkElement Caption(TabControl tabs) => (FrameworkElement)tabs.Resources["Dps.ToolPaneHeader"];
        private static void ClickPin(TabControl tabs) => Invoke(Descendants<Button>(Caption(tabs)).Single(button => ((string)button.ToolTip).Contains("group")));
        private static void ClickClose(TabControl tabs) => Invoke(Descendants<Button>(Caption(tabs)).Single(button => ((string)button.ToolTip).Contains("Close pane")));
        private static void Invoke(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke();
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        { for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) { var child = VisualTreeHelper.GetChild(root, index); if (child is T value) yield return value; foreach (var next in Descendants<T>(child)) yield return next; } }
        private static IEnumerable<DockTabGroup> Groups(DockLayoutNode node)
        { if (node is DockTabGroup group) yield return group; if (node is DockSplit split) foreach (var child in Groups(split.First).Concat(Groups(split.Second))) yield return child; }
        private static DockTabGroup Group(DockLayout layout, DockItem item) => layout.Regions.SelectMany(region => Groups(region.Root)).Single(group => group.ItemIds.Contains(item.Id));
        private DockLayout Snapshot() { using (var stream = new MemoryStream()) { _workspace.SaveLayout(stream); stream.Position = 0; return (DockLayout)new XmlSerializer(typeof(DockLayout)).Deserialize(stream); } }
        private void Restore(DockLayout layout) { using (var stream = new MemoryStream()) { new XmlSerializer(typeof(DockLayout)).Serialize(stream, layout); stream.Position = 0; _workspace.LoadLayout(stream); } }
        private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    }
}
