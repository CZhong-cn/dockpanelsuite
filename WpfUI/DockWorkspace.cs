using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;

namespace DockPanelSuite.Wpf
{
    /// <summary>Native WPF workspace. All access must be on its dispatcher thread.</summary>
    public sealed class DockWorkspace : UserControl
    {
        private readonly ObservableCollection<DockItem> _items = new ObservableCollection<DockItem>();
        private readonly Dictionary<DockItem, ContentPresenter> _presenters = new Dictionary<DockItem, ContentPresenter>();
        private readonly Dictionary<DockItem, Window> _floats = new Dictionary<DockItem, Window>();
        private readonly Dictionary<DockState, string> _selected = new Dictionary<DockState, string>();
        private readonly Dictionary<DockState, TabControl> _tabs = new Dictionary<DockState, TabControl>();
        private readonly HashSet<Window> _closing = new HashSet<Window>();
        private Grid _grid;
        private bool _rendering;
        private DockLayout _sizes = new DockLayout();

        public DockWorkspace()
        {
            Items = new ReadOnlyObservableCollection<DockItem>(_items);
            Loaded += (sender, args) => Render();
            Unloaded += (sender, args) => CloseFloatingWindows();
            Render();
        }

        public ReadOnlyObservableCollection<DockItem> Items { get; }
        public DockItem ActiveItem { get; private set; }
        public event EventHandler LayoutChanged;

        public void Add(DockItem item, DockState state = DockState.Document)
        {
            Dispatcher.VerifyAccess();
            if (item == null) throw new ArgumentNullException(nameof(item));
            ValidateState(state);
            if (item.Owner != null || _items.Any(other => other.Id == item.Id))
                throw new ArgumentException("The item is already owned or its ID is duplicated.", nameof(item));
            if (item.Content is DependencyObject dependency && dependency.Dispatcher != Dispatcher)
                throw new ArgumentException("Content must share the workspace dispatcher.", nameof(item));
            if (_items.Any(other => ReferenceEquals(other.Content, item.Content)))
                throw new ArgumentException("Content must not be shared by two items.", nameof(item));
            item.Owner = this;
            _presenters.Add(item, new ContentPresenter { Content = item.Content });
            _items.Add(item);
            Show(item, state);
        }

