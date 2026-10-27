using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class OuterRegionTests
    {
        private DockWorkspace _workspace;
        private Window _host;
        private DockItem _document;
        private Grid Surface => ((Grid)_workspace.Content).Children.OfType<Grid>().Single();
        private static readonly DockState[] Edges = { DockState.Left, DockState.Right, DockState.Top, DockState.Bottom };

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace(); _document = Add("document", DockState.Document);
            _host = new Window { Content = _workspace, Width = 1200, Height = 820, ShowInTaskbar = false };
            _host.Show(); Pump();
        }
        [TearDown]
        public void TearDown() { _host.Content = null; Pump(); _host.Close(); Pump(); }

        private static IEnumerable<TestCaseData> Orders()
        {
            foreach (var a in Edges)
                foreach (var b in Edges.Where(state => state != a))
                    foreach (var c in Edges.Where(state => state != a && state != b))
                    {
                        var order = new[] { a, b, c, Edges.Single(state => state != a && state != b && state != c) };
                        yield return new TestCaseData(new object[] { order }).SetName("OuterOrder_" + string.Join("_", order));
                    }
        }

        [TestCaseSource(nameof(Orders))]
        public void FirstUseOrderAssignsEveryCornerAndSplitterToTheOuterRegion(DockState[] order)
        {
            var items = order.ToDictionary(edge => edge, edge => Add(edge.ToString(), edge)); Pump();
            CollectionAssert.AreEqual(order, _workspace.RegionOrder);
            var remaining = new Rect(0, 0, Surface.ActualWidth, Surface.ActualHeight);
            double splitter = ((GridLength)_workspace.FindResource("Dps.SplitterLength")).Value;
            foreach (var edge in order)
            {
                var bounds = _workspace.GroupBounds(items[edge]);
                var bar = Surface.Children.OfType<GridSplitter>().Single(element => element.ResizeDirection ==
                    (edge == DockState.Left || edge == DockState.Right ? GridResizeDirection.Columns : GridResizeDirection.Rows) &&
                    (edge == DockState.Left ? Grid.GetColumn(element) == 1 : edge == DockState.Right ? Grid.GetColumn(element) == 3 :
                     edge == DockState.Top ? Grid.GetRow(element) == 1 : Grid.GetRow(element) == 3));
                if (edge == DockState.Left || edge == DockState.Right)
                {
                    Assert.AreEqual(remaining.Top, bounds.Top, 1); Assert.AreEqual(remaining.Height, bounds.Height, 1);
                    Assert.AreEqual(bounds.Height, bar.ActualHeight, 1);
                    if (edge == DockState.Left) { Assert.AreEqual(remaining.Left, bounds.Left, 1); remaining.X += bounds.Width + splitter; }
                    else Assert.AreEqual(remaining.Right, bounds.Right, 1);
                    remaining.Width -= bounds.Width + splitter;
                }
                else
                {
                    Assert.AreEqual(remaining.Left, bounds.Left, 1); Assert.AreEqual(remaining.Width, bounds.Width, 1);
                    Assert.AreEqual(bounds.Width, bar.ActualWidth, 1);
                    if (edge == DockState.Top) { Assert.AreEqual(remaining.Top, bounds.Top, 1); remaining.Y += bounds.Height + splitter; }
                    else Assert.AreEqual(remaining.Bottom, bounds.Bottom, 1);
                    remaining.Height -= bounds.Height + splitter;
                }
            }
            AssertRect(remaining, _workspace.GroupBounds(_document));
            foreach (var pair in items)
                foreach (var other in items.Where(other => other.Key != pair.Key))
                {
                    var overlap = Rect.Intersect(_workspace.GroupBounds(pair.Value), _workspace.GroupBounds(other.Value));
                    Assert.IsTrue(overlap.IsEmpty || overlap.Width == 0 || overlap.Height == 0);
                }
        }

        [TestCase(true), TestCase(false)]
        public void SavedOrderOverridesRegistrationOrderAndPreservesSizesAndNestedGroups(bool leftFirst)
        {
            var left = Add("left", leftFirst ? DockState.Left : DockState.Bottom);
            var bottom = Add("bottom", leftFirst ? DockState.Bottom : DockState.Left);
            var extra = Add("extra", left.State); _workspace.Dock(extra, left, DockPosition.Bottom);
            Surface.ColumnDefinitions[0].Width = new GridLength(250);
            Surface.RowDefinitions[4].Height = new GridLength(180); Pump();
            var snapshot = Snapshot();
            var firstBounds = _workspace.GroupBounds(left); var secondBounds = _workspace.GroupBounds(bottom);
            _workspace.Remove(extra); _workspace.Remove(left); _workspace.Remove(bottom); Pump();
            var replacement = new DockWorkspace();
            _host.Content = replacement; _workspace = replacement;
            _document = Add("document", DockState.Document);
            bottom = Add("bottom", bottom.State); left = Add("left", left.State); extra = Add("extra", DockState.Document);
            Restore(snapshot); Pump();
            Assert.AreEqual(5, Snapshot().Version);
            CollectionAssert.AreEqual(snapshot.RegionOrder, _workspace.RegionOrder);
            AssertRect(firstBounds, _workspace.GroupBounds(left)); AssertRect(secondBounds, _workspace.GroupBounds(bottom));
            Assert.Less(_workspace.GroupBounds(left).Bottom, _workspace.GroupBounds(extra).Top);
            Assert.AreEqual(250, Snapshot().LeftWidth, 1); Assert.AreEqual(180, Snapshot().BottomHeight, 1);
        }

        [Test]
        public void ExtraTabsHiddenFloatingAndRemovedPanesKeepTheirRegionPriority()
        {
            var left = Add("left", DockState.Left); var bottom = Add("bottom", DockState.Bottom);
            var extra = Add("extra", DockState.Left); Pump();
            var order = _workspace.RegionOrder.ToArray();
            _workspace.AutoHideGroup(left); Pump();
            Assert.AreEqual(0, _workspace.GroupBounds(bottom).Left, 1);
            _workspace.PinGroup(left); Pump(); Assert.Greater(_workspace.GroupBounds(bottom).Left, 0);
            _workspace.Float(left); _workspace.Float(extra); Pump();
            _workspace.Pin(left); Pump(); Assert.AreEqual(Surface.ActualHeight, _workspace.GroupBounds(left).Height, 1);
            _workspace.Remove(left); _workspace.Remove(extra); _workspace.Show(bottom, DockState.Hidden); Pump();
            var saved = Snapshot(); Restore(saved); Pump();
            var newLeft = Add("new-left", DockState.Left); _workspace.Show(bottom); Pump();
            CollectionAssert.AreEqual(order, _workspace.RegionOrder);
            Assert.AreEqual(Surface.ActualHeight, _workspace.GroupBounds(newLeft).Height, 1);
        }

        [TestCase(1), TestCase(2), TestCase(3), TestCase(4)]
        public void OlderSnapshotsRetainTheirOriginalHorizontalOuterRegions(int version)
        {
            var left = Add("left", DockState.Left); var bottom = Add("bottom", DockState.Bottom); Pump();
            var saved = Snapshot(); saved.Version = version;
            saved.RegionOrder = new List<DockState> { DockState.Left, DockState.Bottom };
            if (version < 3) saved.Regions.Clear();
            Restore(saved); Pump();
            CollectionAssert.AreEqual(new[] { DockState.Top, DockState.Bottom, DockState.Left, DockState.Right }, _workspace.RegionOrder);
            Assert.AreEqual(Surface.ActualWidth, _workspace.GroupBounds(bottom).Width, 1);
            Assert.Less(_workspace.GroupBounds(left).Bottom, _workspace.GroupBounds(bottom).Top);
            Assert.AreEqual(5, Snapshot().Version);
        }

        [TestCase("duplicate"), TestCase("non-edge"), TestCase("missing"), TestCase("null")]
        public void InvalidOuterOrdersDoNotChangeTheLiveLayoutOrFloatingWindows(string defect)
        {
            Add("left", DockState.Left); Add("bottom", DockState.Bottom); _workspace.Float(_document); Pump();
            var floating = _host.OwnedWindows.Cast<Window>().Single();
            var invalid = Snapshot(); var before = Serialize(invalid);
            if (defect == "duplicate") invalid.RegionOrder.Add(DockState.Left);
            if (defect == "non-edge") invalid.RegionOrder.Add(DockState.Document);
            if (defect == "missing") invalid.RegionOrder.Remove(DockState.Left);
            if (defect == "null") invalid.RegionOrder = null;
            Assert.Throws<InvalidDataException>(() => Restore(invalid));
            Assert.AreEqual(before, Serialize(Snapshot()));
            Assert.AreSame(floating, _host.OwnedWindows.Cast<Window>().Single()); Assert.IsTrue(floating.IsVisible);
        }

        [TestCase(DockState.Left, DockState.Bottom), TestCase(DockState.Bottom, DockState.Left),
         TestCase(DockState.Right, DockState.Top), TestCase(DockState.Top, DockState.Right)]
        public void OuterPreviewMatchesTheNewRegionAfterDropping(DockState existing, DockState destination)
        {
            Add("existing", existing); Pump();
            var tile = DockGuideGeometry.Create(Surface.ActualWidth, Surface.ActualHeight, _workspace.GuideMetrics)
                .Single(guide => guide.IsOuter && guide.State == destination);
            var point = new Point(tile.X + tile.Size / 2, tile.Y + tile.Size / 2);
            Assert.AreEqual(destination, _workspace.UpdateDockDrag(_document, point)); Pump();
            var preview = Surface.Children.OfType<Border>().Single();
            var inset = (Thickness)_workspace.FindResource("Dps.DropPreviewInset");
            var expected = new Rect(preview.Margin.Left - inset.Left, preview.Margin.Top - inset.Top,
                preview.Width + inset.Left + inset.Right, preview.Height + inset.Top + inset.Bottom);
            Assert.IsTrue(_workspace.CompleteDockDrag(_document, point)); Pump();
            AssertRect(expected, _workspace.GroupBounds(_document));
            CollectionAssert.AreEqual(new[] { existing, destination }, _workspace.RegionOrder);
        }

        [Test]
        public void MovingTheOnlySourcePaneToAnotherOuterEdgePredictsTheReleasedSpace()
        {
            var left = Add("left", DockState.Left); Pump();
            var tile = DockGuideGeometry.Create(Surface.ActualWidth, Surface.ActualHeight, _workspace.GuideMetrics)
                .Single(guide => guide.IsOuter && guide.State == DockState.Bottom);
            var point = new Point(tile.X + tile.Size / 2, tile.Y + tile.Size / 2);
            _workspace.UpdateDockDrag(left, point); Pump();
            Assert.AreEqual(Surface.ActualWidth - 4, Surface.Children.OfType<Border>().Single().Width, 1);
            Assert.IsTrue(_workspace.CompleteDockDrag(left, point)); Pump();
            Assert.AreEqual(Surface.ActualWidth, _workspace.GroupBounds(left).Width, 1);
        }

        [TestCase(true), TestCase(false)]
        public void RegionSplittersResizeTheirOwnTracksAndPersistTheirNewSizes(bool leftFirst)
        {
            if (leftFirst) Add("left", DockState.Left);
            Add("bottom", DockState.Bottom);
            if (!leftFirst) Add("left", DockState.Left);
            Pump();
            var left = _workspace.Items.Single(item => item.Id == "left");
            var splitter = Surface.Children.OfType<GridSplitter>().Single(bar => Grid.GetColumn(bar) == 1);
            double before = _workspace.GroupBounds(left).Width;
            splitter.Focus();
            splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(splitter), 0, Key.Right)
                { RoutedEvent = Keyboard.KeyDownEvent });
            Pump();
            double after = _workspace.GroupBounds(left).Width;
            Assert.Greater(after, before);
            var saved = Snapshot(); Restore(saved); Pump();
            Assert.AreEqual(after, _workspace.GroupBounds(left).Width, 1);
            Assert.AreEqual(after, saved.LeftWidth, 1);
            CollectionAssert.AreEqual(leftFirst ? new[] { DockState.Left, DockState.Bottom } : new[] { DockState.Bottom, DockState.Left }, _workspace.RegionOrder);
        }
        private DockItem Add(string id, DockState state)
        { var item = new DockItem(id, id, new TextBox { Text = id }); _workspace.Add(item, state); return item; }
        private DockLayout Snapshot()
        { using (var stream = new MemoryStream()) { _workspace.SaveLayout(stream); stream.Position = 0; return (DockLayout)new XmlSerializer(typeof(DockLayout)).Deserialize(stream); } }
        private void Restore(DockLayout layout)
        { using (var stream = new MemoryStream()) { new XmlSerializer(typeof(DockLayout)).Serialize(stream, layout); stream.Position = 0; _workspace.LoadLayout(stream); } }
        private static string Serialize(DockLayout layout)
        { using (var writer = new StringWriter()) { new XmlSerializer(typeof(DockLayout)).Serialize(writer, layout); return writer.ToString(); } }
        private static void AssertRect(Rect expected, Rect actual)
        { Assert.AreEqual(expected.X, actual.X, 1); Assert.AreEqual(expected.Y, actual.Y, 1); Assert.AreEqual(expected.Width, actual.Width, 1); Assert.AreEqual(expected.Height, actual.Height, 1); }
        private static void Pump()
        { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    }
}
