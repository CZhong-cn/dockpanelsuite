using System;

namespace DockPanelSuite.Wpf
{
    /// <summary>Shared guide layout parameters used by drawing and hit testing.</summary>
    public sealed class DockGuideMetrics : IEquatable<DockGuideMetrics>
    {
        // Compatibility defaults for the standalone, platform-independent resolver.
        // The workspace reads its explicit configuration from Themes/Metrics.xaml.
        public double TileSize { get; set; } = 32;
        public double Step { get; set; } = 38;
        public double PlateSize { get; set; } = 120;
        public double ScaleThreshold { get; set; } = 240;
        public double OuterMargin { get; set; } = 12;

        internal void Validate()
        {
            if (!Positive(TileSize) || !Positive(Step) || !Positive(PlateSize) || !Positive(ScaleThreshold) ||
                double.IsNaN(OuterMargin) || double.IsInfinity(OuterMargin) || OuterMargin < 0 ||
                Step < TileSize || PlateSize < 2 * Step + TileSize || ScaleThreshold < PlateSize || ScaleThreshold < 2 * OuterMargin + TileSize)
                throw new ArgumentOutOfRangeException(nameof(DockGuideMetrics), "Guide tiles must fit inside a non-overlapping plate and host.");
        }
        private static bool Positive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
        internal DockGuideMetrics Copy() => (DockGuideMetrics)MemberwiseClone();
        public bool Equals(DockGuideMetrics other) => other != null && TileSize == other.TileSize && Step == other.Step &&
            PlateSize == other.PlateSize && ScaleThreshold == other.ScaleThreshold && OuterMargin == other.OuterMargin;
        public override bool Equals(object obj) => Equals(obj as DockGuideMetrics);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TileSize.GetHashCode();
                hash = hash * 397 ^ Step.GetHashCode();
                hash = hash * 397 ^ PlateSize.GetHashCode();
                hash = hash * 397 ^ ScaleThreshold.GetHashCode();
                return hash * 397 ^ OuterMargin.GetHashCode();
            }
        }
    }
}