        public void Show(DockItem item, DockState state)
        {
            RequireItem(item);
            ValidateState(state);
            CaptureGeometry();
            item.SetState(state);
            if (state != DockState.Hidden)
            {
                _selected[state] = item.Id;
                ActiveItem = item;
            }
            else if (ActiveItem == item) ActiveItem = null;
            Render();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Show(DockItem item) { RequireItem(item); Show(item, item.LastDockState); }
        public void Hide(DockItem item) { RequireItem(item); if (item.CanClose) Show(item, DockState.Hidden); }
        public void Float(DockItem item) => Show(item, DockState.Float);
        public void Float(DockItem item, Rect bounds)
        {
            RequireItem(item);
            ValidateBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            CaptureGeometry();
            item.FloatingBounds = bounds;
            // Avoid recapturing the old window's geometry after setting the requested bounds.
            if (_floats.TryGetValue(item, out var window)) CloseFloat(item, window);
            Show(item, DockState.Float);
        }

        public void Remove(DockItem item)
        {
            RequireItem(item);
            CaptureGeometry();
            _items.Remove(item);
            Render();
            _presenters[item].Content = null;
            _presenters.Remove(item);
            item.Owner = null;
            if (ActiveItem == item) ActiveItem = null;
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SaveLayout(Stream stream)
        {
            Dispatcher.VerifyAccess();
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            CaptureGeometry();
            var layout = new DockLayout
            {
                LeftWidth = _sizes.LeftWidth, RightWidth = _sizes.RightWidth,
                TopHeight = _sizes.TopHeight, BottomHeight = _sizes.BottomHeight,
                SelectedIds = _selected.Values.Distinct().ToList(),
                Items = _items.Select(item => new DockPlacement
                {
                    Id = item.Id, State = item.State, LastDockState = item.LastDockState,
                    X = item.FloatingBounds.X, Y = item.FloatingBounds.Y,
                    Width = item.FloatingBounds.Width, Height = item.FloatingBounds.Height
                }).ToList()
            };
            new XmlSerializer(typeof(DockLayout)).Serialize(stream, layout);
        }

        public void LoadLayout(Stream stream)
        {
            Dispatcher.VerifyAccess();
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            DockLayout layout;
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = false, MaxCharactersInDocument = 1000000 }))
                layout = (DockLayout)new XmlSerializer(typeof(DockLayout)).Deserialize(reader);
            // Validate the entire snapshot before modifying the live UI.
            if (layout.Version != 1 || layout.Items == null || layout.SelectedIds == null)
                throw new InvalidDataException("Unsupported or incomplete layout.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in layout.Items)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
                    throw new InvalidDataException("Missing or duplicate item identity.");
                ValidateState(entry.State);
                ValidateState(entry.LastDockState);
                if (entry.LastDockState == DockState.Float || entry.LastDockState == DockState.Hidden)
                    throw new InvalidDataException("Invalid previous dock state.");
                ValidateBounds(entry.X, entry.Y, entry.Width, entry.Height);
            }
            foreach (var size in new[] { layout.LeftWidth, layout.RightWidth, layout.TopHeight, layout.BottomHeight })
                if (!Finite(size) || size < 40 || size > 10000) throw new InvalidDataException("Invalid pane size.");
            CloseFloatingWindows();
            _sizes = layout;
            _selected.Clear();
            // Unknown IDs are ignored; registered content omitted from a full snapshot is hidden.
            foreach (var item in _items)
            {
                var entry = layout.Items.FirstOrDefault(candidate => candidate.Id == item.Id);
                item.SetState(entry?.State ?? DockState.Hidden);
                if (entry != null)
                {
                    item.LastDockState = entry.LastDockState;
                    item.FloatingBounds = new Rect(entry.X, entry.Y, entry.Width, entry.Height);
                }
            }
            foreach (var id in layout.SelectedIds)
            {
                var item = _items.FirstOrDefault(candidate => candidate.Id == id);
                if (item != null && item.State != DockState.Hidden) _selected[item.State] = id;
            }
            ActiveItem = null;
            Render();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RequireItem(DockItem item)
        {
            Dispatcher.VerifyAccess();
            if (item == null || item.Owner != this) throw new ArgumentException("Item is not owned by this workspace.", nameof(item));
        }
        private static void ValidateState(DockState state)
        {
            if (!Enum.IsDefined(typeof(DockState), state)) throw new ArgumentOutOfRangeException(nameof(state));
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static void ValidateBounds(double x, double y, double width, double height)
        {
            if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || width < 100 || height < 80 || width > 10000 || height > 10000)
                throw new InvalidDataException("Invalid floating window bounds.");
        }

        private void CaptureGeometry()
        {
            if (_grid != null && _grid.ActualWidth > 0)
            {
                if (_grid.ColumnDefinitions[0].ActualWidth >= 40) _sizes.LeftWidth = _grid.ColumnDefinitions[0].ActualWidth;
                if (_grid.ColumnDefinitions[4].ActualWidth >= 40) _sizes.RightWidth = _grid.ColumnDefinitions[4].ActualWidth;
                if (_grid.RowDefinitions[0].ActualHeight >= 40) _sizes.TopHeight = _grid.RowDefinitions[0].ActualHeight;
                if (_grid.RowDefinitions[4].ActualHeight >= 40) _sizes.BottomHeight = _grid.RowDefinitions[4].ActualHeight;
            }
            foreach (var pair in _floats)
            {
                var window = pair.Value;
                var bounds = window.WindowState == WindowState.Normal
                    ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
                if (!bounds.IsEmpty) pair.Key.FloatingBounds = bounds;
            }
        }

