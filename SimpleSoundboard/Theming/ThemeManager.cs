using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace SimpleSoundboard.Theming;

/// <summary>
/// Applies a <see cref="Theme"/> to the running app by swapping the shared
/// resource brushes (which the XAML references via DynamicResource) and the
/// Fluent accent colours. Derived translucent/hover brushes are computed here
/// so each theme only needs five base colours.
/// </summary>
public static class ThemeManager
{
    public static void Apply(Application app, Theme t)
    {
        var r = app.Resources;

        // Base roles (keys kept from the original Deadlock palette).
        r["Ink"] = B(t.Background);
        r["Surface"] = B(t.Surface);
        r["Primary"] = B(t.Primary);
        r["Sage"] = B(t.Accent);
        r["Cream"] = B(t.Text);

        // Derived variants used across the UI.
        r["SurfaceSoft"] = B(WithAlpha(t.Surface, 0x59));   // translucent cards
        r["Hairline"] = B(WithAlpha(t.Text, 0x33));         // borders / separators
        r["Overlay"] = B(WithAlpha(t.Background, 0xdd));    // drop overlay
        r["BackdropFaint"] = B(WithAlpha(t.Background, 0x33)); // gradients / progress track
        r["PrimaryGlow"] = B(WithAlpha(t.Primary, 0x40));   // top gradient tint
        r["HoverSurface"] = B(WithAlpha(t.Surface, 0x55));  // tab hover
        r["HoverInk"] = B(WithAlpha(t.Background, 0x55));   // mini-button hover
        r["PrimaryHover"] = B(t.IsDark ? Lighten(t.Primary, 0.18) : Darken(t.Primary, 0.12));

        // Fluent accent (checkbox tick, slider, combobox selection).
        r["SystemAccentColor"] = t.Accent;
        r["SystemAccentColorLight1"] = Lighten(t.Accent, 0.2);
        r["SystemAccentColorLight2"] = Lighten(t.Accent, 0.4);
        r["SystemAccentColorLight3"] = Lighten(t.Accent, 0.6);
        r["SystemAccentColorDark1"] = Darken(t.Accent, 0.2);
        r["SystemAccentColorDark2"] = Darken(t.Accent, 0.4);
        r["SystemAccentColorDark3"] = Darken(t.Accent, 0.6);

        app.RequestedThemeVariant = t.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private static SolidColorBrush B(Color c) => new(c);

    private static Color WithAlpha(Color c, byte a) => new(a, c.R, c.G, c.B);

    public static Color Lighten(Color c, double f) => Mix(c, Colors.White, f);

    public static Color Darken(Color c, double f) => Mix(c, Colors.Black, f);

    private static Color Mix(Color a, Color b, double f) => new(
        255,
        (byte)(a.R + (b.R - a.R) * f),
        (byte)(a.G + (b.G - a.G) * f),
        (byte)(a.B + (b.B - a.B) * f));
}
