using Avalonia.Controls;
using Avalonia.Interactivity;
using SimpleSoundboard.Theming;

namespace SimpleSoundboard.Controls;

/// <summary>Settings dialog. Currently a live theme picker.</summary>
public partial class SettingsWindow : Window
{
    private readonly Action<Theme>? _onApply;
    private readonly List<ThemeOption> _options;

    public SettingsWindow() : this("Deadlock", null)
    {
    }

    public SettingsWindow(string currentName, Action<Theme>? onApply)
    {
        InitializeComponent();
        _onApply = onApply;
        _options = Themes.All
            .Select(t => new ThemeOption { Theme = t, IsCurrent = t.Name == currentName })
            .ToList();
        ThemeList.ItemsSource = _options;
    }

    private void OnThemeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ThemeOption option })
        {
            foreach (var o in _options)
            {
                o.IsCurrent = o == option;
            }

            _onApply?.Invoke(option.Theme);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
