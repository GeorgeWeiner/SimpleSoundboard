using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SimpleSoundboard.Controls;

/// <summary>
/// Captures a global hotkey gesture. Returns the gesture string ("Ctrl+Shift+A"),
/// "" to clear, or null if cancelled.
/// </summary>
public partial class HotkeyDialog : Window
{
    private string? _gesture;

    public HotkeyDialog() : this("")
    {
    }

    public HotkeyDialog(string current)
    {
        InitializeComponent();
        _gesture = string.IsNullOrEmpty(current) ? null : current;
        GestureText.Text = _gesture ?? "Press keys…";

        // Capture in the tunnel (preview) phase so we see EVERY key before child
        // controls or the Alt access-key handler can swallow it. handledEventsToo
        // covers keys already marked handled.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        Opened += (_, _) => Focus();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        switch (e.Key)
        {
            case Key.Escape:
                Close(null);
                return;
            case Key.Enter:
                Close(_gesture ?? "");
                return;
            case Key.Delete or Key.Back:
                _gesture = null;
                GestureText.Text = "Press keys…";
                return;
            // Ignore presses that are only a modifier — wait for the real key.
            case Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin:
                return;
        }

        bool hasModifier = e.KeyModifiers != KeyModifiers.None;
        bool isFunctionKey = e.Key is >= Key.F1 and <= Key.F12;
        if (!hasModifier && !isFunctionKey)
        {
            _gesture = null;
            GestureText.Text = $"{e.Key} — add Ctrl / Alt / Shift";
            return;
        }

        _gesture = new KeyGesture(e.Key, e.KeyModifiers).ToString();
        GestureText.Text = _gesture;
    }

    private void OnSave(object? sender, RoutedEventArgs e) => Close(_gesture ?? "");

    private void OnClear(object? sender, RoutedEventArgs e) => Close("");

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
