using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class NestedDockingTests
    {
        private DockWorkspace _workspace;
        private Window _host;
        private DockItem _first, _second;

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _first = Add("one"); _second = Add("two");
            _host = new Window { Content = _workspace, Width = 1400, Height = 900, ShowInTaskbar = false };
            _host.Show(); Pump();
        }
        [TearDown]
        public void TearDown() { _host.Content = null; Pump(); _host.Close(); Pump(); }

        [TestCase(DockPosition.Left), TestCase(DockPosition.Right), TestCase(DockPosition.Top), TestCase(DockPosition.Bottom)]
        public void RelativeDockingSplitsTheTargetWithoutChangingItsRegion(DockPosition position)
        {
            _workspace.Dock(_second, _first, position); Pump();
            Assert.AreEqual(DockState.Document, _second.State);
            Assert.AreEqual(2, Views().Count());
            var first = _workspace.GroupBounds(_first); var second = _workspace.GroupBounds(_second);
            if (position == DockPosition.Left) Assert.Less(second.Right, first.Left);
            if (position == DockPosition.Right) Assert.Greater(second.Left, first.Right);
            if (position == DockPosition.Top) Assert.Less(second.Bottom, first.Top);
            if (position == DockPosition.Bottom) Assert.Greater(second.Top, first.Bottom);
            Assert.AreSame(_second, _workspace.ActiveItem);
        }

        [Test]
        public void ReferenceLayoutHasFourDocumentGroupsAndThreeStackedTools()
        {
            var three = Add("three"); var four = Add("four"); var five = Add("five");
            _workspace.Dock(five, _first, DockPosition.Right);
            _workspace.Dock(three, _first, DockPosition.Bottom);
            _workspace.Dock(four, five, DockPosition.Bottom);
            var properties = Add("properties", DockState.Right);
            var output = Add("output", DockState.Right);
            var explorer = Add("explorer", DockState.Right);
            _workspace.Dock(output, properties, DockPosition.Bottom, 2d / 3);
            _workspace.Dock(explorer, output, DockPosition.Bottom);
            Add("toolbox", DockState.Left); Pump();
            Assert.AreEqual(4, Views().Count(view => Equals(view.Tag, DockState.Document)));
            Assert.AreEqual(3, Views().Count(view => Equals(view.Tag, DockState.Right)));
            Assert.AreSame(ViewFor(_first), ViewFor(_second));
            var a = _workspace.GroupBounds(_first); var b = _workspace.GroupBounds(five);
            var c = _workspace.GroupBounds(three); var d = _workspace.GroupBounds(four);
            Assert.Less(a.Right, b.Left); Assert.Less(a.Bottom, c.Top); Assert.Less(b.Bottom, d.Top);
            Assert.AreEqual(a.Left, c.Left, 1); Assert.AreEqual(b.Left, d.Left, 1);
            Assert.AreEqual(a.Top, b.Top, 1); Assert.AreEqual(c.Top, d.Top, 1);
            Assert.Less(_workspace.GroupBounds(properties).Bottom, _workspace.GroupBounds(output).Top);
            Assert.Less(_workspace.GroupBounds(output).Bottom, _workspace.GroupBounds(explorer).Top);
            Assert.IsTrue(new[] { _first, _second, three, four, five }.All(item => item.State == DockState.Document));
        }

        [Test]
        public void MergingAcrossGroupsCollapsesAnEmptySplit()
        {
            _workspace.Dock(_second, _first, DockPosition.Right);
            _workspace.Dock(_second, _first); Pump();
            Assert.AreEqual(1, Views().Count());
            Assert.AreSame(ViewFor(_first), ViewFor(_second));
            Assert.IsInstanceOf<DockTabGroup>(Snapshot().Regions.Single(region => region.State == DockState.Document).Root);
            Assert.AreEqual("two text", ((TextBox)_second.Content).Text);
        }

        [Test]
        public void FloatingHidingAndAutoHideReturnToTheSameNestedToolGroup()
        {
            _workspace.Show(_first, DockState.Right); _workspace.Show(_second, DockState.Right);
            _workspace.Dock(_second, _first, DockPosition.Bottom); Pump();
            string group = GroupOf(Snapshot(), _second).Id;
            _workspace.Float(_second); Pump();
            Assert.AreEqual(1, Views().Count(view => Equals(view.Tag, DockState.Right)));
            _workspace.Pin(_second); Pump(); Assert.AreEqual(group, GroupOf(Snapshot(), _second).Id);
            Assert.Greater(_workspace.GroupBounds(_second).Top, _workspace.GroupBounds(_first).Top);
            _workspace.Hide(_second); Pump(); _workspace.Show(_second); Pump();
            Assert.AreEqual(group, GroupOf(Snapshot(), _second).Id);
            _workspace.AutoHide(_second); _workspace.OpenAutoHide(_second); Pump();
            _workspace.Pin(_second); Pump(); Assert.AreEqual(group, GroupOf(Snapshot(), _second).Id);
            _workspace.Remove(_second); Pump();
            Assert.IsInstanceOf<DockTabGroup>(Snapshot().Regions.Single(region => region.State == DockState.Right).Root);
        }

        [Test]
        public void SnapshotRestoresNestedStructureSelectionsAndSplitRatios()
        {
            var third = Add("third"); var fourth = Add("fourth");
            _workspace.Dock(_second, _first, DockPosition.Right, .35);
            _workspace.Dock(third, _first, DockPosition.Bottom, .4);
            _workspace.Dock(fourth, _second);
            _workspace.Activate(_second); _workspace.Activate(_first); Pump();
            var before = Snapshot();
            Assert.AreEqual(5, before.Version);
            var root = (DockSplit)before.Regions.Single(region => region.State == DockState.Document).Root;
            _workspace.Dock(_second, _first); _workspace.Hide(third); Restore(before); Pump();
            var restored = Snapshot();
            Assert.AreEqual(3, Views().Count());
            foreach (var group in before.Regions.SelectMany(region => Groups(region.Root)))
            {
                var match = restored.Regions.SelectMany(region => Groups(region.Root)).Single(candidate => candidate.Id == group.Id);
                CollectionAssert.AreEqual(group.ItemIds, match.ItemIds);
                Assert.AreEqual(group.SelectedId, match.SelectedId);
            }
            var afterRoot = (DockSplit)restored.Regions.Single(region => region.State == DockState.Document).Root;
            Assert.AreEqual(root.Ratio, afterRoot.Ratio, .01);
            Assert.AreEqual(_first, _workspace.ActiveItem);
            Assert.AreEqual("third text", ((TextBox)third.Content).Text);
        }

        [Test]
        public void SavingCapturesNestedSplitterResizing()
        {
            _workspace.Dock(_second, _first, DockPosition.Right); Pump();
            var grid = Descendants<Grid>(_workspace).Single(candidate => candidate.Tag is DockSplit);
            grid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
            grid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star); Pump();
            var layout = Snapshot();
            var split = (DockSplit)layout.Regions.Single(region => region.State == DockState.Document).Root;
            Assert.AreEqual(.75, split.Ratio, .01);
            _workspace.Dock(_second, _first); Restore(layout); Pump();
            Assert.AreEqual(3, _workspace.GroupBounds(_first).Width / _workspace.GroupBounds(_second).Width, .1);
        }

        [TestCase("duplicate-group"), TestCase("duplicate-item"), TestCase("missing-member"), TestCase("ratio"), TestCase("child"), TestCase("region"), TestCase("deep")]
        public void InvalidTreeSnapshotsDoNotChangeLiveWindows(string defect)
        {
            _workspace.Dock(_second, _first, DockPosition.Right); _workspace.Float(_second); Pump();
            var floating = _host.OwnedWindows[0];
            var layout = Snapshot();
            var region = layout.Regions.Single(entry => entry.State == DockState.Document);
            var split = (DockSplit)region.Root;
            var first = (DockTabGroup)split.First; var second = (DockTabGroup)split.Second;
            switch (defect)
            {
                case "duplicate-group": second.Id = first.Id; break;
                case "duplicate-item": second.ItemIds.Add(_first.Id); break;
                case "missing-member": second.ItemIds.Clear(); second.SelectedId = null; break;
                case "ratio": split.Ratio = double.NaN; break;
                case "child": split.First = null; break;
                case "region": region.State = DockState.Left; break;
                case "deep":
                    for (int index = 0; index < 34; index++) region.Root = new DockSplit { First = region.Root, Second = new DockTabGroup() };
                    break;
            }
            Assert.Throws<InvalidDataException>(() => Restore(layout));
            Assert.AreSame(floating, _host.OwnedWindows[0]);
            Assert.AreEqual(DockState.Float, _second.State);
            Assert.AreEqual(DockState.Document, _first.State);
        }

        [Test]
        public void UnknownPluginsArePrunedWhileRegisteredContentSurvives()
        {
            _workspace.Dock(_second, _first, DockPosition.Right);
            var snapshot = Snapshot();
            snapshot.Items.Single(item => item.Id == _second.Id).Id = "uninstalled";
            var group = GroupOf(snapshot, _second); group.ItemIds[0] = "uninstalled"; group.SelectedId = "uninstalled";
            Restore(snapshot); Pump();
            Assert.AreEqual(DockState.Hidden, _second.State);
            Assert.AreEqual(1, Views().Count());
            Assert.AreEqual("one text", ((TextBox)_first.Content).Text);
            _workspace.Show(_second); Pump(); Assert.AreSame(ViewFor(_first), ViewFor(_second));
        }

        [TestCase(1), TestCase(2)]
        public void LegacyLayoutsMigrateToOneGroupPerRegion(int version)
        {
            var layout = Snapshot(); layout.Version = version; layout.Regions.Clear();
            _workspace.Dock(_second, _first, DockPosition.Right);
            Restore(layout); Pump();
            Assert.AreSame(ViewFor(_first), ViewFor(_second));
            Assert.AreEqual(5, Snapshot().Version);
        }

        [Test]
        public void InvalidRelativeDockRequestsDoNotChangeTheTree()
        {
            Assert.Throws<ArgumentException>(() => _workspace.Dock(_first, _first));
            Assert.Throws<ArgumentOutOfRangeException>(() => _workspace.Dock(_first, _second, DockPosition.Left, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _workspace.Dock(_first, _second, (DockPosition)99));
            _workspace.Hide(_second);
            Assert.Throws<ArgumentException>(() => _workspace.Dock(_first, _second));
            Assert.AreEqual(DockState.Document, _first.State);
        }

        [Test]
        public void ClosingAGroupActivatesTheSelectedTabInASurvivingGroup()
        {
            var third = Add("third");
            _workspace.Dock(third, _first, DockPosition.Right);
            _workspace.Activate(_second); _workspace.Activate(third);
            _workspace.Hide(third); Pump();
            Assert.AreSame(_second, _workspace.ActiveItem);
            Assert.AreSame(_second, ((TabItem)ViewFor(_second).SelectedItem).Tag);
        }

        [Test]
        public void CentralGuidesSplitOnlyTheHoveredNestedGroup()
        {
            var third = Add("third"); var fourth = Add("fourth");
            _workspace.Dock(_second, _first, DockPosition.Right);
            _workspace.Dock(third, _first, DockPosition.Bottom); Pump();
            var untouched = _workspace.GroupBounds(_second);
            var target = _workspace.GroupBounds(third);
            var grid = ((Grid)_workspace.Content).Children.OfType<Grid>().Single();
            var guide = DockGuideGeometry.Create(grid.ActualWidth, grid.ActualHeight, target.X, target.Y, target.Width, target.Height, _workspace.GuideMetrics)
                .Single(tile => tile.State == DockState.Right && !tile.IsOuter);
            var point = new Point(guide.X + guide.Size / 2, guide.Y + guide.Size / 2);
            Assert.AreEqual(DockState.Right, _workspace.UpdateDockDrag(fourth, point)); Pump();
            var overlay = Descendants<DockGuideOverlay>(_workspace).Single();
            Assert.AreEqual(target, overlay.TargetBounds);
            var preview = grid.Children.OfType<Border>().Single();
            Assert.Greater(preview.Margin.Left, target.Left);
            Assert.LessOrEqual(preview.Margin.Left + preview.ActualWidth, target.Right);
            Assert.IsTrue(_workspace.CompleteDockDrag(fourth, point)); Pump();
            Assert.AreEqual(untouched, _workspace.GroupBounds(_second));
            Assert.Greater(_workspace.GroupBounds(fourth).Left, _workspace.GroupBounds(third).Right);
            Assert.AreEqual(target.Top, _workspace.GroupBounds(fourth).Top, 1);
            Assert.AreEqual(4, Views().Count());
        }

        [Test]
        public void CenterGuideMergesOnlyIntoTheHoveredGroup()
        {
            var third = Add("third");
            _workspace.Dock(_second, _first, DockPosition.Right);
            _workspace.Dock(third, _first, DockPosition.Bottom); Pump();
            var target = _workspace.GroupBounds(_second);
            Assert.IsTrue(_workspace.CompleteDockDrag(third, new Point(target.X + target.Width / 2, target.Y + target.Height / 2))); Pump();
            Assert.AreSame(ViewFor(third), ViewFor(_second));
            Assert.AreNotSame(ViewFor(_first), ViewFor(_second));
            Assert.AreEqual(2, Views().Count());
        }

        [Test]
        public void TabsCanMoveBetweenNestedGroupsWithoutChangingRegion()
        {
            var third = Add("third");
            _workspace.Dock(_second, _first, DockPosition.Right); Pump();
            _workspace.MoveBefore(third, _second); Pump();
            Assert.AreSame(ViewFor(third), ViewFor(_second));
            Assert.AreNotSame(ViewFor(third), ViewFor(_first));
            var order = ViewFor(third).Items.Cast<TabItem>().Select(tab => ((DockItem)tab.Tag).Id).ToArray();
            CollectionAssert.AreEqual(new[] { "third", "two" }, order);
        }
        [Test]
        public void RestoringAFloatingBranchKeepsTheSavedDockedTabActive()
        {
            _workspace.Dock(_second, _first, DockPosition.Right);
            _workspace.Float(_second); _workspace.Activate(_first); Pump();
            var saved = Snapshot();
            _workspace.Show(_second, DockState.Document);
            Restore(saved); Pump();
            Assert.AreSame(_first, _workspace.ActiveItem);
            Assert.AreEqual(DockState.Float, _second.State);
            Assert.AreEqual(1, _host.OwnedWindows.Count);
            Assert.IsTrue(((TextBox)_first.Content).IsKeyboardFocusWithin);
        }
        [TestCase(false), TestCase(true)]
        public void ReorderingTheSelectedTabKeepsItSelected(bool separateGroups)
        {
            if (separateGroups) _workspace.Dock(_second, _first, DockPosition.Right);
            _workspace.Activate(_second);
            _workspace.MoveBefore(_second, _first); Pump();
            Assert.AreSame(_second, _workspace.ActiveItem);
            Assert.AreSame(_second, ((TabItem)ViewFor(_second).SelectedItem).Tag);
            Assert.AreSame(ViewFor(_first), ViewFor(_second));
        }

        [TestCase(DockState.Hidden), TestCase(DockState.Float), TestCase(DockState.AutoHide)]
        public void TabReorderingRejectsItemsWithoutVisibleDockGroups(DockState state)
        {
            _workspace.Show(_first, DockState.Right); _workspace.Show(_second, DockState.Right);
            _workspace.Show(_first, state); _workspace.Show(_second, state); Pump();
            Assert.Throws<ArgumentException>(() => _workspace.MoveBefore(_first, _second));
            var saved = Snapshot(); Restore(saved); Pump();
            Assert.AreEqual(state, _first.State); Assert.AreEqual(state, _second.State);
        }
        private DockItem Add(string id, DockState state = DockState.Document)
        { var item = new DockItem(id, id, new TextBox { Text = id + " text" }); _workspace.Add(item, state); return item; }
        private IEnumerable<TabControl> Views() => Descendants<TabControl>(_workspace);
        private TabControl ViewFor(DockItem item) => Views().Single(view => view.Items.Cast<TabItem>().Any(tab => tab.Tag == item));
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            { if (child is T value) yield return value; foreach (var next in Descendants<T>(child)) yield return next; }
        }
        private static IEnumerable<DockTabGroup> Groups(DockLayoutNode node)
        {
            if (node is DockTabGroup group) yield return group;
            if (node is DockSplit split) foreach (var childGroup in Groups(split.First).Concat(Groups(split.Second))) yield return childGroup;
        }
        private static DockTabGroup GroupOf(DockLayout layout, DockItem item) => layout.Regions.SelectMany(region => Groups(region.Root)).Single(group => group.ItemIds.Contains(item.Id));
        private DockLayout Snapshot()
        { using (var stream = new MemoryStream()) { _workspace.SaveLayout(stream); stream.Position = 0; return (DockLayout)new XmlSerializer(typeof(DockLayout)).Deserialize(stream); } }
        private void Restore(DockLayout layout)
        { using (var stream = new MemoryStream()) { new XmlSerializer(typeof(DockLayout)).Serialize(stream, layout); stream.Position = 0; _workspace.LoadLayout(stream); } }
        private static void Pump()
        { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    }
}
