using System.ComponentModel;

namespace SimpleSoundboard.Models;

/// <summary>A category tab in the board's tab bar. "All" is a special, fixed tab.</summary>
public sealed class CategoryTab : INotifyPropertyChanged
{
    private bool _isSelected;

    public required string Name { get; init; }
    public bool IsAll { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
