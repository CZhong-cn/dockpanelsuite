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
using System.Xml.Serialization;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class FloatingDragTests
    {
        private DockWorkspace _workspace;
        private DockItem _editor;
        private Window _host, _floating;
        private DockItemHeader _header;
        private Grid Surface => ((Grid)_workspace.Content).Children.OfType<Grid>().Single();
        private Point TitlePoint => _header.PointToScreen(new Point(70, _header.ActualHeight / 2));

        [TearDown]
        public void TearDown()
        {
            _workspace?.EndFloatingDrag(null);
            if (_host != null) { _host.Content = null; Pump(); _host.Close(); Pump(); }
        }

        [TestCase(false), TestCase(true)]
        public void CaptionMouseDownStartsWindowMovementAndTheMovedBoundsAreSaved(bool showClose)
        {
            Show(showClose, protectedEditor: true);
            var title = (TextBlock)_header.Template.FindName("Title", _header);
            var caption = (Grid)VisualTreeHelper.GetChild(_header, 0);
            Assert.IsTrue(DockItemHeader.GetIsDragHandle(caption), "The empty part of the title must remain draggable.");
            Assert.IsNotNull(caption.Background);
            var origin = new Point(_floating.Left, _floating.Top);
            var start = _header.PointToScreen(Mouse.GetPosition(_header));
            (showClose ? (UIElement)title : caption).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.MouseDownEvent });
            Assert.IsTrue(_header.IsMouseCaptured, "Floating titles must capture the pointer instead of starting OLE-only docking.");
            var delta = new Vector(120, 75);
            var logicalDelta = PresentationSource.FromVisual(_floating).CompositionTarget.TransformFromDevice.Transform(delta);
            _workspace.UpdateFloatingDrag(start + delta);
            Assert.AreEqual(origin.X + logicalDelta.X, _floating.Left, 1);
            Assert.AreEqual(origin.Y + logicalDelta.Y, _floating.Top, 1);
            Assert.AreEqual(DockState.Float, _editor.State);
            _workspace.EndFloatingDrag(null);
            Assert.IsFalse(_header.IsMouseCaptured);
            Assert.AreEqual(1, _floating.Opacity);
            var saved = Snapshot().Items.Single(item => item.Id == _editor.Id);
            Assert.AreEqual(_floating.Left, saved.X, 1); Assert.AreEqual(_floating.Top, saved.Y, 1);
            Assert.AreSame(_editor.Content, Descendants<TextBox>(_floating).Single());
        }

        [Test]
        public void AClickOrTinyMovementDoesNotMoveOrRedockTheWindow()
        {
            Show(false);
            var origin = new Point(_floating.Left, _floating.Top);
            var start = TitlePoint;
            Assert.IsTrue(_workspace.BeginFloatingDrag(_editor, _header, start));
            _workspace.UpdateFloatingDrag(start + new Vector(.1, .1));
            _workspace.EndFloatingDrag(start);
            Assert.AreEqual(origin, new Point(_floating.Left, _floating.Top));
            Assert.AreEqual(DockState.Float, _editor.State);
            Assert.AreEqual(1, _floating.Opacity); Assert.IsFalse(_header.IsMouseCaptured);
        }

        [Test]
        public void ReleasingOverTheWorkspaceBodyKeepsThePaneFloating()
        {
            Show(false);
            var point = Surface.PointToScreen(new Point(85, 170));
            Assert.IsTrue(_workspace.BeginFloatingDrag(_editor, _header, TitlePoint));
            _workspace.UpdateFloatingDrag(point);
            _workspace.EndFloatingDrag(point); Pump();
            Assert.AreEqual(DockState.Float, _editor.State);
            Assert.IsTrue(_floating.IsVisible); Assert.AreEqual(1, _floating.Opacity);
            Assert.IsFalse(Surface.Children.OfType<DockGuideOverlay>().Any());
        }

        [TestCase(false), TestCase(true)]
        public void DroppingOnAGuideStillDocksAndPreservesContent(bool protectedEditor)
        {
            Show(false, protectedEditor);
            var peer = _workspace.Items.Single(item => item.Id == "peer");
            var bounds = _workspace.GroupBounds(peer);
            var guides = DockGuideGeometry.Create(Surface.ActualWidth, Surface.ActualHeight,
                bounds.X, bounds.Y, bounds.Width, bounds.Height, _workspace.GuideMetrics);
            var guide = guides.Single(target => protectedEditor ? target.State == DockState.Document : target.IsOuter && target.State == DockState.Left);
            var point = Surface.PointToScreen(new Point(guide.X + guide.Size / 2, guide.Y + guide.Size / 2));
            var content = _editor.Content;
            Assert.IsTrue(_workspace.BeginFloatingDrag(_editor, _header, TitlePoint));
            _workspace.UpdateFloatingDrag(point);
            Assert.IsTrue(Surface.Children.OfType<DockGuideOverlay>().Any());
            _workspace.EndFloatingDrag(point); Pump();
            Assert.AreEqual(protectedEditor ? DockState.Document : DockState.Left, _editor.State);
            Assert.AreSame(content, _editor.Content);
            Assert.IsFalse(_floating.IsVisible); Assert.IsFalse(_header.IsMouseCaptured);
            Assert.IsFalse(Surface.Children.OfType<DockGuideOverlay>().Any());
        }

        [Test]
        public void ForbiddenSideTargetsCannotDockTheProtectedEditorDuringMovement()
        {
            Show(false, protectedEditor: true);
            var target = DockGuideGeometry.Create(Surface.ActualWidth, Surface.ActualHeight, _workspace.GuideMetrics)
                .Single(guide => guide.IsOuter && guide.State == DockState.Left);
            var point = Surface.PointToScreen(new Point(target.X + target.Size / 2, target.Y + target.Size / 2));
            Assert.IsTrue(_workspace.BeginFloatingDrag(_editor, _header, TitlePoint));
            _workspace.UpdateFloatingDrag(point);
            _workspace.EndFloatingDrag(point); Pump();
            Assert.AreEqual(DockState.Float, _editor.State);
            Assert.IsTrue(_floating.IsVisible); Assert.AreEqual(1, _floating.Opacity);
        }

        [Test]
        public void EscapeRestoresTheOriginalPositionAndReleasesCapture()
        {
            Show(false);
            var origin = new Point(_floating.Left, _floating.Top);
            var start = TitlePoint;
            Assert.IsTrue(_workspace.BeginFloatingDrag(_editor, _header, start));
            _workspace.UpdateFloatingDrag(start + new Vector(130, 80));
            _floating.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(_floating), 0, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.AreEqual(origin, new Point(_floating.Left, _floating.Top));
            Assert.IsFalse(_header.IsMouseCaptured); Assert.AreEqual(1, _floating.Opacity);
            Assert.IsFalse(Surface.Children.OfType<DockGuideOverlay>().Any());
            Assert.AreEqual(DockState.Float, _editor.State);
        }

        [Test]
        public void LosingCaptureOrRebuildingTheLayoutCleansUpWithoutLosingTheMovedPosition()
        {
            Show(false);
            var start = TitlePoint;
            Assert.IsTrue(_workspace.BeginFloatingDrag(_editor, _header, start));
            _workspace.UpdateFloatingDrag(start + new Vector(130, 80));
            var moved = new Point(_floating.Left, _floating.Top);
            _header.ReleaseMouseCapture();
            _workspace.UpdateFloatingDrag(start + new Vector(260, 160));
            Assert.AreEqual(moved, new Point(_floating.Left, _floating.Top));
            Assert.AreEqual(1, _floating.Opacity);
            Assert.IsFalse(Surface.Children.OfType<DockGuideOverlay>().Any());
            Assert.IsTrue(_workspace.BeginFloatingDrag(_editor, _header, TitlePoint));
            _workspace.UpdateFloatingDrag(TitlePoint + new Vector(90, 60));
            _workspace.Add(new DockItem("extra", "Extra", new TextBox())); Pump();
            Assert.IsFalse(_header.IsMouseCaptured); Assert.AreEqual(1, _floating.Opacity);
            Assert.AreEqual(DockState.Float, _editor.State);
        }

        [Test]
        public void DroppingOnADocumentTitleStillMergesTabs()
        {
            Show(false, protectedEditor: true);
            var target = Descendants<DockItemHeader>(_workspace).Single(header => header.Item.Id == "peer");
            var point = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
            Assert.IsTrue(_workspace.BeginFloatingDrag(_editor, _header, TitlePoint));
            _workspace.UpdateFloatingDrag(point);
            _workspace.EndFloatingDrag(point); Pump();
            Assert.AreEqual(DockState.Document, _editor.State);
            Assert.IsFalse(_floating.IsVisible);
            Assert.AreEqual(2, Descendants<TabItem>(_workspace).Count());
        }

        [Test]
        public void ALayoutChangeDuringDraggingDoesNotOverwriteItsExplicitRegionSize()
        {
            Show(false);
            _workspace.Add(new DockItem("left", "Left", new TextBox()), DockState.Left, 260); Pump();
            _header = Descendants<DockItemHeader>(_floating).Single();
            var start = TitlePoint;
            Assert.IsTrue(_workspace.BeginFloatingDrag(_editor, _header, start));
            _workspace.UpdateFloatingDrag(start + new Vector(120, 80));
            _workspace.Add(new DockItem("new-left", "New left", new TextBox()), DockState.Left, 340); Pump();
            Assert.AreEqual(340, Snapshot().LeftWidth, 1);
            Assert.IsFalse(_header.IsMouseCaptured); Assert.AreEqual(1, _floating.Opacity);
        }

        [Test]
        public void PinAndCloseButtonsDoNotStartWindowMovement()
        {
            Show(true);
            foreach (var name in new[] { "Pin", "Close" })
            {
                var button = (Button)_header.Template.FindName(name, _header);
                Assert.IsFalse(DockItemHeader.GetIsDragHandle(button));
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = Mouse.MouseDownEvent });
                Assert.IsFalse(_header.IsMouseCaptured);
                if (button.IsMouseCaptured) button.ReleaseMouseCapture();
            }
            Assert.AreEqual(DockState.Float, _editor.State);
        }

        private void Show(bool showClose, bool protectedEditor = false)
        {
            _workspace = new DockWorkspace();
            _editor = new DockItem("editor", "Editor", new TextBox { Text = "Keep content" })
            {
                ShowCloseButton = showClose, CanClose = false,
                CanAutoHide = !protectedEditor, CanDockToSides = !protectedEditor
            };
            _workspace.Add(_editor); _workspace.Add(new DockItem("peer", "Peer", new TextBox()));
            _host = new Window { Content = _workspace, Width = 1100, Height = 750, ShowInTaskbar = false };
            _host.Show(); Pump();
            _workspace.Float(_editor, new Rect(200, 160, 640, 400)); Pump();
            _floating = _host.OwnedWindows.Cast<Window>().Single();
            _header = Descendants<DockItemHeader>(_floating).Single();
        }
        private DockLayout Snapshot()
        {
            using (var stream = new MemoryStream())
            {
                _workspace.SaveLayout(stream); stream.Position = 0;
                return (DockLayout)new XmlSerializer(typeof(DockLayout)).Deserialize(stream);
            }
        }
        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
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
