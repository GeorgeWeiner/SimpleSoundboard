using System.ComponentModel;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SimpleSoundboard.Theming;

public static class Converters
{
    /// <summary>Wraps a Color in a SolidColorBrush (for theme swatches in XAML).</summary>
    public static readonly FuncValueConverter<Color, IBrush> ColorToBrush =
        new(c => new SolidColorBrush(c));
}

/// <summary>A theme entry in the settings picker, with a live "current" flag.</summary>
public sealed class ThemeOption : INotifyPropertyChanged
{
    private bool _isCurrent;

    public required Theme Theme { get; init; }

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent != value)
            {
                _isCurrent = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
