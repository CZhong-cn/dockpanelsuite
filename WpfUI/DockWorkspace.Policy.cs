using System;
using System.Linq;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private static bool AllowsPlacement(DockItem item, DockState state) =>
            (!IsEdge(state) || item.CanDockToSides) &&
            (state != DockState.AutoHide || item.CanAutoHide);

        private static void VerifyPlacementAllowed(DockItem item, DockState state)
        {
            if (!AllowsPlacement(item, state))
                throw new InvalidOperationException("The pane does not allow this dock state: " + state);
        }

        private bool CanAutoHideGroup(DockItem item)
        {
            var group = FindGroup(item);
            return group != null && GroupItems(group)
                .Where(member => member.State == item.State).All(member => member.CanAutoHide);
        }

        private bool CanTogglePin(DockItem item)
        {
            if (item.Owner != this) return false;
            if (item.State == DockState.AutoHide) return AllowsPlacement(item, item.LastDockState);
            if (item.State == DockState.Float) return item.CanAutoHide;
            return Docked(item.State) && CanAutoHideGroup(item);
        }
    }
}
