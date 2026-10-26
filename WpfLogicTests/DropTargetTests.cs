using System;
using System.Linq;
using DockPanelSuite.Wpf;
using NUnit.Framework;

namespace DockPanelSuite.Wpf.LogicTests
{
    [TestFixture]
    public class DropTargetTests
    {
        [TestCase(0, 50, DockState.Left)]
        [TestCase(17.99, 50, DockState.Left)]
        [TestCase(18, 50, DockState.Document)]
        [TestCase(82, 50, DockState.Document)]
        [TestCase(82.01, 50, DockState.Right)]
        [TestCase(100, 50, DockState.Right)]
        [TestCase(50, 0, DockState.Top)]
        [TestCase(50, 17.99, DockState.Top)]
        [TestCase(50, 18, DockState.Document)]
        [TestCase(50, 82, DockState.Document)]
        [TestCase(50, 82.01, DockState.Bottom)]
        [TestCase(50, 100, DockState.Bottom)]
        [TestCase(50, 50, DockState.Document)]
        [TestCase(0, 0, DockState.Left)]
        [TestCase(100, 100, DockState.Right)]
        public void PreviewAndDropHaveDeterministicRegions(double x, double y, DockState expected)
        {
            Assert.AreEqual(expected, DockDropTargetResolver.Resolve(x, y, 100, 100));
        }

        [TestCase(200, 800), TestCase(1920, 1080), TestCase(3840, 2160)]
        public void RegionsScaleWithWorkspaceSize(double width, double height)
        {
            Assert.AreEqual(DockState.Left, DockDropTargetResolver.Resolve(width * .1, height * .5, width, height));
            Assert.AreEqual(DockState.Bottom, DockDropTargetResolver.Resolve(width * .5, height * .9, width, height));
            Assert.AreEqual(DockState.Document, DockDropTargetResolver.Resolve(width * .5, height * .5, width, height));
        }

        [TestCase(800, 600), TestCase(1920, 1080), TestCase(180, 140)]
        public void EveryRenderedGuideResolvesToItsAdvertisedTarget(double width, double height)
        {
            foreach (var target in DockGuideGeometry.Create(width, height))
                Assert.AreEqual(target.State, DockDropTargetResolver.Resolve(target.X + target.Size / 2, target.Y + target.Size / 2, width, height));
        }

        [TestCase(DockState.Left), TestCase(DockState.Right), TestCase(DockState.Top), TestCase(DockState.Bottom), TestCase(DockState.Document)]
        public void CentralCrossOverridesTheBroadDocumentRegion(DockState state)
        {
            var target = DockGuideGeometry.Create(800, 600).Single(guide => guide.State == state && !guide.IsOuter);
            double x = target.X + target.Size / 2, y = target.Y + target.Size / 2;
            Assert.That(x, Is.InRange(800 * .18, 800 * .82));
            Assert.That(y, Is.InRange(600 * .18, 600 * .82));
            Assert.AreEqual(state, DockDropTargetResolver.Resolve(x, y, 800, 600));
        }

        [TestCase(800, 600), TestCase(240, 240), TestCase(100, 80)]
        public void GuideBoundsStayInsideTheWorkspaceAndDoNotOverlap(double width, double height)
        {
            var targets = DockGuideGeometry.Create(width, height);
            foreach (var target in targets)
            {
                Assert.GreaterOrEqual(target.X, 0); Assert.GreaterOrEqual(target.Y, 0);
                Assert.LessOrEqual(target.X + target.Size, width); Assert.LessOrEqual(target.Y + target.Size, height);
            }
            for (int first = 0; first < targets.Count; first++)
                for (int second = first + 1; second < targets.Count; second++)
                {
                    var a = targets[first]; var b = targets[second];
                    Assert.IsFalse(a.X < b.X + b.Size && a.X + a.Size > b.X && a.Y < b.Y + b.Size && a.Y + a.Size > b.Y);
                }
        }

