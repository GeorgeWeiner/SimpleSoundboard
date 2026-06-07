using System;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SimpleSoundboard.Controls;

public partial class LogoControl : UserControl
{
    public LogoControl()
    {
        InitializeComponent();

        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://SimpleSoundboard/Assets/logo.png"));
            LogoImage.Source = new Bitmap(stream);
        }
        catch
        {
            // Missing or invalid logo — show nothing rather than crash the app.
        }
    }
}
