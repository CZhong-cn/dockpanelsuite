using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DockPanelSuite.Wpf.Sample
{
    public partial class MainWindow : Window
    {
        private int _document;
        private bool _ready;
        private ResourceDictionary _sampleThemeResources;
        private TextBlock _activeTitle;
        private byte[] _defaultLayout;
        private readonly string _layoutFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DockPanelSuite.Wpf.Sample", "layout.xml");
        public DockWorkspace Workspace => Docking;

        public MainWindow()
        {
            InitializeComponent();
            SetTheme(DockTheme.Dark);
            Docking.Add(new DockItem("preview", "检测预览", new PreviewView()));
            AddDocument();
            var tree = new TreeView();
            tree.SetResourceReference(StyleProperty, "Dps.TreeView");
            var station = new TreeViewItem { Header = "▣  检测工位 A", IsExpanded = true };
            foreach (var group in new[] { "采集设备", "算法流程", "检测配方", "数据与报表" })
            {
                var node = new TreeViewItem { Header = group, IsExpanded = true, Margin = new Thickness(0, 5, 0, 5) };
                node.Items.Add(new TreeViewItem { Header = group == "采集设备" ? "Camera 01" : group == "算法流程" ? "表面检测.flow" : group == "检测配方" ? "零件 A · 默认" : "检测记录" });
                station.Items.Add(node);
            }
            tree.Items.Add(station);
            Docking.Add(new DockItem("explorer", "项目资源", tree), DockState.Left);
            Docking.Add(new DockItem("properties", "属性检查器", BuildInspector()), DockState.Right);
            Docking.Add(new DockItem("output", "输出", Editor("[示例] 工作台初始化完成\n[示例] 文档、工具面板与主题已就绪\n提示：拖动标题可停靠，双击标题可浮动。", true)), DockState.Bottom);
            Docking.Add(new DockItem("history", "历史记录", Editor("示例操作记录\n\n• 加载默认工作区\n• 注册检测预览\n• 启用自动隐藏面板", true)), DockState.Right);
            Docking.AutoHide(Docking.Items.Single(item => item.Id == "history"));
            Docking.ActiveItemChanged += (sender, args) => UpdateActiveItem();
            Docking.LayoutChanged += (sender, args) => { if (_ready) Status.Text = "布局已更改 · 可保存当前工作区"; };
            _ready = true;
            Docking.Activate(Docking.Items.First());
            UpdateActiveItem();
            using (var stream = new MemoryStream())
            {
                Docking.SaveLayout(stream);
                _defaultLayout = stream.ToArray();
            }
        }

        public void SetTheme(DockTheme theme)
        {
            if (_sampleThemeResources != null) Resources.MergedDictionaries.Remove(_sampleThemeResources);
            _sampleThemeResources = DockThemeResources.Create(theme);
            Resources.MergedDictionaries.Add(_sampleThemeResources);
            Docking.Theme = theme;
            ThemeButton.Content = theme == DockTheme.Dark ? "☀  浅色模式" : "☾  深色模式";
        }

        private FrameworkElement BuildInspector()
        {
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = "当前面板", FontSize = 11 });
            _activeTitle = new TextBlock { Text = "检测预览", FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 24), TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(_activeTitle);
            foreach (var pair in new[] { new[] { "内容类型", "原生 WPF 控件" }, new[] { "工作区", "检测工位 A" }, new[] { "布局操作", "停靠 / 浮动 / 自动隐藏" }, new[] { "交互提示", "右键标题可选择停靠位置" } })
            {
                var label = new TextBlock { Text = pair[0], FontSize = 10, Margin = new Thickness(0, 0, 0, 6) };
                label.SetResourceReference(TextBlock.ForegroundProperty, "Dps.Muted");
                panel.Children.Add(label);
                panel.Children.Add(new TextBlock { Text = pair[1], TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
            }
            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void UpdateActiveItem()
        {
            if (!_ready) return;
            var item = Docking.ActiveItem;
            _activeTitle.Text = item?.Title ?? "未选择面板";
            Status.Text = item == null ? "工作台就绪" : "当前面板：" + item.Title;
        }

        private TextBox Editor(string text, bool readOnly = false)
        {
            var editor = new TextBox { Text = text, IsReadOnly = readOnly, AcceptsReturn = true, AcceptsTab = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas, Microsoft YaHei UI"), FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            editor.SetResourceReference(StyleProperty, "Dps.TextBox");
            return editor;
        }

        private void AddDocument()
        {
            var number = ++_document;
            Docking.Add(new DockItem("document-" + number, number == 1 ? "流程脚本.json" : "未命名 " + number,
                Editor("{\n  \"station\": \"检测工位 A\",\n  \"recipe\": \"零件表面检测\",\n  \"steps\": [\n    \"图像采集\",\n    \"定位与校正\",\n    \"尺寸测量\",\n    \"结果判定\"\n  ]\n}\n")));
        }
        private void NewDocument(object sender, RoutedEventArgs args) => AddDocument();
        private void ToggleTheme(object sender, RoutedEventArgs args) => SetTheme(Docking.Theme == DockTheme.Dark ? DockTheme.Light : DockTheme.Dark);

        private void ChoosePane(object sender, RoutedEventArgs args)
        {
            var menu = new ContextMenu { PlacementTarget = (Button)sender, Placement = PlacementMode.Bottom };
            menu.Resources.MergedDictionaries.Add(_sampleThemeResources);
            menu.SetResourceReference(StyleProperty, "Dps.Menu");
            foreach (var item in Docking.Items)
            {
                var command = new MenuItem { Header = item.Title, IsCheckable = true, IsChecked = item.State != DockState.Hidden };
                command.Click += (s, e) => Docking.Activate(item);
                menu.Items.Add(command);
            }
            menu.IsOpen = true;
        }

        private void ResetLayout(object sender, RoutedEventArgs args)
        {
            using (var stream = new MemoryStream(_defaultLayout)) Docking.LoadLayout(stream);
            Status.Text = "已恢复默认布局";
        }

        private void SaveLayout(object sender, RoutedEventArgs args) => WithErrorDialog(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_layoutFile));
            var temporary = _layoutFile + ".tmp";
            using (var stream = File.Create(temporary)) Docking.SaveLayout(stream);
            if (File.Exists(_layoutFile)) File.Replace(temporary, _layoutFile, null);
            else File.Move(temporary, _layoutFile);
            Status.Text = "布局已保存";
        });
        private void RestoreLayout(object sender, RoutedEventArgs args) => WithErrorDialog(() =>
        {
            if (!File.Exists(_layoutFile)) { Status.Text = "尚无保存的布局，请先保存"; return; }
            using (var stream = File.OpenRead(_layoutFile)) Docking.LoadLayout(stream);
            Status.Text = "布局已恢复";
        });
        private void WithErrorDialog(Action action)
        {
            try { action(); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "布局操作", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}
