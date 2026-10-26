using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class CloseShortcutTests
    {
        private DockWorkspace _workspace;
        private DockItem _item;
        private Window _host;

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _item = new DockItem("editor", "Editor", new TextBox { Text = "Preserved" });
            _workspace.Add(_item);
            _host = new Window { Content = _workspace, Width = 800, Height = 600, ShowInTaskbar = false };
            _host.Show(); Pump();
        }
        [TearDown]
        public void TearDown() { _host.Content = null; Pump(); _host.Close(); Pump(); }

        [TestCase(false), TestCase(true)]
        public void CtrlWIsDisabledAndUnconsumedByDefault(bool floating)
        {
            Prepare(floating);
            int closing = 0; _workspace.ItemClosing += (sender, args) => closing++;
            Assert.IsFalse(_workspace.IsCtrlWEnabled);
            Assert.IsFalse((bool)DockWorkspace.IsCtrlWEnabledProperty.GetMetadata(typeof(DockWorkspace)).DefaultValue);
            Assert.IsFalse(_workspace.TryHandleCloseShortcut(Key.W, ModifierKeys.Control)); Pump();
            Assert.AreEqual(floating ? DockState.Float : DockState.Document, _item.State);
            Assert.AreEqual(0, closing);
            Assert.AreEqual(floating ? 1 : 0, _host.OwnedWindows.Count);
        }

        [TestCase(false), TestCase(true)]
        public void CtrlWCanBeEnabledAndDisabledForExistingPanes(bool floating)
        {
            Prepare(floating);
            _workspace.IsCtrlWEnabled = true;
            Assert.IsTrue(_workspace.TryHandleCloseShortcut(Key.W, ModifierKeys.Control)); Pump();
            Assert.AreEqual(DockState.Hidden, _item.State);
            Assert.AreEqual(0, _host.OwnedWindows.Count);
            _workspace.Show(_item, floating ? DockState.Float : DockState.Document); Pump();
            _workspace.IsCtrlWEnabled = false;
            Assert.IsFalse(_workspace.TryHandleCloseShortcut(Key.W, ModifierKeys.Control)); Pump();
            Assert.AreEqual(floating ? DockState.Float : DockState.Document, _item.State);
            Assert.AreEqual("Preserved", ((TextBox)_item.Content).Text);
        }

        [TestCase(false), TestCase(true)]
        public void EnabledCtrlWHonorsCanCloseAndClosingCancellation(bool cancel)
        {
            _workspace.IsCtrlWEnabled = true;
            if (cancel) _workspace.ItemClosing += (sender, args) => args.Cancel = true;
            else _item.CanClose = false;
            Assert.IsTrue(_workspace.TryHandleCloseShortcut(Key.W, ModifierKeys.Control)); Pump();
            Assert.AreEqual(DockState.Document, _item.State);
        }

        [Test]
        public void CtrlWEnablementSupportsBindingAndRequiresControl()
        {
            var setting = new CheckBox { IsChecked = true };
            BindingOperations.SetBinding(_workspace, DockWorkspace.IsCtrlWEnabledProperty,
                new Binding(nameof(ToggleButton.IsChecked)) { Source = setting, Mode = BindingMode.OneWay });
            Pump();
            Assert.IsTrue(_workspace.IsCtrlWEnabled);
            Assert.IsFalse(_workspace.TryHandleCloseShortcut(Key.W, ModifierKeys.None));
            Assert.IsFalse(_workspace.TryHandleCloseShortcut(Key.W, ModifierKeys.Alt));
            setting.IsChecked = false; Pump();
            Assert.IsFalse(_workspace.TryHandleCloseShortcut(Key.W, ModifierKeys.Control));
            setting.IsChecked = true; Pump();
            Assert.IsTrue(_workspace.TryHandleCloseShortcut(Key.W, ModifierKeys.Control)); Pump();
            Assert.AreEqual(DockState.Hidden, _item.State);
        }

        [TestCase(false), TestCase(true)]
        public void CtrlF4StillClosesPanesWhenCtrlWIsDisabled(bool floating)
        {
            Prepare(floating);
            Assert.IsFalse(_workspace.IsCtrlWEnabled);
            Assert.IsTrue(_workspace.TryHandleCloseShortcut(Key.F4, ModifierKeys.Control)); Pump();
            Assert.AreEqual(DockState.Hidden, _item.State);
            Assert.AreEqual(0, _host.OwnedWindows.Count);
        }

        private void Prepare(bool floating) { if (floating) _workspace.Float(_item); _workspace.Activate(_item); Pump(); }
        private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    }
}
