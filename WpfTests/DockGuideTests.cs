using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DockPanelSuite.Wpf;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class DockGuideTests
    {
        private DockWorkspace _workspace;
        private DockItem _item;
        private DockItem _target;
        private Window _host;
        private Grid Surface => ((Grid)_workspace.Content).Children.OfType<Grid>().Single();

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _item = new DockItem("editor", "Editor", new TextBox { Text = "Preserved" });
            _workspace.Add(_item);
            _target = new DockItem("target", "Target", new TextBox());
            _workspace.Add(_target); _workspace.Activate(_item);
            _host = new Window { Content = _workspace, Width = 800, Height = 600, ShowInTaskbar = false };
            _host.Show(); Pump();
        }

        [TearDown]
        public void TearDown()
        {
            _host.Content = null; Pump(); _host.Close(); Pump();
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top), TestCase(DockState.Bottom), TestCase(DockState.Document)]
        public void DroppingOnEachGuideUsesThePreviewedState(DockState state)
        {
            _workspace.Float(_item); Pump();
            var point = GuidePoint(state);
            Assert.AreEqual(state, _workspace.UpdateDockDrag(_item, point));
            var overlay = Surface.Children.OfType<DockGuideOverlay>().Single();
            Assert.AreEqual(state, overlay.HoveredTarget.Value.State);
            Assert.IsFalse(overlay.IsHitTestVisible);
            Assert.IsTrue(_workspace.CompleteDockDrag(_item, point)); Pump();
            Assert.AreEqual(DockState.Document, _item.State);
            if (state != DockState.Document) Assert.AreNotEqual(_workspace.GroupBounds(_item), _workspace.GroupBounds(_target));
            Assert.AreSame(_item, _workspace.ActiveItem);
            Assert.AreEqual("Preserved", ((TextBox)_item.Content).Text);
            Assert.IsEmpty(Surface.Children.OfType<DockGuideOverlay>());
            Assert.AreEqual(0, _host.OwnedWindows.Count);
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top), TestCase(DockState.Bottom)]
        public void OuterGuidesDockAtTheCorrespondingWorkspaceEdge(DockState state)
        {
            var point = GuidePoint(state, true);
            Assert.AreEqual(state, _workspace.UpdateDockDrag(_item, point));
            Assert.IsTrue(Surface.Children.OfType<DockGuideOverlay>().Single().HoveredTarget.Value.IsOuter);
            Assert.IsTrue(_workspace.CompleteDockDrag(_item, point)); Pump();
            Assert.AreEqual(state, _item.State);
        }

        [Test]
        public void MovingBetweenTargetsReusesTheOverlayAndUpdatesItsHighlight()
        {
            _workspace.UpdateDockDrag(_item, GuidePoint(DockState.Left)); Pump();
            var overlay = Surface.Children.OfType<DockGuideOverlay>().Single();
            _workspace.UpdateDockDrag(_item, GuidePoint(DockState.Bottom)); Pump();
            Assert.AreSame(overlay, Surface.Children.OfType<DockGuideOverlay>().Single());
            Assert.AreEqual(DockState.Bottom, overlay.HoveredTarget.Value.State);
            var tile = overlay.Children.OfType<DockGuideTile>().Single(border =>
                Equals(border.Tag, DockState.Bottom) && Canvas.GetTop(border) < Surface.ActualHeight - 100);
            Assert.AreEqual(ColorOf(_workspace.FindResource("Dps.GuideHover")), ColorOf(tile.Background));
            var frame = (System.Windows.Shapes.Rectangle)tile.Template.FindName("Frame", tile);
            Assert.AreEqual(ColorOf(_workspace.FindResource("Dps.GuideHoverGlyph")), ColorOf(frame.Stroke));
            _workspace.Theme = DockTheme.Dark; Pump();
            Assert.AreEqual(ColorOf(_workspace.FindResource("Dps.GuideHover")), ColorOf(tile.Background));
            Assert.AreEqual(ColorOf(_workspace.FindResource("Dps.GuideHoverGlyph")), ColorOf(frame.Stroke));
        }

        [Test]
        public void ForeignAndOutsideDragsClearTheOverlayWithoutMovingContent()
        {
            _workspace.UpdateDockDrag(_item, GuidePoint(DockState.Left));
            var other = new DockWorkspace();
            var foreign = new DockItem("other", "Other", new TextBox()); other.Add(foreign);
            Assert.IsNull(_workspace.UpdateDockDrag(foreign, GuidePoint(DockState.Left)));
            Assert.IsEmpty(Surface.Children.OfType<DockGuideOverlay>());
            Assert.IsFalse(_workspace.CompleteDockDrag(foreign, GuidePoint(DockState.Left)));
            Assert.IsFalse(_workspace.CompleteDockDrag(_item, new Point(-1, 20)));
            Assert.AreEqual(DockState.Document, _item.State);
            Assert.AreEqual(DockState.Document, foreign.State);
        }

        [Test]
        public void CancelingOrUnloadingRemovesGuides()
        {
            _workspace.UpdateDockDrag(_item, GuidePoint(DockState.Left));
            _workspace.ClearDropPreview();
            Assert.IsEmpty(Surface.Children.OfType<DockGuideOverlay>());
            _workspace.UpdateDockDrag(_item, GuidePoint(DockState.Right));
            var surface = Surface;
            _host.Content = null; Pump();
            Assert.IsEmpty(surface.Children.OfType<DockGuideOverlay>());
        }

        [Test]
        public void EditorDragHandlersCannotBlockTheGuideOrFinalDrop()
        {
            var editor = (TextBox)_item.Content;
            bool swallowed = false;
            editor.PreviewDragOver += (sender, args) => { swallowed = true; args.Handled = true; args.Effects = DragDropEffects.None; };
            var point = Surface.TranslatePoint(GuidePoint(DockState.Right), editor);
            var over = RaiseDrag(editor, point, DragDrop.PreviewDragOverEvent);
            Assert.IsFalse(swallowed);
            Assert.AreEqual(DragDropEffects.Move, over.Effects);
            Assert.AreEqual(DockState.Right, Surface.Children.OfType<DockGuideOverlay>().Single().HoveredTarget.Value.State);
            var drop = RaiseDrag(editor, point, DragDrop.PreviewDropEvent); Pump();
            Assert.AreEqual(DragDropEffects.Move, drop.Effects);
            Assert.AreEqual(DockState.Document, _item.State);
            Assert.Greater(_workspace.GroupBounds(_item).Left, _workspace.GroupBounds(_target).Left);
            Assert.IsEmpty(Surface.Children.OfType<DockGuideOverlay>());
        }

        [Test]
        public void HeaderDropStillMovesAndOrdersTabsAcrossRegions()
        {
            var second = new DockItem("second", "Second", new TextBox());
            _workspace.Add(second); _workspace.Show(_item, DockState.Left); Pump();
            var tabs = Surface.Children.OfType<TabControl>().Single(tab => Equals(tab.Tag, DockState.Document));
            var header = (FrameworkElement)((TabItem)tabs.Items[0]).Header;
            var point = new Point(header.ActualWidth / 2, header.ActualHeight / 2);
            RaiseDrag(header, point, DragDrop.PreviewDragOverEvent);
            Assert.AreEqual(Visibility.Collapsed, Surface.Children.OfType<Border>().Single().Visibility);
            RaiseDrag(header, point, DragDrop.PreviewDropEvent); Pump();
            Assert.AreEqual(DockState.Document, _item.State);
            Assert.Less(_workspace.Items.IndexOf(_item), _workspace.Items.IndexOf(second));
            Assert.AreSame(_item, _workspace.ActiveItem);
        }

        [Test]
        public void ChildDragLeaveKeepsGuidesUntilThePointerLeavesTheWorkspace()
        {
            var editor = (TextBox)_item.Content;
            _workspace.UpdateDockDrag(_item, GuidePoint(DockState.Left));
            RaiseDrag(editor, Surface.TranslatePoint(new Point(100, 100), editor), DragDrop.PreviewDragLeaveEvent);
            Assert.AreEqual(1, Surface.Children.OfType<DockGuideOverlay>().Count());
            RaiseDrag(editor, Surface.TranslatePoint(new Point(-10, -10), editor), DragDrop.PreviewDragLeaveEvent);
            Assert.IsEmpty(Surface.Children.OfType<DockGuideOverlay>());
        }

        [Test]
        public void GuideDropTakesPriorityOverAHeaderUnderneathIt()
        {
            var header = (FrameworkElement)((TabItem)Surface.Children.OfType<TabControl>().Single().Items[0]).Header;
            var point = Surface.TranslatePoint(GuidePoint(DockState.Bottom), header);
            RaiseDrag(header, point, DragDrop.PreviewDropEvent); Pump();
            Assert.AreEqual(DockState.Document, _item.State);
            Assert.Greater(_workspace.GroupBounds(_item).Top, _workspace.GroupBounds(_target).Top);
        }

        [Test]
        public void GuidesFitSmallHostsAndReflowWhenTheHostExpands()
        {
            _host.Width = 220; _host.Height = 180; Pump();
            _workspace.UpdateDockDrag(_item, GuidePoint(DockState.Top)); Pump();
            var overlay = Surface.Children.OfType<DockGuideOverlay>().Single();
            Assert.AreEqual(5, overlay.Targets.Count);
            Assert.AreEqual(DockState.Top, overlay.HoveredTarget.Value.State);
            foreach (var tile in overlay.Children.OfType<DockGuideTile>())
            {
                Assert.LessOrEqual(Canvas.GetLeft(tile) + tile.ActualWidth, Surface.ActualWidth);
                Assert.LessOrEqual(Canvas.GetTop(tile) + tile.ActualHeight, Surface.ActualHeight);
            }
            _host.Width = 800; _host.Height = 600; Pump();
            _workspace.UpdateDockDrag(_item, GuidePoint(DockState.Left, true)); Pump();
            Assert.AreSame(overlay, Surface.Children.OfType<DockGuideOverlay>().Single());
            Assert.AreEqual(9, overlay.Targets.Count);
            Assert.AreEqual(DockState.Left, overlay.HoveredTarget.Value.State);
        }
        private Point GuidePoint(DockState state, bool outer = false)
        {
            var bounds = _workspace.GroupBounds(_target);
            var target = DockGuideGeometry.Create(Surface.ActualWidth, Surface.ActualHeight, bounds.X, bounds.Y, bounds.Width, bounds.Height, _workspace.GuideMetrics)
                .Single(guide => guide.State == state && guide.IsOuter == outer);
            return new Point(target.X + target.Size / 2, target.Y + target.Size / 2);
        }

        private DragEventArgs RaiseDrag(UIElement target, Point point, RoutedEvent routedEvent)
        {
            // WPF exposes no public constructor, so synthesize its real routed event arguments.
            var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { new DataObject(typeof(DockItem), _item), DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, target, point }, null);
            args.RoutedEvent = routedEvent;
            target.RaiseEvent(args);
            return args;
        }

        private static Color ColorOf(object brush) => ((SolidColorBrush)brush).Color;
        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}
