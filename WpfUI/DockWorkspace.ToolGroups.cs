using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private TabControl _flyoutTabs;

        /// <summary>Auto-hide every visible tool tab in the item's current group.</summary>
        public void AutoHideGroup(DockItem item)
        {
            RequireItem(item);
            if (!IsEdge(item.State)) throw new ArgumentException("The item must be a docked tool pane.", nameof(item));
            CaptureGeometry();
            var group = FindGroup(item);
            var members = GroupItems(group).Where(candidate => candidate.State == item.State).ToArray();
            foreach (var member in members)
            {
                foreach (var pair in _selected.Where(pair => pair.Value == member.Id).ToArray()) _selected.Remove(pair.Key);
                member.SetState(DockState.AutoHide);
            }
            group.SelectedId = item.Id;
            if (members.Contains(ActiveItem)) SetActiveItem(null);
            if (FindGroup(_autoHideItem ?? item) == group) _autoHideItem = null;
            Render();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Pin the auto-hidden tabs in this group back to their saved tree position.</summary>
        public void PinGroup(DockItem item)
        {
            RequireItem(item);
            if (item.State != DockState.AutoHide) throw new ArgumentException("The item must be auto-hidden.", nameof(item));
            CaptureGeometry();
            var group = FindGroup(item);
            var members = GroupItems(group).Where(candidate => candidate.State == DockState.AutoHide && candidate.LastDockState == item.LastDockState).ToArray();
            foreach (var member in members) member.SetState(member.LastDockState);
            group.SelectedId = item.Id;
            _selected[item.State] = item.Id;
            if (FindGroup(_autoHideItem ?? item) == group) _autoHideItem = null;
            SetActiveItem(item);
            Render(); FocusContent(item);
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        private IEnumerable<DockItem> GroupItems(DockTabGroup group) => group.ItemIds.Select(id => _items.FirstOrDefault(candidate => candidate.Id == id)).Where(item => item != null);

        private void ToggleGroupPin(DockItem item)
        {
            if (item.State == DockState.AutoHide) PinGroup(item);
            else if (IsEdge(item.State)) AutoHideGroup(item);
            else AutoHide(item);
        }

        private void RefreshToolCaption(TabControl tabs, DockItem item)
        {
            tabs.Resources["Dps.ToolPaneHeader"] = Header(item, toolPaneHeader: true);
        }

        private TabControl CreateTabs(DockTabGroup group, DockState state, List<DockItem> items, bool flyout = false)
        {
            bool tool = state != DockState.Document;
            var tabs = new TabControl { Tag = state };
            tabs.SetResourceReference(StyleProperty, tool ? "Dps.ToolTabControl" : "Dps.TabControl");
            foreach (var item in items)
            {
                var page = new TabItem { Header = Header(item, toolTab: tool), Content = _presenters[item], Tag = item };
                page.SetResourceReference(StyleProperty, tool ? "Dps.ToolTabItem" : "Dps.TabItem");
                page.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(DockItem.Title)) { Source = item });
                tabs.Items.Add(page);
            }
            string selectedId = flyout ? _autoHideItem?.Id : group.SelectedId;
            tabs.SelectedItem = tabs.Items.Cast<TabItem>().FirstOrDefault(tab => ((DockItem)tab.Tag).Id == selectedId);
            if (tabs.SelectedItem == null && tabs.Items.Count > 0) tabs.SelectedIndex = 0;
            if (tabs.SelectedItem is TabItem current)
            {
                var item = (DockItem)current.Tag;
                group.SelectedId = item.Id;
                if (tool) RefreshToolCaption(tabs, item);
            }
            tabs.SelectionChanged += (sender, args) =>
            {
                if (args.Source != tabs || _rendering || !(tabs.SelectedItem is TabItem selected)) return;
                var item = (DockItem)selected.Tag;
                group.SelectedId = item.Id;
                if (flyout) _autoHideItem = item;
                else _selected[state] = item.Id;
                if (tool) RefreshToolCaption(tabs, item);
                SetActiveItem(item);
                if (flyout) FocusContent(item);
                LayoutChanged?.Invoke(this, EventArgs.Empty);
            };
            if (!flyout) _tabs.Add(group, tabs);
            return tabs;
        }

        private void AddRail(DockState edge, int row, int column)
        {
            if (!_roots.TryGetValue(edge, out var root)) return;
            var groups = Groups(root).Select(group => GroupItems(group).Where(item => item.State == DockState.AutoHide && item.LastDockState == edge).ToArray()).Where(items => items.Length > 0).ToArray();
            if (groups.Length == 0) return;
            var rail = new StackPanel();
            DockChrome.SetEdge(rail, edge);
            rail.SetResourceReference(StyleProperty, "Dps.RailPanel");
            foreach (var items in groups)
            {
                var cluster = new StackPanel();
                cluster.SetResourceReference(StyleProperty, "Dps.RailPanel");
                foreach (var item in items)
                {
                    var button = new Button { Tag = item };
                    button.SetBinding(ContentControl.ContentProperty, new Binding(nameof(DockItem.Title)) { Source = item });
                    button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(DockItem.Title)) { Source = item });
                    button.SetBinding(ToolTipProperty, new Binding(nameof(DockItem.Title)) { Source = item, StringFormat = "展开 / Expand {0}" });
                    button.SetResourceReference(StyleProperty, "Dps.RailButton");
                    button.Click += (sender, args) => { if (_autoHideItem == item) DismissAutoHide(); else OpenAutoHide(item); };
                    cluster.Children.Add(button);
                }
                var frame = new Border { Child = cluster };
                frame.SetResourceReference(StyleProperty, "Dps.RailGroup");
                rail.Children.Add(frame);
            }
            Grid.SetRow(rail, row); Grid.SetColumn(rail, column);
            _surface.Children.Add(rail); _rails.Add(rail);
        }

        private void BuildFlyout(DockItem item)
        {
            var group = FindGroup(item);
            var items = GroupItems(group).Where(candidate => candidate.State == DockState.AutoHide && candidate.LastDockState == item.LastDockState).ToList();
            _flyoutTabs = CreateTabs(group, DockState.AutoHide, items, true);
            _flyout = new Border { Child = _flyoutTabs };
            var edge = item.LastDockState;
            DockChrome.SetEdge(_flyout, edge);
            _flyout.SetResourceReference(StyleProperty, "Dps.AutoHideFlyout");
            if (edge == DockState.Left || edge == DockState.Right) _flyout.Width = Math.Max(Metric("Dps.FlyoutMinWidth"), edge == DockState.Left ? _sizes.LeftWidth : _sizes.RightWidth);
            else _flyout.Height = Math.Max(Metric("Dps.FlyoutMinHeight"), edge == DockState.Top ? _sizes.TopHeight : _sizes.BottomHeight);
            _flyout.SetBinding(MaxWidthProperty, new Binding(nameof(ActualWidth)) { Source = _grid });
            _flyout.SetBinding(MaxHeightProperty, new Binding(nameof(ActualHeight)) { Source = _grid });
            Grid.SetRow(_flyout, 1); Grid.SetColumn(_flyout, 1); Panel.SetZIndex(_flyout, 10);
            _surface.Children.Add(_flyout);
        }

        private void DetachFlyout()
        {
            if (_flyoutTabs != null)
            {
                foreach (TabItem page in _flyoutTabs.Items) page.Content = null;
                _flyoutTabs.Items.Clear(); _flyoutTabs = null;
            }
            if (_flyout != null) { _flyout.Child = null; _surface?.Children.Remove(_flyout); }
            _flyout = null;
        }
    }
}
