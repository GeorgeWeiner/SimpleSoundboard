using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SimpleSoundboard.Controls;

/// <summary>Small modal that edits a sound's name. Returns the new name, or null if cancelled.</summary>
public partial class RenameDialog : Window
{
    public RenameDialog() : this("")
    {
    }

    public RenameDialog(string currentName, string title = "Rename sound", string label = "Button name")
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        NameBox.Text = currentName;
        Opened += (_, _) =>
        {
            NameBox.SelectAll();
            NameBox.Focus();
        };
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim();
        Close(string.IsNullOrEmpty(name) ? null : name);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
        }

        base.OnKeyDown(e);
    }
}
