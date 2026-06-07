using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SimpleSoundboard.Controls;

public partial class LogoControl : UserControl
{
    public LogoControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
