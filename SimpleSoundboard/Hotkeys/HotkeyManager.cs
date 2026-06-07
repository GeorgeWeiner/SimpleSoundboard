using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Win32;

namespace SimpleSoundboard.Hotkeys;

/// <summary>
/// Registers system-wide hotkeys via Win32 RegisterHotKey and dispatches the
/// resulting WM_HOTKEY messages (received through Avalonia's WndProc hook) to
/// actions. Works even when the window is hidden in the tray.
/// </summary>
public sealed class HotkeyManager
{
    private const uint WmHotkey = 0x0312;
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8, ModNoRepeat = 0x4000;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Window _window;
    private readonly Dictionary<int, Action> _actions = new();
    private IntPtr _hwnd;
    private bool _hooked;
    private int _nextId = 1;

    public HotkeyManager(Window window) => _window = window;

    /// <summary>Attaches the WndProc hook once the window has a native handle.</summary>
    public bool EnsureHooked()
    {
        if (_hooked)
        {
            return true;
        }

        var handle = _window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero)
        {
            return false;
        }

        _hwnd = handle.Handle;
        Win32Properties.AddWndProcHookCallback(_window, WndProc);
        _hooked = true;
        return true;
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action(); // runs on the UI thread (window message pump)
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>Registers a gesture to an action. Returns false if it's unsupported or already taken.</summary>
    public bool TryRegister(KeyGesture gesture, Action action)
    {
        if (!_hooked)
        {
            return false;
        }

        uint vk = KeyToVirtualKey(gesture.Key);
        if (vk == 0)
        {
            return false;
        }

        int id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, ToWin32Modifiers(gesture.KeyModifiers), vk))
        {
            return false; // combo already in use (by us or another app)
        }

        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        if (_hooked)
        {
            foreach (var id in _actions.Keys)
            {
                UnregisterHotKey(_hwnd, id);
            }
        }

        _actions.Clear();
        _nextId = 1;
    }

    private static uint ToWin32Modifiers(KeyModifiers modifiers)
    {
        uint result = ModNoRepeat;
        if (modifiers.HasFlag(KeyModifiers.Alt)) result |= ModAlt;
        if (modifiers.HasFlag(KeyModifiers.Control)) result |= ModControl;
        if (modifiers.HasFlag(KeyModifiers.Shift)) result |= ModShift;
        if (modifiers.HasFlag(KeyModifiers.Meta)) result |= ModWin;
        return result;
    }

    private static uint KeyToVirtualKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return (uint)(key - Key.A) + 0x41;
        if (key >= Key.D0 && key <= Key.D9) return (uint)(key - Key.D0) + 0x30;
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return (uint)(key - Key.NumPad0) + 0x60;
        if (key >= Key.F1 && key <= Key.F12) return (uint)(key - Key.F1) + 0x70;

        return key switch
        {
            Key.Space => 0x20,
            Key.Enter => 0x0D,
            Key.Tab => 0x09,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            Key.Home => 0x24,
            Key.End => 0x23,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.OemTilde => 0xC0,
            Key.OemMinus => 0xBD,
            Key.OemPlus => 0xBB,
            Key.OemComma => 0xBC,
            Key.OemPeriod => 0xBE,
            _ => 0
        };
    }
}
