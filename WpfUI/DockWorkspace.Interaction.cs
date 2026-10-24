using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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

        public DockTheme Theme { get => (DockTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
        public DockItem OpenAutoHideItem => _autoHideItem;

        private void InitializeInteraction()
        {
            ApplyTheme();
            SetResourceReference(ForegroundProperty, "Dps.Foreground");
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            FontSize = 12;
            PreviewKeyDown += OnWorkspaceKeyDown;
            Loaded += (sender, args) => AttachOwner();
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
            window.Resources.MergedDictionaries.Add(_themeResources);
            window.SetResourceReference(BackgroundProperty, "Dps.Surface");
            window.SetResourceReference(ForegroundProperty, "Dps.Foreground");
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
            _autoHideItem = null;
            DetachFlyout();
            EnsureActiveItem();
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
            if (item.State != before.State) throw new ArgumentException("Items must share a dock region.", nameof(before));
            CaptureGeometry();
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
            SetActiveItem(_items.FirstOrDefault(item => item.State == DockState.Document && item.Id == id) ??
                _items.FirstOrDefault(item => item.State == DockState.Document || IsEdge(item.State) || item.State == DockState.Float));
        }

        private void FocusContent(DockItem item)
        {
            if (!IsLoaded) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (item.Owner != this || ActiveItem != item || item.State == DockState.Hidden) return;
                _presenters[item].MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }));
        }

        private void OnWorkspaceKeyDown(object sender, KeyEventArgs args)
        {
            bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (control && args.Key == Key.Tab)
            {
                ActivateNextDocument((Keyboard.Modifiers & ModifierKeys.Shift) != 0);
                args.Handled = true;
            }
            else if (control && (args.Key == Key.W || args.Key == Key.F4) && ActiveItem != null)
            {
                Hide(ActiveItem); args.Handled = true;
            }
            else if (args.Key == Key.Escape && _autoHideItem != null)
            {
                DismissAutoHide(); args.Handled = true;
            }
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
            _surface.SetResourceReference(Panel.BackgroundProperty, "Dps.Background");
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

        private void AddRail(DockState edge, int row, int column)
        {
            var items = _items.Where(item => item.State == DockState.AutoHide && item.LastDockState == edge).ToList();
            if (items.Count == 0) return;
            var rail = new StackPanel { Orientation = edge == DockState.Top || edge == DockState.Bottom ? Orientation.Horizontal : Orientation.Vertical };
            foreach (var item in items)
            {
                var button = new Button { Margin = new Thickness(3), Padding = new Thickness(6), ToolTip = "展开 / Expand " + item.Title };
                button.SetBinding(ContentControl.ContentProperty, new Binding(nameof(DockItem.Title)) { Source = item });
                button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(DockItem.Title)) { Source = item });
                button.SetResourceReference(StyleProperty, "Dps.Button");
                if (edge == DockState.Left || edge == DockState.Right) button.LayoutTransform = new System.Windows.Media.RotateTransform(-90);
                button.Click += (sender, args) => { if (_autoHideItem == item) DismissAutoHide(); else OpenAutoHide(item); };
                rail.Children.Add(button);
            }
            Grid.SetRow(rail, row); Grid.SetColumn(rail, column);
            _surface.Children.Add(rail);
            _rails.Add(rail);
        }

        private void BuildFlyout(DockItem item)
        {
            var frame = new System.Windows.Controls.DockPanel();
            var header = Header(item);
            System.Windows.Controls.DockPanel.SetDock(header, Dock.Top);
            frame.Children.Add(header); frame.Children.Add(_presenters[item]);
            _flyout = new Border { Child = frame, BorderThickness = new Thickness(1), Margin = new Thickness(1) };
            _flyout.SetResourceReference(Border.BackgroundProperty, "Dps.Surface");
            _flyout.SetResourceReference(Border.BorderBrushProperty, "Dps.Accent");
            var edge = item.LastDockState;
            _flyout.HorizontalAlignment = edge == DockState.Left ? HorizontalAlignment.Left : edge == DockState.Right ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
            _flyout.VerticalAlignment = edge == DockState.Top ? VerticalAlignment.Top : edge == DockState.Bottom ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            if (edge == DockState.Left || edge == DockState.Right)
                _flyout.Width = Math.Min(Math.Max(220, edge == DockState.Left ? _sizes.LeftWidth : _sizes.RightWidth), _grid.ActualWidth > 0 ? _grid.ActualWidth : 360);
            else _flyout.Height = Math.Min(Math.Max(160, edge == DockState.Top ? _sizes.TopHeight : _sizes.BottomHeight), _grid.ActualHeight > 0 ? _grid.ActualHeight : 240);
            Grid.SetRow(_flyout, 1); Grid.SetColumn(_flyout, 1); Panel.SetZIndex(_flyout, 10);
            _surface.Children.Add(_flyout);
        }

        private void DetachFlyout()
        {
            if (_flyout?.Child is System.Windows.Controls.DockPanel frame) frame.Children.Clear();
            if (_flyout != null) _surface?.Children.Remove(_flyout);
            _flyout = null;
        }

        private FrameworkElement Header(DockItem item)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal, AllowDrop = item.State != DockState.Float };
            var title = new TextBlock { Margin = new Thickness(5, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.SizeAll };
            title.SetBinding(TextBlock.TextProperty, new Binding(nameof(DockItem.Title)) { Source = item });
            header.Children.Add(title);
            if (IsEdge(item.State) || item.State == DockState.AutoHide)
            {
                var pin = HeaderButton(item.State == DockState.AutoHide ? "↗" : "−", item.State == DockState.AutoHide ? "固定面板 / Pin pane" : "自动隐藏 / Auto-hide");
                pin.Click += (sender, args) => { if (item.State == DockState.AutoHide) Pin(item); else AutoHide(item); };
                header.Children.Add(pin);
            }
            var close = HeaderButton("×", "隐藏面板 / Close pane (Ctrl+W)");
            close.SetBinding(IsEnabledProperty, new Binding(nameof(DockItem.CanClose)) { Source = item });
            close.Click += (sender, args) => Hide(item);
            header.Children.Add(close);
            var menu = new ContextMenu();
            menu.Resources.MergedDictionaries.Add(_themeResources);
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
                var pin = new MenuItem { Header = item.State == DockState.AutoHide ? "固定面板 / Pin" : "自动隐藏 / Auto-hide" };
                pin.Click += (sender, args) => { if (item.State == DockState.AutoHide) Pin(item); else AutoHide(item); };
                menu.Items.Add(pin);
            }
            header.ContextMenu = menu;
            header.DragOver += (sender, args) =>
            {
                if (GetDraggedItem(args) == null || item.State == DockState.AutoHide) return;
                ClearDropPreview(); args.Effects = DragDropEffects.Move; args.Handled = true;
            };
            header.Drop += (sender, args) =>
            {
                var dragged = GetDraggedItem(args);
                if (dragged == null || item.State == DockState.AutoHide || item.State == DockState.Float) return;
                ClearDropPreview();
                if (dragged != item)
                {
                    if (dragged.State != item.State) Show(dragged, item.State);
                    MoveBefore(dragged, item);
                    Activate(dragged);
                }
                args.Effects = DragDropEffects.Move; args.Handled = true;
            };
            Point start = default;
            bool armed = false;
            title.MouseLeftButtonDown += (sender, args) =>
            {
                if (args.ClickCount == 2)
                {
                    if (item.State == DockState.Float) Pin(item); else Float(item);
                    args.Handled = true; armed = false;
                }
                else { start = args.GetPosition(title); armed = true; }
            };
            title.MouseMove += (sender, args) =>
            {
                if (!armed || args.LeftButton != MouseButtonState.Pressed) { armed = false; return; }
                var current = args.GetPosition(title);
                if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                armed = false;
                try { DragDrop.DoDragDrop(title, new DataObject(typeof(DockItem), item), DragDropEffects.Move); }
                finally { ClearDropPreview(); }
            };
            return header;
        }

        private Button HeaderButton(string content, string description)
        {
            var button = new Button { Content = content, ToolTip = description };
            button.SetResourceReference(StyleProperty, "Dps.IconButton");
            AutomationProperties.SetName(button, description);
            return button;
        }

        private void OnDragOver(object sender, DragEventArgs args)
        {
            if (GetDraggedItem(args) == null) { ClearDropPreview(); args.Effects = DragDropEffects.None; args.Handled = true; return; }
            var point = args.GetPosition(_grid);
            var state = DockDropTargetResolver.Resolve(point.X, point.Y, Math.Max(1, _grid.ActualWidth), Math.Max(1, _grid.ActualHeight));
            ClearDropPreview();
            _dropPreview = new Border { IsHitTestVisible = false, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(5), Margin = new Thickness(8) };
            _dropPreview.SetResourceReference(Border.BackgroundProperty, "Dps.Preview");
            _dropPreview.SetResourceReference(Border.BorderBrushProperty, "Dps.Accent");
            var label = new TextBlock { Text = RegionName(state), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 17, FontWeight = FontWeights.SemiBold };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Dps.Foreground");
            _dropPreview.Child = label;
            if (state == DockState.Left || state == DockState.Right)
            {
                _dropPreview.Width = _grid.ActualWidth * .32;
                _dropPreview.HorizontalAlignment = state == DockState.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            }
            else if (state == DockState.Top || state == DockState.Bottom)
            {
                _dropPreview.Height = _grid.ActualHeight * .32;
                _dropPreview.VerticalAlignment = state == DockState.Top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            }
            else _dropPreview.Margin = new Thickness(_grid.ActualWidth * .2, _grid.ActualHeight * .2, _grid.ActualWidth * .2, _grid.ActualHeight * .2);
            Grid.SetRowSpan(_dropPreview, 5); Grid.SetColumnSpan(_dropPreview, 5); Panel.SetZIndex(_dropPreview, 20);
            _grid.Children.Add(_dropPreview);
            args.Effects = DragDropEffects.Move; args.Handled = true;
        }

        private void ClearDropPreview()
        {
            if (_dropPreview != null) _grid?.Children.Remove(_dropPreview);
            _dropPreview = null;
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
