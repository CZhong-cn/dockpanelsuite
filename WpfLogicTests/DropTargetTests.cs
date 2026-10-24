using System;
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
