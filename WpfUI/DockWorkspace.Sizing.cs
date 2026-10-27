using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private readonly HashSet<DockState> _explicitRegionSizes = new HashSet<DockState>();
        private readonly Dictionary<DockState, double> _pendingRegionProportions = new Dictionary<DockState, double>();

        /// <summary>Adds a pane with an initial edge-region width (Left/Right) or height (Top/Bottom), in DIPs. Zero keeps the default sizing.</summary>
        public void Add(DockItem item, DockState state, int size)
        {
            Dispatcher.VerifyAccess();
            if (size < 0 || size > 10000) throw new ArgumentOutOfRangeException(nameof(size), "Choose zero or a size up to 10000 DIPs.");
            if (size != 0) VerifySizingEdge(state);
            AddCore(item, state, size == 0 ? (GridLength?)null : new GridLength(size));
        }

        /// <summary>Adds a pane with an initial edge-region fraction of the docking surface width or height. Zero keeps the default sizing.</summary>
        /// <remarks>Nonzero proportions range from 0.05 to 0.95 and apply once, when the surface has a layout size. Panes on the same edge share its size.</remarks>
        public void Add(DockItem item, DockState state, float proportion)
        {
            Dispatcher.VerifyAccess();
            if (!Finite(proportion) || (proportion != 0 && (proportion < .05f || proportion > .95f)))
                throw new ArgumentOutOfRangeException(nameof(proportion), "Choose zero or a proportion from 0.05 to 0.95.");
            if (proportion != 0) VerifySizingEdge(state);
            AddCore(item, state, proportion == 0 ? (GridLength?)null : new GridLength(proportion, GridUnitType.Star));
        }

        private static void VerifySizingEdge(DockState state)
        {
            if (!IsEdge(state)) throw new ArgumentException("An initial size requires Left, Right, Top or Bottom.", nameof(state));
        }

        private void SetInitialRegionSize(DockState state, GridLength size)
        {
            _explicitRegionSizes.Add(state);
            _pendingRegionProportions.Remove(state);
            if (size.IsStar) _pendingRegionProportions[state] = size.Value;
            else SetRegionSize(state, size.Value);
            LayoutUpdated -= OnInitialRegionLayoutUpdated;
            if (_pendingRegionProportions.Count != 0) LayoutUpdated += OnInitialRegionLayoutUpdated;
        }

        private void SetRegionSize(DockState state, double size)
        {
            // Match the geometry range supported by saved layouts; theme minimums still apply.
            size = Math.Max(40, Math.Min(10000, size));
            if (state == DockState.Left) _sizes.LeftWidth = size;
            else if (state == DockState.Right) _sizes.RightWidth = size;
            else if (state == DockState.Top) _sizes.TopHeight = size;
            else _sizes.BottomHeight = size;
        }

        private bool ApplyPendingRegionProportions()
        {
            if (_pendingRegionProportions.Count == 0 || !IsLoaded || _grid == null || !_grid.IsArrangeValid) return false;
            bool applied = false;
            foreach (var pair in _pendingRegionProportions.ToArray())
            {
                double available = pair.Key == DockState.Left || pair.Key == DockState.Right ? _grid.ActualWidth : _grid.ActualHeight;
                if (!Finite(available) || available <= 0) continue;
                SetRegionSize(pair.Key, available * pair.Value);
                _pendingRegionProportions.Remove(pair.Key);
                applied = true;
            }
            if (_pendingRegionProportions.Count == 0) LayoutUpdated -= OnInitialRegionLayoutUpdated;
            return applied;
        }

        private void OnInitialRegionLayoutUpdated(object sender, EventArgs args)
        {
            if (_rendering || !ApplyPendingRegionProportions()) return;
            if (_items.Any(item => item.State == DockState.Left)) _grid.ColumnDefinitions[0].Width = new GridLength(_sizes.LeftWidth);
            if (_items.Any(item => item.State == DockState.Right)) _grid.ColumnDefinitions[4].Width = new GridLength(_sizes.RightWidth);
            if (_items.Any(item => item.State == DockState.Top)) _grid.RowDefinitions[0].Height = new GridLength(_sizes.TopHeight);
            if (_items.Any(item => item.State == DockState.Bottom)) _grid.RowDefinitions[4].Height = new GridLength(_sizes.BottomHeight);
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearInitialRegionSizes()
        {
            _explicitRegionSizes.Clear();
            _pendingRegionProportions.Clear();
            LayoutUpdated -= OnInitialRegionLayoutUpdated;
        }
    }
}
