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
    public class DockPolicyTests
    {
        private DockWorkspace _workspace;
        private DockItem _editor, _peer, _tool;
        private Window _host;
        private Grid Surface => ((Grid)_workspace.Content).Children.OfType<Grid>().Single();

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _editor = new DockItem("editor", "Editor", new TextBox())
            {
                CanAutoHide = false, CanDockToSides = false, CanClose = false, ShowCloseButton = false
            };
            _peer = new DockItem("peer", "Peer", new TextBox());
            _tool = new DockItem("tool", "Tool", new TextBox());
            _workspace.Add(_editor); _workspace.Add(_peer); _workspace.Add(_tool, DockState.Right);
            _workspace.Activate(_editor);
            _host = new Window { Content = _workspace, Width = 1000, Height = 700, ShowInTaskbar = false };
            _host.Show(); Pump();
        }

        [TearDown]
        public void TearDown() { _host.Content = null; Pump(); _host.Close(); Pump(); }

        [Test]
        public void DocumentHeadersHaveNoPinAndProtectedEditorHasNoCollapseMenuEvenWhenFloating()
        {
            foreach (var item in new[] { _editor, _peer })
            {
                var header = Descendants<DockItemHeader>(_workspace).Single(h => h.Item == item);
                Assert.AreEqual(Visibility.Collapsed, Part<Button>(header, "Pin").Visibility);
            }
            var toolHeader = Descendants<DockItemHeader>(_workspace).Single(h => h.Item == _tool && h.IsToolPaneHeader);
            Assert.AreEqual(Visibility.Visible, Part<Button>(toolHeader, "Pin").Visibility);
            AssertProtectedMenu();
            _workspace.Float(_editor); Pump();
            var floating = _host.OwnedWindows.Cast<Window>().Single();
            var floatingHeader = Descendants<DockItemHeader>(floating).Single();
            Assert.AreEqual(Visibility.Collapsed, Part<Button>(floatingHeader, "Pin").Visibility);
            Assert.IsFalse(DockItemHeader.TogglePinCommand.CanExecute(null, floatingHeader));
            AssertProtectedMenu(floating);
            _workspace.Pin(_editor); Pump();
            Assert.AreEqual(DockState.Document, _editor.State);
        }

        [TestCase(DockState.Left, DockPosition.Left), TestCase(DockState.Right, DockPosition.Right),
         TestCase(DockState.Top, DockPosition.Top), TestCase(DockState.Bottom, DockPosition.Bottom)]
        public void SideAndCollapseApisRejectBeforeChangingThePane(DockState side, DockPosition position)
        {
            var before = Snapshot();
            Assert.Throws<InvalidOperationException>(() => _workspace.Show(_editor, side));
            Assert.Throws<InvalidOperationException>(() => _workspace.Dock(_editor, _peer, position));
            Assert.Throws<InvalidOperationException>(() => _workspace.Dock(_editor, _tool));
            Assert.Throws<InvalidOperationException>(() => _workspace.AutoHide(_editor));
            Assert.Throws<InvalidOperationException>(() => _workspace.AutoHide(_editor, side));
            Assert.Throws<InvalidOperationException>(() => _workspace.AutoHideGroup(_editor));
            Assert.AreEqual(before, Snapshot());
            Assert.AreEqual(DockState.Document, _editor.LastDockState);
            // Other panes may still split around the editor, as required by the initial layout.
            _workspace.Dock(_peer, _editor, position);
            Assert.AreEqual(DockState.Document, _editor.State);
        }

        [Test]
        public void GroupCollapseCannotHideAProtectedPeer()
        {
            Assert.Throws<InvalidOperationException>(() => _workspace.AutoHideGroup(_peer));
            Assert.AreEqual(DockState.Document, _editor.State);
            Assert.AreEqual(DockState.Document, _peer.State);
            _workspace.AutoHideGroup(_tool); Pump();
            Assert.AreEqual(DockState.AutoHide, _tool.State);
            _workspace.PinGroup(_tool); Pump();
            Assert.AreEqual(DockState.Right, _tool.State);
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top), TestCase(DockState.Bottom)]
        public void DragGuidesAndDropsDisallowSideSplitsAndEdgesButAllowDocumentTabs(DockState side)
        {
            _workspace.Float(_editor); Pump();
            var bounds = _workspace.GroupBounds(_peer);
            var geometry = DockGuideGeometry.Create(Surface.ActualWidth, Surface.ActualHeight,
                bounds.X, bounds.Y, bounds.Width, bounds.Height, _workspace.GuideMetrics);
            foreach (var target in geometry.Where(target => target.State == side))
            {
                var point = new Point(target.X + target.Size / 2, target.Y + target.Size / 2);
                Assert.IsNull(_workspace.UpdateDockDrag(_editor, point));
                var overlay = Surface.Children.OfType<DockGuideOverlay>().Single();
                Assert.IsTrue(overlay.Targets.All(t => t.State == DockState.Document));
                Assert.IsTrue(overlay.Children.OfType<DockGuideTile>()
                    .Where(tile => tile.Visibility == Visibility.Visible).All(tile => tile.State == DockState.Document));
                Assert.IsFalse(_workspace.CompleteDockDrag(_editor, point));
                Assert.AreEqual(DockState.Float, _editor.State);
            }
            var center = geometry.Single(target => target.State == DockState.Document);
            Assert.IsTrue(_workspace.CompleteDockDrag(_editor,
                new Point(center.X + center.Size / 2, center.Y + center.Size / 2))); Pump();
            Assert.AreEqual(DockState.Document, _editor.State);
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top),
         TestCase(DockState.Bottom), TestCase(DockState.AutoHide)]
        public void LayoutRestoreCannotBypassTheRuntimePanePolicy(DockState state)
        {
            _editor.CanAutoHide = true; _editor.CanDockToSides = true;
            _workspace.Show(_editor, state); Pump();
            var forbidden = Snapshot();
            _workspace.Show(_editor, DockState.Document); Pump();
            _editor.CanAutoHide = false; _editor.CanDockToSides = false;
            var before = Snapshot();
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(forbidden)))
                Assert.Throws<InvalidDataException>(() => _workspace.LoadLayout(stream));
            Assert.AreEqual(before, Snapshot());
            Assert.AreEqual(DockState.Document, _editor.State);
        }

        private void AssertProtectedMenu(DependencyObject root = null)
        {
            var header = Descendants<DockItemHeader>(root ?? _workspace).Single(h => h.Item == _editor);
            header.ContextMenu.PlacementTarget = header;
            header.ContextMenu.IsOpen = true; Pump();
            try
            {
                var commands = header.ContextMenu.Items.OfType<MenuItem>().ToArray();
                Assert.AreEqual(4, commands.Count(command => !command.IsEnabled));
                Assert.IsFalse(commands.Any(command => command.Command == DockItemHeader.TogglePinCommand));
                Assert.IsTrue(commands.Single(command => ((string)command.Header).Contains("Document")).IsEnabled);
                Assert.IsTrue(commands.Single(command => ((string)command.Header).Contains("Float")).IsEnabled);
            }
            finally { header.ContextMenu.IsOpen = false; Pump(); }
        }
        private string Snapshot()
        {
            using (var stream = new MemoryStream())
            {
                _workspace.SaveLayout(stream);
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        private static T Part<T>(DockItemHeader header, string name) where T : FrameworkElement =>
            (T)header.Template.FindName(name, header);
        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
    }
}
