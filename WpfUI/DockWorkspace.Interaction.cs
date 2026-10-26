using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DockPanelSuite.Wpf
{
    public sealed partial class DockWorkspace
    {
        private ResourceDictionary _themeResources;
        private Grid _surface;
        private Border _flyout;
        private Border _dropPreview;
        private DockItem _autoHideItem;
        private Window _inputOwner;
        private readonly List<FrameworkElement> _rails = new List<FrameworkElement>();

        public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
            nameof(Theme), typeof(DockTheme), typeof(DockWorkspace),
            new PropertyMetadata(DockTheme.Light, (sender, args) => ((DockWorkspace)sender).ApplyTheme()),
            value => Enum.IsDefined(typeof(DockTheme), value));

        public static readonly DependencyProperty IsCtrlWEnabledProperty = DependencyProperty.Register(
            nameof(IsCtrlWEnabled), typeof(bool), typeof(DockWorkspace), new PropertyMetadata(false));

        /// <summary>Enables Ctrl+W to close the active pane. Disabled by default; Ctrl+F4 is independent.</summary>
        public bool IsCtrlWEnabled { get => (bool)GetValue(IsCtrlWEnabledProperty); set => SetValue(IsCtrlWEnabledProperty, value); }

        public DockTheme Theme { get => (DockTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
        public DockItem OpenAutoHideItem => _autoHideItem;

        private void InitializeInteraction()
        {
            ApplyTheme();
            SetResourceReference(StyleProperty, "Dps.Workspace");
            ApplyDefaultSizes();
            PreviewKeyDown += OnWorkspaceKeyDown;
            Loaded += (sender, args) =>
            {
                if (!_initialSizingApplied) { ApplyDefaultSizes(); _initialSizingApplied = true; }
                AttachOwner();
            };
            Unloaded += (sender, args) =>
            {
                if (_inputOwner != null) _inputOwner.PreviewMouseDown -= OnOwnerMouseDown;
                _inputOwner = null;
            };
        }

        private void ApplyTheme()
        {
            var palette = DockThemeResources.Create(Theme);
            if (_themeResources == null)
            {
                _themeResources = palette;
                Resources.MergedDictionaries.Add(_themeResources);
            }
            else
            {
                // Keep the shared dictionary identity: popup menus and floating windows
                // must observe the same live palette as docked panes.
                foreach (System.Collections.DictionaryEntry entry in palette) _themeResources[entry.Key] = entry.Value;
            }
        }

        private void ApplyFloatingTheme(Window window)
        {
            window.Resources.MergedDictionaries.Clear();
            window.Resources.MergedDictionaries.Add(Resources);
            window.SetResourceReference(StyleProperty, "Dps.FloatingWindow");
        }

        private static bool IsEdge(DockState state) => state == DockState.Left || state == DockState.Right || state == DockState.Top || state == DockState.Bottom;

        public void AutoHide(DockItem item)
        {
            RequireItem(item);
            Show(item, DockState.AutoHide);
        }

        public void AutoHide(DockItem item, DockState edge)
        {
            RequireItem(item);
            if (!IsEdge(edge)) throw new ArgumentException("Choose a tool-pane edge.", nameof(edge));
            item.LastDockState = edge;
            AutoHide(item);
        }

        public void Pin(DockItem item) { RequireItem(item); Show(item, item.LastDockState); }

        public void OpenAutoHide(DockItem item)
        {
            RequireItem(item);
            if (item.State != DockState.AutoHide) throw new ArgumentException("The item is not auto-hidden.", nameof(item));
            CaptureGeometry();
            _autoHideItem = item;
            SetActiveItem(item);
            Render();
            FocusContent(item);
        }

        public void DismissAutoHide()
        {
            Dispatcher.VerifyAccess();
            bool restoreFocus = _flyout?.IsKeyboardFocusWithin == true;
            _autoHideItem = null;
            DetachFlyout();
            EnsureActiveItem();
            if (restoreFocus && ActiveItem != null) FocusContent(ActiveItem);
        }

        public void Activate(DockItem item)
        {
            RequireItem(item);
            if (item.State == DockState.AutoHide) OpenAutoHide(item);
            else Show(item, item.State == DockState.Hidden ? item.LastDockState : item.State);
            FocusContent(item);
        }

        public void ActivateNextDocument(bool reverse = false)
        {
            Dispatcher.VerifyAccess();
            var documents = _items.Where(item => item.State == DockState.Document).ToList();
            if (documents.Count == 0) return;
            int current = documents.IndexOf(ActiveItem);
            int next = current < 0 ? (reverse ? documents.Count - 1 : 0) :
                (current + (reverse ? documents.Count - 1 : 1)) % documents.Count;
            Activate(documents[next]);
        }

        public void MoveBefore(DockItem item, DockItem before)
        {
            RequireItem(item); RequireItem(before);
            if (item == before) return;
            if (!Docked(item.State) || item.State != before.State) throw new ArgumentException("Items must share a visible dock region.", nameof(before));
            CaptureGeometry();
            bool preserveSelection = FindGroup(item)?.SelectedId == item.Id || ActiveItem == item;
            var group = FindGroup(before);
            if (group != null)
            {
                RemoveFromGroups(item.Id);
                group.ItemIds.Insert(group.ItemIds.IndexOf(before.Id), item.Id);
                if (preserveSelection) group.SelectedId = item.Id;
                if (!_roots.Values.SelectMany(Groups).Contains(group)) _roots[item.State] = group;
            }
            int source = _items.IndexOf(item), target = _items.IndexOf(before);
            _items.Move(source, target - (source < target ? 1 : 0));
            Render();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool CanHide(DockItem item)
        {
            if (!item.CanClose) return false;
            var args = new DockItemClosingEventArgs(item);
            ItemClosing?.Invoke(this, args);
            return !args.Cancel;
        }

        private void SetActiveItem(DockItem item)
        {
            if (ReferenceEquals(item, ActiveItem)) return;
            ActiveItem = item;
            ActiveItemChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ActivateFromContent(DockItem item)
        {
            if (!_rendering && item.Owner == this && item.State != DockState.Hidden &&
                (item.State != DockState.AutoHide || _autoHideItem == item)) SetActiveItem(item);
        }

        private void EnsureActiveItem()
        {
            if (ActiveItem != null && ActiveItem.Owner == this && _items.Contains(ActiveItem) && ActiveItem.State != DockState.Hidden &&
                (ActiveItem.State != DockState.AutoHide || _autoHideItem == ActiveItem)) return;
            _selected.TryGetValue(DockState.Document, out var id);
            var selected = _tabs.Values.Select(tab => (tab.SelectedItem as TabItem)?.Tag as DockItem).Where(item => item != null).ToList();
            SetActiveItem(selected.FirstOrDefault(item => item.State == DockState.Document && item.Id == id) ??
                selected.FirstOrDefault(item => item.State == DockState.Document) ?? selected.FirstOrDefault() ??
                _items.FirstOrDefault(item => item.State == DockState.Float));
        }

        private void FocusContent(DockItem item)
        {
            if (!IsLoaded) return;
            // Wait until the rebuilt presenters have been measured and loaded.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (item.Owner != this || ActiveItem != item || item.State == DockState.Hidden) return;
                var target = item.State == DockState.Float && _floats.TryGetValue(item, out var floating)
                    ? floating : Window.GetWindow(this);
                target?.Activate();
                _presenters[item].MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }));
        }

        private void OnWorkspaceKeyDown(object sender, KeyEventArgs args)
        {
            var modifiers = Keyboard.Modifiers;
            bool control = (modifiers & ModifierKeys.Control) != 0;
            if (control && args.Key == Key.Tab)
            {
                ActivateNextDocument((modifiers & ModifierKeys.Shift) != 0);
                args.Handled = true;
            }
            else if (TryHandleCloseShortcut(args.Key, modifiers)) args.Handled = true;
            else if (args.Key == Key.Escape && _autoHideItem != null)
            {
                DismissAutoHide(); args.Handled = true;
            }
        }

        internal bool TryHandleCloseShortcut(Key key, ModifierKeys modifiers)
        {
            if ((modifiers & ModifierKeys.Control) == 0 || ActiveItem == null ||
                (key != Key.F4 && (key != Key.W || !IsCtrlWEnabled))) return false;
            Hide(ActiveItem);
            return true;
        }

        private void AttachOwner()
        {
            var owner = Window.GetWindow(this);
            if (_inputOwner == owner) return;
            if (_inputOwner != null) _inputOwner.PreviewMouseDown -= OnOwnerMouseDown;
            _inputOwner = owner;
            if (owner != null) owner.PreviewMouseDown += OnOwnerMouseDown;
        }

        private void OnOwnerMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (_autoHideItem != null && _flyout != null && !_flyout.IsMouseOver && !_rails.Any(rail => rail.IsMouseOver))
                DismissAutoHide();
        }

        private void BuildSurface()
        {
            _surface = new Grid();
            _surface.SetResourceReference(StyleProperty, "Dps.DockSurface");
            for (int i = 0; i < 3; i++)
            {
                _surface.RowDefinitions.Add(new RowDefinition { Height = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
                _surface.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            }
            Grid.SetRow(_grid, 1); Grid.SetColumn(_grid, 1);
            _surface.Children.Add(_grid);
            _rails.Clear();
            AddRail(DockState.Left, 1, 0); AddRail(DockState.Right, 1, 2);
            AddRail(DockState.Top, 0, 1); AddRail(DockState.Bottom, 2, 1);
            if (_autoHideItem != null && _autoHideItem.Owner == this && _autoHideItem.State == DockState.AutoHide) BuildFlyout(_autoHideItem);
            Content = _surface;
        }

        private FrameworkElement Header(DockItem item, bool toolTab = false, bool toolPaneHeader = false)
        {
            var header = new DockItemHeader { Item = item, IsToolTab = toolTab, IsToolPaneHeader = toolPaneHeader, AllowDrop = item.State != DockState.Float };
            header.SetResourceReference(StyleProperty, "Dps.DockItemHeader");
            header.CommandBindings.Add(new CommandBinding(DockItemHeader.CloseCommand,
                (sender, args) => { Hide(item); args.Handled = true; },
                (sender, args) => { args.CanExecute = item.Owner == this && item.CanClose; args.Handled = true; }));
            header.CommandBindings.Add(new CommandBinding(DockItemHeader.TogglePinCommand,
                (sender, args) => { ToggleGroupPin(item); args.Handled = true; },
                (sender, args) => { args.CanExecute = item.Owner == this && (IsEdge(item.State) || item.State == DockState.AutoHide); args.Handled = true; }));
            var menu = new ContextMenu();
            menu.Resources.MergedDictionaries.Add(Resources);
            menu.SetResourceReference(StyleProperty, "Dps.Menu");
            foreach (DockState state in new[] { DockState.Document, DockState.Left, DockState.Right, DockState.Top, DockState.Bottom, DockState.Float })
            {
                var command = new MenuItem { Header = RegionName(state) };
                command.Click += (sender, args) => Show(item, state);
                menu.Items.Add(command);
            }
            if (IsEdge(item.LastDockState))
            {
                menu.Items.Add(new Separator());
                var pin = new MenuItem { Header = item.State == DockState.AutoHide ? "固定整组 / Pin group" : "自动隐藏整组 / Auto-hide group" };
                pin.Click += (sender, args) => ToggleGroupPin(item);
                menu.Items.Add(pin);
            }
            header.ContextMenu = menu;
            header.SetValue(DockHeaderItemProperty, item);
            Point start = default;
            bool armed = false;
            header.MouseLeftButtonDown += (sender, args) =>
            {
                armed = false;
                if (!(args.OriginalSource is DependencyObject source) || !DockItemHeader.GetIsDragHandle(source)) return;
                if (args.ClickCount == 2)
                {
                    if (item.State == DockState.Float) Pin(item); else Float(item);
                    args.Handled = true;
                }
                else { start = args.GetPosition(header); armed = true; }
            };
            header.MouseLeftButtonUp += (sender, args) => armed = false;
            header.MouseMove += (sender, args) =>
            {
                if (!armed || args.LeftButton != MouseButtonState.Pressed) { armed = false; return; }
                var current = args.GetPosition(header);
                if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                armed = false;
                try { DragDrop.DoDragDrop(header, new DataObject(typeof(DockItem), item), DragDropEffects.Move); }
                finally { ClearDropPreview(); }
            };
            return header;
        }

        private static string RegionName(DockState state)
        {
            switch (state)
            {
                case DockState.Left: return "停靠到左侧 / Dock left";
                case DockState.Right: return "停靠到右侧 / Dock right";
                case DockState.Top: return "停靠到顶部 / Dock top";
                case DockState.Bottom: return "停靠到底部 / Dock bottom";
                case DockState.Float: return "浮动窗口 / Float";
                default: return "文档标签页 / Document";
            }
        }
    }
}
