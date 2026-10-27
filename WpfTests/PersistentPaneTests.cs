using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class PersistentPaneTests
    {
        [Test]
        public void NonClosablePaneHidesItsCloseChromeAndSurvivesNativeAndKeyboardClose()
        {
            var workspace = new DockWorkspace();
            var item = new DockItem("editor", "Editor", new TextBox()) { CanClose = false, ShowCloseButton = false };
            workspace.Add(item);
            var host = new Window { Content = workspace, Width = 900, Height = 600, ShowInTaskbar = false };
            try
            {
                host.Show(); Pump();
                var close = Descendants<Button>(workspace).Single(button => button.Name == "Close");
                Assert.AreEqual(Visibility.Collapsed, close.Visibility);
                Assert.IsTrue(workspace.TryHandleCloseShortcut(System.Windows.Input.Key.F4, System.Windows.Input.ModifierKeys.Control));
                Assert.AreEqual(DockState.Document, item.State);
                workspace.Float(item); Pump();
                var floating = host.OwnedWindows.Cast<Window>().Single();
                Assert.AreEqual(WindowStyle.None, floating.WindowStyle);
                floating.Close(); Pump();
                Assert.IsTrue(floating.IsVisible);
                Assert.AreEqual(DockState.Float, item.State);
                item.ShowCloseButton = true; Pump();
                Assert.AreEqual(WindowStyle.SingleBorderWindow, floating.WindowStyle);
                item.ShowCloseButton = false;
                workspace.AutoHide(item); Pump();
                Assert.AreEqual(DockState.AutoHide, item.State);
                workspace.PinGroup(item); Pump();
                Assert.AreEqual(DockState.Document, item.State);
            }
            finally { host.Content = null; Pump(); host.Close(); }
        }

        [Test]
        public void CollapsedDocumentGroupRoundTripsAndReturnsToItsOriginalSplit()
        {
            var workspace = new DockWorkspace();
            var editor = new DockItem("editor", "Editor", new TextBox());
            var log = new DockItem("log", "Log", new TextBox()) { CanClose = false };
            var variables = new DockItem("variables", "Variables", new TextBox()) { CanClose = false };
            workspace.Add(editor); workspace.Add(log); workspace.Add(variables);
            workspace.Dock(log, editor, DockPosition.Bottom, .3);
            workspace.Dock(variables, log);
            var host = new Window { Content = workspace, Width = 900, Height = 600, ShowInTaskbar = false };
            try
            {
                host.Show(); Pump();
                workspace.AutoHideGroup(variables); Pump();
                Assert.AreEqual(DockState.Document, editor.State);
                Assert.AreEqual(DockState.AutoHide, log.State);
                Assert.AreEqual(DockState.Bottom, variables.AutoHideEdge);
                Assert.AreEqual(2, Descendants<Button>(workspace).Count(b => b.Tag == log || b.Tag == variables));
                using (var stream = new MemoryStream())
                {
                    workspace.SaveLayout(stream);
                    workspace.PinGroup(variables); stream.Position = 0;
                    workspace.LoadLayout(stream); Pump();
                }
                workspace.OpenAutoHide(log); Pump();
                Assert.AreSame(log, workspace.OpenAutoHideItem);
                workspace.PinGroup(log); Pump();
                Assert.AreEqual(DockState.Document, log.State);
                Assert.AreEqual(DockState.Document, variables.State);
                using (var stream = new MemoryStream())
                {
                    workspace.SaveLayout(stream); stream.Position = 0;
                    var layout = (DockLayout)new System.Xml.Serialization.XmlSerializer(typeof(DockLayout)).Deserialize(stream);
                    var split = (DockSplit)layout.Regions.Single(region => region.State == DockState.Document).Root;
                    Assert.AreEqual(.7, split.Ratio, .03);
                    CollectionAssert.AreEquivalent(new[] { "log", "variables" }, ((DockTabGroup)split.Second).ItemIds);
                }
            }
            finally { host.Content = null; Pump(); host.Close(); }
        }

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
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