        private void Render()
        {
            if (_rendering) return;
            _rendering = true;
            try
            {
                // Detach the reusable presenters before moving them between logical parents.
                foreach (var tab in _tabs.Values)
                {
                    foreach (TabItem page in tab.Items) page.Content = null;
                    tab.Items.Clear();
                }
                _tabs.Clear();
                foreach (var window in _floats.Values)
                {
                    if (window.Content is System.Windows.Controls.DockPanel frame) frame.Children.Clear();
                    window.Content = null;
                }
                foreach (var pair in _floats.ToArray())
                    if (!_items.Contains(pair.Key) || pair.Key.State != DockState.Float) CloseFloat(pair.Key, pair.Value);

                _grid = new Grid { Background = SystemColors.ControlBrush, AllowDrop = true };
                foreach (var width in new[] { _sizes.LeftWidth, 5, -1, 5, _sizes.RightWidth })
                    _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(width) });
                foreach (var height in new[] { _sizes.TopHeight, 5, -1, 5, _sizes.BottomHeight })
                    _grid.RowDefinitions.Add(new RowDefinition { Height = height < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(height) });
                AddZone(DockState.Document, 2, 2);
                AddZone(DockState.Left, 2, 0);
                AddZone(DockState.Right, 2, 4);
                AddZone(DockState.Top, 0, 0, 5);
                AddZone(DockState.Bottom, 4, 0, 5);
                AddSplitter(DockState.Left, 2, 1, false);
                AddSplitter(DockState.Right, 2, 3, false);
                AddSplitter(DockState.Top, 1, 0, true);
                AddSplitter(DockState.Bottom, 3, 0, true);
                _grid.Drop += OnDrop;
                _grid.DragOver += (sender, args) =>
                {
                    args.Effects = GetDraggedItem(args) == null ? DragDropEffects.None : DragDropEffects.Move;
                    args.Handled = true;
                };
                Content = _grid;
                if (IsLoaded)
                    foreach (var item in _items.Where(candidate => candidate.State == DockState.Float)) RenderFloat(item);
            }
            finally { _rendering = false; }
        }

        private void AddZone(DockState state, int row, int column, int span = 1)
        {
            var items = _items.Where(item => item.State == state).ToList();
            if (state != DockState.Document && items.Count == 0)
            {
                if (state == DockState.Left || state == DockState.Right) _grid.ColumnDefinitions[column].Width = new GridLength(0);
                else _grid.RowDefinitions[row].Height = new GridLength(0);
                return;
            }
            var tabs = new TabControl();
            foreach (var item in items)
                tabs.Items.Add(new TabItem { Header = Header(item), Content = _presenters[item], Tag = item });
            if (_selected.TryGetValue(state, out var id))
                tabs.SelectedItem = tabs.Items.Cast<TabItem>().FirstOrDefault(tab => ((DockItem)tab.Tag).Id == id);
            if (tabs.SelectedItem == null && tabs.Items.Count > 0) tabs.SelectedIndex = 0;
            if (tabs.SelectedItem is TabItem current) _selected[state] = ((DockItem)current.Tag).Id;
            tabs.SelectionChanged += (sender, args) =>
            {
                if (args.Source != tabs || _rendering || !(tabs.SelectedItem is TabItem selected)) return;
                var item = (DockItem)selected.Tag;
                _selected[state] = item.Id;
                ActiveItem = item;
            };
            _tabs.Add(state, tabs);
            Place(tabs, row, column, span);
        }

        private void AddSplitter(DockState state, int row, int column, bool horizontal)
        {
            bool visible = _items.Any(item => item.State == state);
            if (horizontal) _grid.RowDefinitions[row].Height = new GridLength(visible ? 5 : 0);
            else _grid.ColumnDefinitions[column].Width = new GridLength(visible ? 5 : 0);
            if (!visible) return;
            var splitter = new GridSplitter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                ResizeDirection = horizontal ? GridResizeDirection.Rows : GridResizeDirection.Columns,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                Background = SystemColors.ControlDarkBrush
            };
            splitter.DragCompleted += (sender, args) => { CaptureGeometry(); LayoutChanged?.Invoke(this, EventArgs.Empty); };
            Place(splitter, row, column, horizontal ? 5 : 1);
        }

        private void Place(UIElement element, int row, int column, int span)
        {
            Grid.SetRow(element, row); Grid.SetColumn(element, column); Grid.SetColumnSpan(element, span);
            _grid.Children.Add(element);
        }

        private FrameworkElement Header(DockItem item)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            var title = new TextBlock { Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center };
            title.SetBinding(TextBlock.TextProperty, new Binding(nameof(DockItem.Title)) { Source = item });
            header.Children.Add(title);
            var close = new Button { Content = "×", Padding = new Thickness(3, 0, 3, 0), ToolTip = "Hide" };
            close.SetBinding(IsEnabledProperty, new Binding(nameof(DockItem.CanClose)) { Source = item });
            close.Click += (sender, args) => Hide(item);
            header.Children.Add(close);
            var menu = new ContextMenu();
            foreach (DockState state in Enum.GetValues(typeof(DockState)))
            {
                if (state == DockState.Hidden) continue;
                var command = new MenuItem { Header = state.ToString() };
                command.Click += (sender, args) => Show(item, state);
                menu.Items.Add(command);
            }
            header.ContextMenu = menu;
            Point start = default;
            bool armed = false;
            title.MouseLeftButtonDown += (sender, args) => { start = args.GetPosition(title); armed = true; };
            title.MouseMove += (sender, args) =>
            {
                if (!armed || args.LeftButton != MouseButtonState.Pressed) { armed = false; return; }
                var current = args.GetPosition(title);
                if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                armed = false;
                DragDrop.DoDragDrop(title, new DataObject(typeof(DockItem), item), DragDropEffects.Move);
            };
            return header;
        }

        private DockItem GetDraggedItem(DragEventArgs args)
        {
            var item = args.Data.GetData(typeof(DockItem)) as DockItem;
            return item?.Owner == this ? item : null;
        }

        private void OnDrop(object sender, DragEventArgs args)
        {
            var item = GetDraggedItem(args);
            if (item == null) return;
            var point = args.GetPosition(_grid);
            var x = point.X / Math.Max(1, _grid.ActualWidth);
            var y = point.Y / Math.Max(1, _grid.ActualHeight);
            var state = x < .18 ? DockState.Left : x > .82 ? DockState.Right :
                y < .18 ? DockState.Top : y > .82 ? DockState.Bottom : DockState.Document;
            Show(item, state);
            args.Effects = DragDropEffects.Move;
            args.Handled = true;
        }

        private void RenderFloat(DockItem item)
        {
            if (!_floats.TryGetValue(item, out var window))
            {
                var bounds = item.FloatingBounds;
                // Keep restored windows reachable when a monitor has been removed.
                var area = SystemParameters.WorkArea;
                window = new Window
                {
                    Left = Math.Max(area.Left, Math.Min(bounds.X, area.Right - 100)),
                    Top = Math.Max(area.Top, Math.Min(bounds.Y, area.Bottom - 80)),
                    Width = bounds.Width, Height = bounds.Height, MinWidth = 100, MinHeight = 80,
                    ShowInTaskbar = false, Owner = Window.GetWindow(this)
                };
                window.SetBinding(Window.TitleProperty, new Binding(nameof(DockItem.Title)) { Source = item });
                window.Closing += (sender, args) =>
                {
                    if (!_closing.Contains(window) && !item.CanClose) args.Cancel = true;
                };
                window.Closed += (sender, args) =>
                {
                    if (_closing.Contains(window)) return;
                    if (window.Content is System.Windows.Controls.DockPanel oldFrame) oldFrame.Children.Clear();
                    window.Content = null;
                    _floats.Remove(item);
                    if (item.Owner == this) Show(item, DockState.Hidden);
                };
                _floats.Add(item, window);
            }
            var frame = new System.Windows.Controls.DockPanel();
            var header = Header(item);
            System.Windows.Controls.DockPanel.SetDock(header, Dock.Top);
            frame.Children.Add(header);
            frame.Children.Add(_presenters[item]);
            window.Content = frame;
            if (!window.IsVisible) window.Show();
        }

        private void CloseFloat(DockItem item, Window window)
        {
            // Detach presenters inside the floating frame, not just the frame from the window.
            if (window.Content is System.Windows.Controls.DockPanel frame) frame.Children.Clear();
            window.Content = null;
            _closing.Add(window);
            try { window.Close(); }
            finally { _closing.Remove(window); _floats.Remove(item); }
        }

        private void CloseFloatingWindows()
        {
            CaptureGeometry();
            foreach (var pair in _floats.ToArray()) CloseFloat(pair.Key, pair.Value);
        }
    }
}
