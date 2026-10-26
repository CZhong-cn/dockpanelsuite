using System;
using System.Windows;

namespace DockPanelSuite.Wpf
{
    /// <summary>Resource keys are public so applications can override the docking chrome.</summary>
    public static class DockThemeResources
    {
        public static ResourceDictionary Create(DockTheme theme)
        {
            if (!Enum.IsDefined(typeof(DockTheme), theme)) throw new ArgumentOutOfRangeException(nameof(theme));
            return new ResourceDictionary
            {
                Source = new Uri($"/WpfUI;component/Themes/{theme}.xaml", UriKind.Relative)
            };
        }
    }
}
