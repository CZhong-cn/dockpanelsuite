using System;
using System.Windows;
using System.Windows.Media;

namespace DockPanelSuite.Wpf
{
    /// <summary>Resource keys are public so applications can override the docking chrome.</summary>
    public static class DockThemeResources
    {
        public static ResourceDictionary Create(DockTheme theme)
        {
            if (!Enum.IsDefined(typeof(DockTheme), theme)) throw new ArgumentOutOfRangeException(nameof(theme));
            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/WpfUI;component/Themes/Controls.xaml", UriKind.Relative)
            });
            bool dark = theme == DockTheme.Dark;
            Add(resources, "Dps.Background", dark ? "#141A24" : "#E8EDF4");
            Add(resources, "Dps.Surface", dark ? "#1C2431" : "#FFFFFF");
            Add(resources, "Dps.Header", dark ? "#202B3B" : "#F3F6FA");
            Add(resources, "Dps.Foreground", dark ? "#E3EBF7" : "#202D42");
            Add(resources, "Dps.Muted", dark ? "#A2B2C9" : "#5A6D86");
            Add(resources, "Dps.Border", dark ? "#344259" : "#CDD7E4");
            Add(resources, "Dps.Hover", dark ? "#2C3B50" : "#E4EEF9");
            Add(resources, "Dps.Accent", dark ? "#66C5FF" : "#006CB8");
            Add(resources, "Dps.Selected", dark ? "#263E58" : "#DCEEFE");
            Add(resources, "Dps.Preview", dark ? "#663F9EDB" : "#55409CDB");
            Add(resources, "Dps.Success", dark ? "#7EE0B0" : "#176947");
            return resources;
        }

        private static void Add(ResourceDictionary resources, string key, string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            resources[key] = brush;
        }
    }
}
