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
            ThemeButton.Content = ThemeName(theme) + " ▾";
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
            var viewer = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            viewer.SetResourceReference(StyleProperty, "Dps.ScrollViewer");
            return viewer;
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
        private static string ThemeName(DockTheme theme) => theme == DockTheme.GreenDark ? "绿色深色 / GreenDark" : theme == DockTheme.Dark ? "深色 / Dark" : "浅色 / Light";
        private void ChooseTheme(object sender, RoutedEventArgs args)
        {
            var button = (Button)sender;
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
            menu.Resources.MergedDictionaries.Add(_sampleThemeResources);
            menu.SetResourceReference(StyleProperty, "Dps.Menu");
            foreach (DockTheme theme in Enum.GetValues(typeof(DockTheme)))
            {
                var command = new MenuItem { Header = ThemeName(theme), Tag = theme, IsCheckable = true, IsChecked = Docking.Theme == theme };
                command.Click += (s, e) => SetTheme(theme);
                menu.Items.Add(command);
            }
            button.ContextMenu = menu;
            menu.IsOpen = true;
        }

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

        private void ShowNestedLayout(object sender, RoutedEventArgs args) => ArrangeNestedLayout();

        public void ArrangeNestedLayout()
        {
            foreach (var item in Docking.Items) Docking.Show(item, DockState.Hidden);
            var documents = Enumerable.Range(1, 5).Select(number =>
            {
                string id = "nested-document-" + number;
                var item = Docking.Items.FirstOrDefault(candidate => candidate.Id == id);
                if (item == null)
                {
                    item = new DockItem(id, "Document" + number, Editor("Document" + number + "\n\n可拖动标签，在任意子窗口内继续分割或合并。\n\n中央箭头：在当前分组内分割\n中央方块：合并为标签\n四边图标：停靠到工作区边缘", true));
                    Docking.Add(item);
                }
                else Docking.Show(item, DockState.Document);
                return item;
            }).ToArray();
            foreach (var item in documents.Skip(1)) Docking.Dock(item, documents[0]);
            Docking.Dock(documents[4], documents[0], DockPosition.Right);
            Docking.Dock(documents[2], documents[0], DockPosition.Bottom);
            Docking.Dock(documents[3], documents[4], DockPosition.Bottom);
            var properties = Docking.Items.Single(item => item.Id == "properties");
            var output = Docking.Items.Single(item => item.Id == "output");
            var solution = Docking.Items.Single(item => item.Id == "explorer");
            Docking.Show(properties, DockState.Right); Docking.Show(output, DockState.Right); Docking.Show(solution, DockState.Right);
            Docking.Dock(output, properties, DockPosition.Bottom, 2d / 3);
            Docking.Dock(solution, output, DockPosition.Bottom);
            var toolbox = Docking.Items.FirstOrDefault(item => item.Id == "nested-toolbox");
            if (toolbox == null) { toolbox = new DockItem("nested-toolbox", "Toolbox", Editor("常规\n\n  指针\n  文本\n  图像\n  控件", true)); Docking.Add(toolbox, DockState.Left); }
            else Docking.Show(toolbox, DockState.Left);
            Docking.Activate(documents[4]);
            Status.Text = "树状停靠布局 · 四个文档组 / 三个右侧工具组";
        }
        private void ShowTabbedToolLayout(object sender, RoutedEventArgs args) => ArrangeTabbedToolLayout();

        public void ArrangeTabbedToolLayout()
        {
            ArrangeNestedLayout();
            foreach (var item in Docking.Items.Where(candidate => candidate.State == DockState.Right).ToArray()) Docking.Show(item, DockState.Hidden);
            var output = Docking.Items.FirstOrDefault(item => item.Id == "group-output");
            if (output == null)
            {
                output = new DockItem("group-output", "Output", Editor("Build started...\n\nWpfUI → native WPF docking library\nWpfSample → workbench sample\n\nBuild succeeded.\n0 errors, 0 warnings", true));
                Docking.Add(output, DockState.Right);
            }
            else Docking.Show(output, DockState.Right);
            var solution = Docking.Items.FirstOrDefault(item => item.Id == "group-solution");
            if (solution == null)
            {
                var tree = new TreeView(); tree.SetResourceReference(StyleProperty, "Dps.TreeView");
                var root = new TreeViewItem { Header = "Solution 'DockPanelSuite'", IsExpanded = true };
                foreach (var name in new[] { "WpfUI", "WpfSample", "WpfTests" })
                {
                    var project = new TreeViewItem { Header = name, IsExpanded = true };
                    project.Items.Add(new TreeViewItem { Header = name == "WpfUI" ? "DockWorkspace.cs" : name == "WpfSample" ? "MainWindow.xaml" : "ToolGroupTests.cs" });
                    root.Items.Add(project);
                }
                tree.Items.Add(root);
                solution = new DockItem("group-solution", "Solution Explorer", tree);
                Docking.Add(solution, DockState.Right);
            }
            else Docking.Show(solution, DockState.Right);
            Docking.Dock(output, solution);
            Docking.MoveBefore(output, solution);
            Docking.Activate(solution);
            Status.Text = "工具组示例 · 底部标签切换 / pin 整组自动隐藏";
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
