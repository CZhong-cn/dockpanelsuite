using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
            layout.Items[0].Id = "uninstalled-plugin";
            Restore(layout);
            Assert.AreEqual(DockState.Hidden, _item.State);
            Assert.AreEqual(1, _workspace.Items.Count);
        }

        [TestCase("duplicate"), TestCase("version"), TestCase("bounds"), TestCase("previous"), TestCase("size")]
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
            var tab = ((Grid)_workspace.Content).Children.OfType<TabControl>().Single();
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
