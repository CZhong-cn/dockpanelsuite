using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using DockPanelSuite.Wpf;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class WorkspaceTests
    {
        private DockWorkspace _workspace;
        private DockItem _item;
        private Window _host;

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _item = new DockItem("editor", "Editor", new TextBox { Text = "keep me" });
            _workspace.Add(_item);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) { _host.Content = null; Pump(); _host.Close(); _host = null; }
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top), TestCase(DockState.Bottom), TestCase(DockState.Document)]
        public void MovingContentPreservesInstanceAndText(DockState state)
        {
            _workspace.Show(_item, state);
            Assert.AreEqual(state, _item.State);
            Assert.AreEqual("keep me", ((TextBox)_item.Content).Text);
            Assert.AreSame(_item, _workspace.ActiveItem);
            _workspace.Hide(_item);
            _workspace.Show(_item);
            Assert.AreEqual(state, _item.State);
        }

        [Test]
        public void DuplicateIdsAndCrossWorkspaceOwnershipAreRejected()
        {
            Assert.Throws<ArgumentException>(() => _workspace.Add(new DockItem("editor", "Duplicate", new TextBox())));
            Assert.Throws<ArgumentException>(() => new DockWorkspace().Add(_item));
            Assert.Throws<ArgumentException>(() => _workspace.Add(new DockItem("shared", "Shared", _item.Content)));
            Assert.Throws<ArgumentException>(() => new DockWorkspace().Show(_item, DockState.Left));
            Assert.Throws<ArgumentOutOfRangeException>(() => _workspace.Show(_item, (DockState)999));
            Assert.AreEqual(DockState.Document, _item.State);
        }

        [Test]
        public void RemoveDetachesContentAndAllowsAnotherWorkspaceToOwnIt()
        {
            _workspace.Remove(_item);
            var other = new DockWorkspace();
            other.Add(_item, DockState.Right);
            Assert.IsEmpty(_workspace.Items);
            Assert.AreEqual(DockState.Right, _item.State);
        }

        [Test]
        public void ClosePolicyIsHonored()
        {
            _item.CanClose = false;
            _workspace.Hide(_item);
            Assert.AreEqual(DockState.Document, _item.State);
            _item.CanClose = true;
            _workspace.Hide(_item);
            Assert.AreEqual(DockState.Hidden, _item.State);
        }

        [Test]
        public void SnapshotRestoresHiddenAndFloatingStatesAndDoesNotSerializeContent()
        {
            var hidden = new DockItem("hidden", "Hidden", new Button());
            _workspace.Add(hidden, DockState.Left);
            _workspace.Hide(hidden);
            _workspace.Float(_item, new Rect(120, 140, 450, 300));
            using (var stream = new MemoryStream())
            {
                _workspace.SaveLayout(stream);
                Assert.IsTrue(stream.CanWrite);
                _workspace.Show(_item, DockState.Bottom);
                _workspace.Show(hidden);
                stream.Position = 0;
                _workspace.LoadLayout(stream);
                Assert.IsTrue(stream.CanRead);
                Assert.AreEqual(DockState.Float, _item.State);
                Assert.AreEqual(new Rect(120, 140, 450, 300), _item.FloatingBounds);
                Assert.AreEqual(DockState.Hidden, hidden.State);
                _workspace.Show(hidden);
                Assert.AreEqual(DockState.Left, hidden.State);
                Assert.AreEqual("keep me", ((TextBox)_item.Content).Text);
            }
        }

        [Test]
        public void RestoreSkipsUnknownIdsAndHidesItemsMissingFromSnapshot()
        {
            var layout = Snapshot();
            layout.Version = 2; // Legacy snapshots have no tree references.
            layout.Items[0].Id = "uninstalled-plugin";
            Restore(layout);
            Assert.AreEqual(DockState.Hidden, _item.State);
            Assert.AreEqual(1, _workspace.Items.Count);
        }

        [TestCase("duplicate"), TestCase("version"), TestCase("bounds"), TestCase("previous"), TestCase("size"), TestCase("autoHideEdge")]
        public void InvalidSnapshotsDoNotChangeLiveState(string defect)
        {
            var layout = Snapshot();
            switch (defect)
            {
                case "duplicate": layout.Items.Add(layout.Items[0]); break;
                case "version": layout.Version = 99; break;
                case "bounds": layout.Items[0].Width = double.NaN; break;
                case "previous": layout.Items[0].LastDockState = DockState.Float; break;
                case "size": layout.LeftWidth = -1; break;
                case "autoHideEdge": layout.Items[0].AutoHideEdge = DockState.Document; break;
            }
            Assert.Throws<InvalidDataException>(() => Restore(layout));
            Assert.AreEqual(DockState.Document, _item.State);
        }

        [Test]
        public void SelectedDocumentIsRestored()
        {
            var second = new DockItem("second", "Second", new TextBox());
            _workspace.Add(second);
            _workspace.Float(_item);
            _workspace.Show(_item, DockState.Document);
            _workspace.Show(second, DockState.Document);
            var saved = Snapshot();
            _workspace.Show(_item, DockState.Document);
            Restore(saved);
            var tab = Descendants<TabControl>(_workspace).Single();
            Assert.AreSame(second, ((TabItem)tab.SelectedItem).Tag);
        }

        [Test]
        public void FloatingDockingAndClosingReleaseLogicalParents()
        {
            ShowHost();
            _workspace.Float(_item);
            Pump();
            Assert.AreEqual(1, _host.OwnedWindows.Count);
            _workspace.Show(_item, DockState.Right);
            Pump();
            Assert.AreEqual(0, _host.OwnedWindows.Count);
            _workspace.Float(_item);
            Pump();
            _host.OwnedWindows[0].Close();
            Pump();
            Assert.AreEqual(DockState.Hidden, _item.State);
            _workspace.Show(_item);
            Assert.AreEqual(DockState.Right, _item.State);
            Assert.AreEqual("keep me", ((TextBox)_item.Content).Text);
        }

        [Test]
        public void RebuildingWhileFloatingAndRemovingDoesNotDoubleParentContent()
        {
            ShowHost();
            _workspace.Float(_item);
            _workspace.Add(new DockItem("new", "New", new Button()), DockState.Left);
            Pump();
            Assert.AreEqual(1, _host.OwnedWindows.Count);
            _workspace.Remove(_item);
            Pump();
            Assert.AreEqual(0, _host.OwnedWindows.Count);
            new DockWorkspace().Add(_item);
        }

        [Test]
        public void UnloadingClosesFloatsAndReloadingRecreatesThem()
        {
            ShowHost();
            _workspace.Float(_item);
            Pump();
            _host.Content = null;
            Pump();
            Assert.AreEqual(0, _host.OwnedWindows.Count);
            Assert.AreEqual(DockState.Float, _item.State);
            _host.Content = _workspace;
            Pump();
            Assert.AreEqual(1, _host.OwnedWindows.Count);
        }

        private void ShowHost()
        {
            _host = new Window { Content = _workspace, Width = 800, Height = 600, ShowInTaskbar = false };
            _host.Show();
            Pump();
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top), TestCase(DockState.Bottom)]
        public void AutoHiddenPaneCanOpenDismissAndPinWithoutLosingContent(DockState edge)
        {
            _workspace.AutoHide(_item, edge);
            Assert.AreEqual(DockState.AutoHide, _item.State);
            _workspace.OpenAutoHide(_item);
            Assert.AreSame(_item, _workspace.OpenAutoHideItem);
            Assert.AreSame(_item, _workspace.ActiveItem);
            _workspace.DismissAutoHide();
            Assert.IsNull(_workspace.OpenAutoHideItem);
            Assert.AreEqual(DockState.AutoHide, _item.State);
            _workspace.Pin(_item);
            Assert.AreEqual(edge, _item.State);
            Assert.AreEqual("keep me", ((TextBox)_item.Content).Text);
        }

        [Test]
        public void InvalidAutoHideDoesNotClaimOrRegisterTheItem()
        {
            var other = new DockItem("other", "Other", new TextBox());
            Assert.Throws<ArgumentOutOfRangeException>(() => _workspace.Add(other, (DockState)999));
            Assert.AreEqual(1, _workspace.Items.Count);
            _workspace.Add(other);
            Assert.Throws<ArgumentException>(() => _workspace.AutoHide(other, DockState.Document));
            Assert.AreEqual(DockState.Document, other.State);
        }

        [Test]
        public void AutoHideAndTabOrderRoundTripThroughLayout()
        {
            var second = new DockItem("second", "Second", new TextBox());
            _workspace.Add(second);
            var tool = new DockItem("tool", "Tool", new TextBox());
            _workspace.Add(tool, DockState.Left);
            _workspace.AutoHide(tool);
            _workspace.MoveBefore(second, _item);
            _workspace.Activate(second);
            var saved = Snapshot();
            _workspace.Pin(tool);
            _workspace.MoveBefore(_item, second);
            Restore(saved);
            Assert.AreEqual(DockState.AutoHide, tool.State);
            Assert.AreEqual(DockState.Left, tool.LastDockState);
            Assert.AreEqual("second", _workspace.Items[0].Id);
            Assert.AreSame(second, _workspace.ActiveItem);
            Assert.IsNull(_workspace.OpenAutoHideItem);
        }

        [Test]
        public void MoveBeforePreservesRelativeOrderAndRejectsDifferentRegions()
        {
            var second = new DockItem("second", "Second", new Button());
            var third = new DockItem("third", "Third", new Button());
            _workspace.Add(second); _workspace.Add(third);
            _workspace.MoveBefore(_item, third);
            CollectionAssert.AreEqual(new[] { "second", "editor", "third" }, _workspace.Items.Select(item => item.Id));
            _workspace.MoveBefore(third, second);
            CollectionAssert.AreEqual(new[] { "third", "second", "editor" }, _workspace.Items.Select(item => item.Id));
            _workspace.Show(second, DockState.Left);
            Assert.Throws<ArgumentException>(() => _workspace.MoveBefore(third, second));
        }

        [Test]
        public void DocumentCyclingWrapsAndSkipsToolsAndHiddenDocuments()
        {
            var second = new DockItem("second", "Second", new TextBox());
            _workspace.Add(second);
            _workspace.Add(new DockItem("tool", "Tool", new Button()), DockState.Right);
            _workspace.Activate(_item);
            _workspace.ActivateNextDocument(); Assert.AreSame(second, _workspace.ActiveItem);
            _workspace.ActivateNextDocument(); Assert.AreSame(_item, _workspace.ActiveItem);
            _workspace.ActivateNextDocument(true); Assert.AreSame(second, _workspace.ActiveItem);
            _workspace.Hide(second);
            _workspace.ActivateNextDocument(); Assert.AreSame(_item, _workspace.ActiveItem);
        }

        [Test]
        public void CancelableClosingAndActiveItemNotificationsWork()
        {
            var second = new DockItem("second", "Second", new Button());
            _workspace.Add(second);
            int notifications = 0;
            _workspace.ActiveItemChanged += (sender, args) => notifications++;
            EventHandler<DockItemClosingEventArgs> cancel = (sender, args) => args.Cancel = true;
            _workspace.ItemClosing += cancel;
            _workspace.Hide(second);
            Assert.AreEqual(DockState.Document, second.State);
            _workspace.ItemClosing -= cancel;
            _workspace.Remove(second);
            Assert.AreSame(_item, _workspace.ActiveItem);
            Assert.Greater(notifications, 0);
        }

        [Test]
        public void ThemeChangesKeepContentAndOpenFloatingWindows()
        {
            ShowHost();
            _workspace.Float(_item); Pump();
            var window = _host.OwnedWindows[0];
            var before = _workspace.FindResource("Dps.Surface");
            _workspace.Theme = DockTheme.Dark;
            Assert.AreNotEqual(before, _workspace.FindResource("Dps.Surface"));
            Assert.AreSame(_workspace.FindResource("Dps.Surface"), window.FindResource("Dps.Surface"));
            Assert.AreSame(window, _host.OwnedWindows[0]);
            Assert.AreEqual("keep me", ((TextBox)_item.Content).Text);
            Assert.IsInstanceOf<Style>(_workspace.FindResource("Dps.TabItem"));
        }

        [Test]
        public void LegacyLayoutVersionOneCanStillBeRestored()
        {
            var layout = Snapshot(); layout.Version = 1; layout.ActiveId = null;
            _workspace.Show(_item, DockState.Right);
            Restore(layout);
            Assert.AreEqual(DockState.Document, _item.State);
        }

        [Test]
        public void FloatingWindowCloseCanBeCanceled()
        {
            ShowHost(); _workspace.Float(_item); Pump();
            EventHandler<DockItemClosingEventArgs> cancel = (sender, args) => args.Cancel = true;
            _workspace.ItemClosing += cancel;
            _host.OwnedWindows[0].Close(); Pump();
            Assert.AreEqual(DockState.Float, _item.State);
            Assert.AreEqual(1, _host.OwnedWindows.Count);
            _workspace.ItemClosing -= cancel;
        }

        [Test]
        public void KeyboardFocusInPaneContentChangesTheActiveItem()
        {
            ShowHost();
            var tool = new DockItem("tool", "Tool", new TextBox());
            _workspace.Add(tool, DockState.Right); Pump();
            System.Windows.Input.Keyboard.Focus((TextBox)_item.Content); Pump();
            Assert.AreSame(_item, _workspace.ActiveItem);
            System.Windows.Input.Keyboard.Focus((TextBox)tool.Content); Pump();
            Assert.AreSame(tool, _workspace.ActiveItem);
        }

        [Test]
        public void ClosingAFloatingWindowPreservesItsLatestBounds()
        {
            ShowHost();
            _workspace.Float(_item); Pump();
            var window = _host.OwnedWindows[0];
            window.Left = 160; window.Top = 180;
            window.Width = 520; window.Height = 360;
            Pump();
            var expected = new Rect(window.Left, window.Top, window.Width, window.Height);
            window.Close(); Pump();
            Assert.AreEqual(expected, _item.FloatingBounds);
        }

        [Test]
        public void ActivatingADocumentDismissesTheAutoHideOverlay()
        {
            var tool = new DockItem("tool", "Tool", new TextBox());
            _workspace.Add(tool, DockState.Right);
            _workspace.AutoHide(tool);
            _workspace.OpenAutoHide(tool);
            _workspace.Activate(_item);
            Assert.IsNull(_workspace.OpenAutoHideItem);
            Assert.AreSame(_item, _workspace.ActiveItem);
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top), TestCase(DockState.Bottom)]
        public void AutoHideFlyoutStaysInsideANarrowWorkspace(DockState edge)
        {
            ShowHost();
            var tool = new DockItem("tool", "Tool", new TextBox());
            _workspace.Add(tool, edge);
            _workspace.AutoHide(tool);
            _host.Width = 220; _host.Height = 180; Pump();
            _workspace.OpenAutoHide(tool); Pump();
            var surface = (Grid)_workspace.Content;
            var flyout = surface.Children.OfType<Border>().Single();
            Assert.LessOrEqual(flyout.ActualWidth, surface.ActualWidth);
            Assert.LessOrEqual(flyout.ActualHeight, surface.ActualHeight);
            _host.Width = 800; _host.Height = 600; Pump();
            if (edge == DockState.Left || edge == DockState.Right) Assert.GreaterOrEqual(flyout.ActualWidth, 220);
            else Assert.GreaterOrEqual(flyout.ActualHeight, 160);
            _host.Width = 220; _host.Height = 180; Pump();
            Assert.LessOrEqual(flyout.ActualWidth, surface.ActualWidth);
            Assert.LessOrEqual(flyout.ActualHeight, surface.ActualHeight);
        }

        [Test]
        public void DarkTreeHeadersUseTheThemeForeground()
        {
            _workspace.Theme = DockTheme.Dark;
            var tree = new TreeView();
            tree.SetResourceReference(FrameworkElement.StyleProperty, "Dps.TreeView");
            var child = new TreeViewItem { Header = "Child" };
            var parent = new TreeViewItem { Header = "Parent", IsExpanded = true };
            parent.Items.Add(child); tree.Items.Add(parent);
            _workspace.Add(new DockItem("tree", "Tree", tree), DockState.Left);
            ShowHost();
            Assert.AreEqual(((SolidColorBrush)_workspace.FindResource("Dps.Foreground")).Color,
                ((SolidColorBrush)parent.Foreground).Color);
            Assert.AreEqual(((SolidColorBrush)parent.Foreground).Color, ((SolidColorBrush)child.Foreground).Color);
            _workspace.Theme = DockTheme.Light; Pump();
            Assert.AreEqual(((SolidColorBrush)_workspace.FindResource("Dps.Foreground")).Color,
                ((SolidColorBrush)child.Foreground).Color);
        }

        [Test]
        public void ActivationTransfersFocusBetweenDockedAndFloatingContent()
        {
            var second = new DockItem("second", "Second", new TextBox());
            _workspace.Add(second);
            ShowHost();
            _workspace.Float(_item); Pump();
            var floating = _host.OwnedWindows[0];
            _workspace.Activate(second); Pump();
            Assert.IsTrue(_host.IsActive);
            Assert.IsTrue(((TextBox)second.Content).IsKeyboardFocusWithin);
            _workspace.Activate(_item); Pump();
            Assert.IsTrue(floating.IsActive);
            Assert.IsTrue(((TextBox)_item.Content).IsKeyboardFocusWithin);
        }

        [Test]
        public void DismissingAutoHideReturnsKeyboardFocusToTheDocument()
        {
            var tool = new DockItem("tool", "Tool", new TextBox());
            _workspace.Add(tool, DockState.Right);
            _workspace.AutoHide(tool);
            ShowHost();
            _workspace.Activate(_item); Pump();
            _workspace.OpenAutoHide(tool); Pump();
            Assert.IsTrue(((TextBox)tool.Content).IsKeyboardFocusWithin);
            _workspace.DismissAutoHide(); Pump();
            Assert.IsTrue(((TextBox)_item.Content).IsKeyboardFocusWithin);
        }

        [Test]
        public void DarkDockMenuCommandsUseTheThemeForeground()
        {
            _workspace.Theme = DockTheme.Dark;
            ShowHost();
            var page = Descendants<TabControl>(_workspace).Single().Items.Cast<TabItem>().Single();
            var header = (FrameworkElement)page.Header;
            var menu = header.ContextMenu;
            menu.PlacementTarget = header;
            try
            {
                menu.IsOpen = true; Pump();
                foreach (var command in menu.Items.OfType<MenuItem>())
                    Assert.AreEqual(((SolidColorBrush)_workspace.FindResource("Dps.Foreground")).Color,
                        ((SolidColorBrush)command.Foreground).Color);
            }
            finally { menu.IsOpen = false; }
        }
        [TestCase(Orientation.Vertical), TestCase(Orientation.Horizontal)]
        public void ThemedScrollBarsPageAndFollowContentOffsets(Orientation orientation)
        {
            var viewer = new ScrollViewer
            {
                Content = new Border { Width = 1600, Height = 1600 },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            viewer.SetResourceReference(FrameworkElement.StyleProperty, "Dps.ScrollViewer");
            _workspace.Add(new DockItem("scroll", "Scroll", viewer));
            ShowHost();
            var bar = VisualDescendants<ScrollBar>(viewer).Single(candidate => candidate.Orientation == orientation);
            var track = (Track)bar.Template.FindName("PART_Track", bar);
            Assert.IsNotNull(track.Thumb);
            var command = orientation == Orientation.Vertical ? ScrollBar.PageDownCommand : ScrollBar.PageRightCommand;
            command.Execute(null, bar); Pump();
            var offset = orientation == Orientation.Vertical ? viewer.VerticalOffset : viewer.HorizontalOffset;
            Assert.Greater(offset, 0);
            Assert.AreEqual(offset, bar.Value);
            if (orientation == Orientation.Vertical) viewer.ScrollToVerticalOffset(180);
            else viewer.ScrollToHorizontalOffset(180);
            Pump();
            Assert.AreEqual(180, bar.Value);
            Assert.AreEqual(bar.Value, track.Value);
            _workspace.Theme = DockTheme.Dark; Pump();
            Assert.AreEqual(((SolidColorBrush)_workspace.FindResource("Dps.Surface")).Color,
                ((SolidColorBrush)bar.Background).Color);
        }

        private static System.Collections.Generic.IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match) yield return match;
                foreach (var descendant in VisualDescendants<T>(child)) yield return descendant;
            }
        }
        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                if (child is T match) yield return match;
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
            }
        }
        private DockLayout Snapshot()
        {
            using (var stream = new MemoryStream())
            {
                _workspace.SaveLayout(stream); stream.Position = 0;
                return (DockLayout)new XmlSerializer(typeof(DockLayout)).Deserialize(stream);
            }
        }
        private void Restore(DockLayout layout)
        {
            using (var stream = new MemoryStream())
            {
                new XmlSerializer(typeof(DockLayout)).Serialize(stream, layout); stream.Position = 0;
                _workspace.LoadLayout(stream);
            }
        }
        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}