        [Test]
        public void SpaceBetweenTilesIsNotAnIconTarget()
        {
            var left = DockGuideGeometry.Create(800, 600).Single(guide => guide.State == DockState.Left && !guide.IsOuter);
            Assert.IsNull(DockGuideGeometry.HitTest(left.X + left.Size + 1, left.Y + left.Size / 2, 800, 600));
            Assert.AreEqual(DockState.Document, DockDropTargetResolver.Resolve(left.X + left.Size + 1, left.Y + left.Size / 2, 800, 600));
        }
        [TestCase(200, 100, 300, 200), TestCase(600, 300, 200, 300), TestCase(0, 200, 80, 200)]
        public void LocalCrossIsCenteredInTheHoveredPaneAndStaysClearOfOuterTiles(double x, double y, double width, double height)
        {
            var tiles = DockGuideGeometry.Create(800, 600, x, y, width, height);
            var center = tiles.Single(tile => !tile.IsOuter && tile.State == DockState.Document);
            Assert.AreEqual(x + width / 2, center.X + center.Size / 2, .00001);
            Assert.AreEqual(y + height / 2, center.Y + center.Size / 2, .00001);
            Assert.AreEqual(5, tiles.Count(tile => !tile.IsOuter));
            foreach (var tile in tiles.Where(tile => !tile.IsOuter))
            {
                Assert.GreaterOrEqual(tile.X, x); Assert.GreaterOrEqual(tile.Y, y);
                Assert.LessOrEqual(tile.X + tile.Size, x + width); Assert.LessOrEqual(tile.Y + tile.Size, y + height);
                foreach (var outer in tiles.Where(candidate => candidate.IsOuter))
                    Assert.IsFalse(tile.X < outer.X + outer.Size && tile.X + tile.Size > outer.X && tile.Y < outer.Y + outer.Size && tile.Y + tile.Size > outer.Y);
            }
        }

        [TestCase(800, 600), TestCase(280, 280), TestCase(120, 90)]
        public void CustomGuideMetricsKeepAllRenderedTilesInsideTheHostAndResolveTheirCenters(double width, double height)
        {
            var metrics = new DockGuideMetrics { TileSize = 40, Step = 46, PlateSize = 140, ScaleThreshold = 280, OuterMargin = 20 };
            var targets = DockGuideGeometry.Create(width, height, metrics);
            foreach (var tile in targets)
            {
                Assert.GreaterOrEqual(tile.X, 0); Assert.GreaterOrEqual(tile.Y, 0);
                Assert.LessOrEqual(tile.X + tile.Size, width); Assert.LessOrEqual(tile.Y + tile.Size, height);
                Assert.AreEqual(tile.State, DockDropTargetResolver.Resolve(tile.X + tile.Size / 2, tile.Y + tile.Size / 2, width, height, metrics));
                foreach (var other in targets.Where(candidate => candidate.X != tile.X || candidate.Y != tile.Y))
                    Assert.IsFalse(tile.X < other.X + other.Size && tile.X + tile.Size > other.X && tile.Y < other.Y + other.Size && tile.Y + tile.Size > other.Y);
            }
        }

        [TestCase("size"), TestCase("step"), TestCase("plate"), TestCase("threshold"), TestCase("margin")]
        public void InvalidGuideMetricsAreRejectedBeforeHitTesting(string defect)
        {
            var metrics = new DockGuideMetrics();
            switch (defect)
            {
                case "size": metrics.TileSize = double.NaN; break;
                case "step": metrics.Step = 10; break;
                case "plate": metrics.PlateSize = 50; break;
                case "threshold": metrics.ScaleThreshold = 100; break;
                case "margin": metrics.OuterMargin = -1; break;
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => DockDropTargetResolver.Resolve(100, 100, 800, 600, metrics));
        }

        [Test]
        public void NullExplicitGuideMetricsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => DockDropTargetResolver.Resolve(100, 100, 800, 600, null));
        }

        [TestCase(-1, 0, 200, 200), TestCase(0, 0, 0, 200), TestCase(700, 0, 200, 200), TestCase(0, 0, double.NaN, 200)]
        public void InvalidPaneBoundsAreRejected(double x, double y, double width, double height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DockGuideGeometry.Create(800, 600, x, y, width, height));
        }
        [TestCase(0, 10), TestCase(-1, 10), TestCase(10, 0), TestCase(10, -1)]
        [TestCase(double.NaN, 10), TestCase(10, double.PositiveInfinity)]
        public void InvalidWorkspaceDimensionsAreRejected(double width, double height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DockDropTargetResolver.Resolve(10, 10, width, height));
        }

        [Test]
        public void InvalidPointerCoordinatesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DockDropTargetResolver.Resolve(double.NaN, 0, 100, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => DockDropTargetResolver.Resolve(0, double.NegativeInfinity, 100, 100));
        }
    }
}
