using Avalonia.Media;

namespace SimpleSoundboard.Theming;

/// <summary>
/// A colour theme expressed as five roles. Translucent/hover variants are
/// derived from these by <see cref="ThemeManager"/>.
///   Background — window base / acrylic tint
///   Surface    — cards (toolbar, now-playing, tabs)
///   Primary    — sound-button fill
///   Accent     — outlines, highlights, the Fluent accent (checkbox/slider)
///   Text       — foreground (must contrast Background and Surface)
/// </summary>
public sealed class Theme
{
    public required string Name { get; init; }
    public bool IsDark { get; init; } = true;
    public required Color Background { get; init; }
    public required Color Surface { get; init; }
    public required Color Primary { get; init; }
    public required Color Accent { get; init; }
    public required Color Text { get; init; }
}

public static class Themes
{
    private static Color C(string hex) => Color.Parse(hex);

    public static readonly IReadOnlyList<Theme> All = new[]
    {
        new Theme
        {
            Name = "Deadlock", IsDark = true,
            Background = C("#222021"), Surface = C("#2f4442"),
            Primary = C("#3f5d4d"), Accent = C("#72947f"), Text = C("#efdebf")
        },
        new Theme
        {
            Name = "Gruvbox", IsDark = true,
            Background = C("#282828"), Surface = C("#3c3836"),
            Primary = C("#504945"), Accent = C("#d79921"), Text = C("#ebdbb2")
        },
        new Theme
        {
            Name = "One Dark", IsDark = true,
            Background = C("#282c34"), Surface = C("#333842"),
            Primary = C("#4b5263"), Accent = C("#61afef"), Text = C("#abb2bf")
        },
        new Theme
        {
            Name = "Nord", IsDark = true,
            Background = C("#2e3440"), Surface = C("#3b4252"),
            Primary = C("#434c5e"), Accent = C("#88c0d0"), Text = C("#eceff4")
        },
        new Theme
        {
            Name = "VS Dark", IsDark = true,
            Background = C("#1e1e1e"), Surface = C("#252526"),
            Primary = C("#3e3e42"), Accent = C("#007acc"), Text = C("#d4d4d4")
        },
        new Theme
        {
            Name = "Light", IsDark = false,
            Background = C("#fafafa"), Surface = C("#e4e5f1"),
            Primary = C("#d2d3db"), Accent = C("#9394a5"), Text = C("#484b6a")
        },
        new Theme
        {
            Name = "Light Retro", IsDark = false,
            Background = C("#f6efe6"), Surface = C("#e5c3c6"),
            Primary = C("#bcd2d0"), Accent = C("#f96161"), Text = C("#5e4b4b")
        }
    };

    public static Theme ByName(string? name) =>
        All.FirstOrDefault(t => t.Name == name) ?? All[0];
}
