using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.Tests
{
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class InitialSizingTests
    {
        private DockWorkspace _workspace;
        private Window _host;
        private Grid Surface => ((Grid)_workspace.Content).Children.OfType<Grid>().Single();

        [SetUp]
        public void SetUp()
        {
            _workspace = new DockWorkspace();
            _workspace.Resources["Dps.DefaultToolWidth"] = 240d;
            _workspace.Resources["Dps.DefaultToolHeight"] = 180d;
            _workspace.Add(Pane("document"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host == null) return;
            _host.Content = null; _host.Close(); Pump();
        }

        [TestCase(DockState.Left, false), TestCase(DockState.Right, false),
         TestCase(DockState.Top, false), TestCase(DockState.Bottom, false),
         TestCase(DockState.Left, true), TestCase(DockState.Right, true),
         TestCase(DockState.Top, true), TestCase(DockState.Bottom, true)]
        public void IntegerSizeSetsTheCorrectAxisBeforeOrAfterLoading(DockState edge, bool loaded)
        {
            if (loaded) Show();
            var pane = Pane("pane");
            _workspace.Add(pane, edge, size: 300);
            if (!loaded) Show();
            else Pump();
            AssertSize(pane, 300);
            var saved = Snapshot();
            Assert.AreEqual(300, SavedSize(saved, edge), 1);
            if (edge != DockState.Left) Assert.AreEqual(240, saved.LeftWidth);
            if (edge != DockState.Bottom) Assert.AreEqual(180, saved.BottomHeight);
        }

        [TestCase(DockState.Left, false), TestCase(DockState.Right, false),
         TestCase(DockState.Top, false), TestCase(DockState.Bottom, false),
         TestCase(DockState.Left, true), TestCase(DockState.Right, true),
         TestCase(DockState.Top, true), TestCase(DockState.Bottom, true)]
        public void FloatSizeUsesTheSurfaceAxisAndIsNotReappliedAfterResizing(DockState edge, bool loaded)
        {
            if (loaded) Show();
            var pane = Pane("pane");
            _workspace.Add(pane, edge, proportion: .3f);
            if (!loaded) Show();
            else Pump();
            double expected = AxisLength(edge) * .3;
            AssertSize(pane, expected);
            double initial = SavedSize(Snapshot(), edge);
            _host.Width += 160; _host.Height += 120; Pump();
            AssertSize(pane, initial);
            Restore(Snapshot()); Pump();
            AssertSize(pane, initial);
        }

        [TestCase(false), TestCase(true)]
        public void ZeroUsesTheSameResourcesAsTheOriginalOverload(bool floatingPoint)
        {
            var left = Pane("left"); var bottom = Pane("bottom");
            if (floatingPoint)
            {
                _workspace.Add(left, DockState.Left, 0f);
                _workspace.Add(bottom, DockState.Bottom, 0f);
                _workspace.Add(Pane("other-document"), DockState.Document, 0f);
            }
            else
            {
                _workspace.Add(left, DockState.Left, 0);
                _workspace.Add(bottom, DockState.Bottom, 0);
                _workspace.Add(Pane("other-document"), DockState.Document, 0);
            }
            Show(); AssertSize(left, 240); AssertSize(bottom, 180);
        }

        [TestCase(false), TestCase(true)]
        public void TabsShareTheLastExplicitSizeAndZeroDoesNotResetIt(bool floatingPoint)
        {
            var first = Pane("first"); var second = Pane("second"); var third = Pane("third");
            _workspace.Add(first, DockState.Left, 310);
            if (floatingPoint) _workspace.Add(second, DockState.Left, .25f);
            else _workspace.Add(second, DockState.Left, 350);
            _workspace.Add(third, DockState.Left, 0);
            _workspace.Add(Pane("fourth"), DockState.Left, 0f);
            _workspace.Add(Pane("fifth"), DockState.Left);
            Show();
            double expected = floatingPoint ? AxisLength(DockState.Left) * .25 : 350;
            Assert.AreEqual(expected, Snapshot().LeftWidth, 1);
            Assert.AreEqual(1, Surface.Children.OfType<TabControl>().Count(tabs => tabs.Items.Count == 5));
        }

        [TestCase(false), TestCase(true)]
        public void RestoredSizesOverridePendingStartupRequests(bool floatingPoint)
        {
            var left = Pane("left"); var bottom = Pane("bottom");
            if (floatingPoint)
            {
                _workspace.Add(left, DockState.Left, .3f);
                _workspace.Add(bottom, DockState.Bottom, .4f);
            }
            else
            {
                _workspace.Add(left, DockState.Left, 330);
                _workspace.Add(bottom, DockState.Bottom, 260);
            }
            var saved = Snapshot(); saved.LeftWidth = 210; saved.BottomHeight = 170;
            Restore(saved); Show();
            AssertSize(left, 210); AssertSize(bottom, 170);
            _host.Width += 150; Pump(); AssertSize(left, 210);
        }

        [Test]
        public void EachEdgeCanHaveAnIndependentProportionAndHiddenPaneKeepsItsRequest()
        {
            var left = Pane("left"); var right = Pane("right");
            var top = Pane("top"); var bottom = Pane("bottom");
            _workspace.Add(left, DockState.Left, .2f);
            _workspace.Add(right, DockState.Right, .25f);
            _workspace.Add(top, DockState.Top, .15f);
            _workspace.Add(bottom, DockState.Bottom, .3f);
            _workspace.Show(right, DockState.Hidden);
            Show();
            AssertSize(left, AxisLength(DockState.Left) * .2);
            AssertSize(top, AxisLength(DockState.Top) * .15);
            AssertSize(bottom, AxisLength(DockState.Bottom) * .3);
            _workspace.Show(right, DockState.Right); Pump();
            AssertSize(right, AxisLength(DockState.Right) * .25);
        }

        [Test]
        public void PixelRequestOverridesPendingProportionAndManualResizeSurvivesLaterRenders()
        {
            var first = Pane("first"); var second = Pane("second");
            _workspace.Add(first, DockState.Left, .4f);
            _workspace.Add(second, DockState.Left, 320);
            Show(); Assert.AreEqual(320, Snapshot().LeftWidth, 1);
            Surface.ColumnDefinitions[0].Width = new GridLength(370); Pump();
            _workspace.Add(Pane("extra"), DockState.Left); Pump();
            Assert.AreEqual(370, Snapshot().LeftWidth, 1);
            _host.Width += 150; Pump(); Assert.AreEqual(370, Snapshot().LeftWidth, 1);
        }

        [TestCase(-1), TestCase(10001)]
        public void InvalidPixelSizesAreRejectedBeforeRegisteringThePane(int size)
        {
            var pane = Pane("invalid");
            Assert.Throws<ArgumentOutOfRangeException>(() => _workspace.Add(pane, DockState.Left, size));
            Assert.AreEqual(1, _workspace.Items.Count);
            _workspace.Add(pane, DockState.Left); Show(); AssertSize(pane, 240);
        }

        [TestCase(float.NaN), TestCase(float.PositiveInfinity), TestCase(float.NegativeInfinity),
         TestCase(-.1f), TestCase(.01f), TestCase(.96f)]
        public void InvalidProportionsAreRejectedBeforeRegisteringThePane(float proportion)
        {
            var pane = Pane("invalid");
            Assert.Throws<ArgumentOutOfRangeException>(() => _workspace.Add(pane, DockState.Left, proportion));
            Assert.AreEqual(1, _workspace.Items.Count);
            _workspace.Add(pane, DockState.Left); Show(); AssertSize(pane, 240);
        }

        [TestCase(DockState.Document), TestCase(DockState.Float), TestCase(DockState.Hidden), TestCase(DockState.AutoHide)]
        public void NonzeroSizesRequireAnEdge(DockState state)
        {
            Assert.Throws<ArgumentException>(() => _workspace.Add(Pane("integer"), state, 300));
            Assert.Throws<ArgumentException>(() => _workspace.Add(Pane("fraction"), state, .3f));
            Assert.AreEqual(1, _workspace.Items.Count);
        }

        [TestCase(false), TestCase(true)]
        public void RejectedRegistrationDoesNotChangeAnExistingRegionRequest(bool floatingPoint)
        {
            var left = Pane("left"); _workspace.Add(left, DockState.Left, 320);
            if (floatingPoint) Assert.Throws<ArgumentException>(() => _workspace.Add(Pane("left"), DockState.Left, .5f));
            else Assert.Throws<ArgumentException>(() => _workspace.Add(Pane("left"), DockState.Left, 500));
            var protectedPane = Pane("protected"); protectedPane.CanDockToSides = false;
            Assert.Throws<InvalidOperationException>(() => _workspace.Add(protectedPane, DockState.Left, .5f));
            Show(); AssertSize(left, 320);
        }

        [Test]
        public void InitialProportionIsResolvedBeforeTheApplicationLoadedSnapshot()
        {
            var left = Pane("left"); _workspace.Add(left, DockState.Left, .3f);
            DockLayout loadedLayout = null;
            _workspace.Loaded += (sender, args) => loadedLayout = Snapshot();
            Show();
            Assert.IsNotNull(loadedLayout);
            Assert.AreEqual(AxisLength(DockState.Left) * .3, loadedLayout.LeftWidth, 1);
        }

        [TestCase(1), TestCase(10000)]
        public void PixelSizesRespectLayoutMinimumsAndRemainRestorable(int size)
        {
            var left = Pane("left"); _workspace.Add(left, DockState.Left, size); Show();
            AssertSize(left, Math.Max((double)_workspace.FindResource("Dps.PaneMinWidth"), size));
            Restore(Snapshot()); Pump();
        }

        private static DockItem Pane(string id) => new DockItem(id, id, new TextBox());
        private void Show()
        {
            _host = new Window { Content = _workspace, Width = 1200, Height = 820, ShowInTaskbar = false };
            _host.Show(); Pump();
        }
        private double AxisLength(DockState edge) => edge == DockState.Left || edge == DockState.Right ? Surface.ActualWidth : Surface.ActualHeight;
        private void AssertSize(DockItem pane, double expected)
        {
            var bounds = _workspace.GroupBounds(pane);
            Assert.AreEqual(expected, pane.State == DockState.Left || pane.State == DockState.Right ? bounds.Width : bounds.Height, 1);
        }
        private static double SavedSize(DockLayout layout, DockState edge) => edge == DockState.Left ? layout.LeftWidth :
            edge == DockState.Right ? layout.RightWidth : edge == DockState.Top ? layout.TopHeight : layout.BottomHeight;
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
