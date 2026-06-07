using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SimpleSoundboard.Controls;

/// <summary>
/// Picks a category for a sound. Returns the chosen category name, "" for None,
/// or null if cancelled.
/// </summary>
public partial class CategoryDialog : Window
{
    private const string NoneLabel = "(None)";

    public CategoryDialog() : this(new List<string>(), "")
    {
    }

    public CategoryDialog(IEnumerable<string> categories, string current)
    {
        InitializeComponent();

        var options = new List<string> { NoneLabel };
        options.AddRange(categories);
        CategoryCombo.ItemsSource = options;
        CategoryCombo.SelectedItem = string.IsNullOrEmpty(current) || !options.Contains(current)
            ? NoneLabel
            : current;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var selected = CategoryCombo.SelectedItem as string;
        Close(selected == NoneLabel ? "" : selected);
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
