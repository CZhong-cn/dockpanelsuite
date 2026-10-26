using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private static void ValidateLayoutTrees(DockLayout layout)
        {
            if (layout.Version < 3) return;
            if (layout.Regions == null) throw new InvalidDataException("Missing region trees.");
            var states = new HashSet<DockState>();
            var groups = new HashSet<string>(StringComparer.Ordinal);
            var members = new HashSet<string>(StringComparer.Ordinal);
            var entries = layout.Items.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            int nodes = 0;
            foreach (var region in layout.Regions)
            {
                if (region == null || !Docked(region.State) || !states.Add(region.State)) throw new InvalidDataException("Invalid or duplicate tree region.");
                ValidateNode(region.Root, region.State, 0, ref nodes, groups, members, entries);
            }
            if (layout.Items.Any(entry => !members.Contains(entry.Id))) throw new InvalidDataException("An item has no tab group.");
        }

        private static void ValidateNode(DockLayoutNode node, DockState state, int depth, ref int count,
            HashSet<string> groups, HashSet<string> members, Dictionary<string, DockPlacement> entries)
        {
            if (node == null || depth > 32 || ++count > 1024) throw new InvalidDataException("Invalid or excessive layout tree.");
            if (node is DockTabGroup group)
            {
                if (string.IsNullOrWhiteSpace(group.Id) || !groups.Add(group.Id) || group.ItemIds == null)
                    throw new InvalidDataException("Invalid tab group identity.");
                foreach (var id in group.ItemIds)
                {
                    if (id == null || !entries.TryGetValue(id, out var entry) || !members.Add(id))
                        throw new InvalidDataException("Unknown or duplicated tree member.");
                    if ((Docked(entry.State) ? entry.State : entry.LastDockState) != state || (Docked(entry.State) && entry.LastDockState != state))
                        throw new InvalidDataException("Tree membership does not match the dock region.");
                }
                if (group.SelectedId != null && !group.ItemIds.Contains(group.SelectedId)) throw new InvalidDataException("Invalid selected tab.");
                return;
            }
            if (!(node is DockSplit split) || !Enum.IsDefined(typeof(DockSplitOrientation), split.Orientation) ||
                !Finite(split.Ratio) || split.Ratio < .05 || split.Ratio > .95)
                throw new InvalidDataException("Invalid split geometry.");
            ValidateNode(split.First, state, depth + 1, ref count, groups, members, entries);
            ValidateNode(split.Second, state, depth + 1, ref count, groups, members, entries);
        }

        private DockLayoutNode RestoreNode(DockLayoutNode node)
        {
            if (node is DockTabGroup group)
            {
                var ids = group.ItemIds.Where(id => _items.Any(item => item.Id == id)).ToList();
                return ids.Count == 0 ? null : new DockTabGroup { Id = group.Id, ItemIds = ids, SelectedId = ids.Contains(group.SelectedId) ? group.SelectedId : null };
            }
            var source = (DockSplit)node;
            var first = RestoreNode(source.First); var second = RestoreNode(source.Second);
            return first == null ? second : second == null ? first : new DockSplit
            { Orientation = source.Orientation, Ratio = source.Ratio, First = first, Second = second };
        }

        private void RestoreLayoutTrees(DockLayout layout)
        {
            _roots.Clear();
            if (layout.Version >= 3)
                foreach (var region in layout.Regions)
                {
                    var restored = RestoreNode(region.Root);
                    if (restored != null) _roots.Add(region.State, restored);
                }
            foreach (var item in _items) EnsureItemGroup(item, item.State);
            if (layout.Version < 3)
                foreach (var id in layout.SelectedIds)
                {
                    var item = _items.FirstOrDefault(candidate => candidate.Id == id);
                    if (item != null && Docked(item.State)) SelectInGroup(item);
                }
            foreach (var group in _roots.Values.SelectMany(Groups))
            {
                var item = _items.FirstOrDefault(candidate => candidate.Id == group.SelectedId && Docked(candidate.State));
                if (item != null) _selected[item.State] = item.Id;
            }
        }
    }
}
