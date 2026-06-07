using System.Collections.ObjectModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SimpleSoundboard.Audio;
using SimpleSoundboard.Controls;
using SimpleSoundboard.Diagnostics;
using SimpleSoundboard.Models;

namespace SimpleSoundboard;

public partial class MainWindow : Window
{
    private readonly AudioEngine _engine = new();
    private readonly SoundboardConfig _config;
    private readonly ObservableCollection<SoundClip> _sounds;
    private readonly ObservableCollection<Playback> _playing = new();
    private readonly DispatcherTimer _uiTimer;
    private OutputDevice? _vbCable;
    private OutputDevice? _defaultDevice;

    public MainWindow()
    {
        InitializeComponent();
        Icon = LoadIcon();

        _config = SoundboardConfig.Load();
        _sounds = new ObservableCollection<SoundClip>(_config.Sounds);
        SoundList.ItemsSource = _sounds;
        _sounds.CollectionChanged += (_, _) => UpdateEmptyHint();

        // Initialise controls from config BEFORE seeding, because seeding may
        // call SaveConfig() and that reads these controls.
        VolumeSlider.Value = _config.Volume;
        MonitorCheck.IsChecked = _config.RouteToMonitor;
        VbCableCheck.IsChecked = _config.RouteToVbCable;

        DetectDevices();

        SeedDefaultSounds();
        ReorderFavorites();
        UpdateEmptyHint();

        // Now Playing panel
        PlayingList.ItemsSource = _playing;
        NowPlayingEmpty.IsVisible = true;
        _engine.PlaybackStarted += OnPlaybackStarted;
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _uiTimer.Tick += (_, _) =>
        {
            foreach (var playback in _playing)
            {
                playback.Tick();
            }
        };
        _playing.CollectionChanged += (_, _) =>
        {
            NowPlayingEmpty.IsVisible = _playing.Count == 0;
            if (_playing.Count > 0)
            {
                _uiTimer.Start();
            }
            else
            {
                _uiTimer.Stop();
            }
        };

        Closing += (_, _) => SaveConfig();
    }

    private void UpdateEmptyHint() => EmptyHint.IsVisible = _sounds.Count == 0;

    /// <summary>
    /// Adds bundled example sounds from Assets/Sounds the first time each appears.
    /// Tracks seeded file names in config so removed defaults don't come back and
    /// newly-shipped ones are picked up on later launches.
    /// </summary>
    private void SeedDefaultSounds()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
            if (!Directory.Exists(dir))
            {
                return;
            }

            var extensions = new[] { ".wav", ".mp3", ".ogg" };
            var files = Directory.EnumerateFiles(dir)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            bool added = false;
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                if (_config.SeededDefaults.Contains(fileName))
                {
                    continue;
                }

                _sounds.Add(new SoundClip
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    BuiltInFile = fileName
                });
                _config.SeededDefaults.Add(fileName);
                added = true;
            }

            if (added)
            {
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
            Log.Write($"SeedDefaultSounds failed: {ex.Message}");
        }
    }

    /// <summary>Moves favorites to the front, preserving order within each group.</summary>
    private void ReorderFavorites()
    {
        var sorted = _sounds.OrderByDescending(s => s.IsFavorite).ToList();
        if (sorted.SequenceEqual(_sounds))
        {
            return;
        }

        _sounds.Clear();
        foreach (var clip in sorted)
        {
            _sounds.Add(clip);
        }
    }

    /// <summary>
    /// Window/taskbar icon. Prefers the embedded multi-resolution .ico (crisp at
    /// every size); falls back to rendering the vector logo if it's missing.
    /// </summary>
    private static WindowIcon? LoadIcon()
    {
        try
        {
            var uri = new Uri("avares://SimpleSoundboard/Assets/logo.ico");
            return new WindowIcon(AssetLoader.Open(uri));
        }
        catch
        {
            return RenderLogoIcon();
        }
    }

    /// <summary>Renders the vector logo to a bitmap for use as the window/taskbar icon.</summary>
    private static WindowIcon? RenderLogoIcon()
    {
        try
        {
            var logo = new LogoControl();
            var size = new Size(64, 64);
            logo.Measure(size);
            logo.Arrange(new Rect(size));

            using var bitmap = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
            bitmap.Render(logo);

            using var stream = new MemoryStream();
            bitmap.Save(stream);
            stream.Position = 0;
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e) => ToggleMaximize();

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void DetectDevices()
    {
        List<OutputDevice> devices;
        try
        {
            _defaultDevice = _engine.GetDefaultDevice();
            devices = _engine.GetOutputDevices();
            _vbCable = AudioEngine.FindVbCable(devices);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not enumerate audio devices: {ex.Message}";
            return;
        }

        // Monitor picker: "Default (...)" first, then every endpoint.
        var pickable = new List<OutputDevice> { _defaultDevice };
        pickable.AddRange(devices);
        MonitorDeviceCombo.ItemsSource = pickable;
        MonitorDeviceCombo.SelectedItem =
            pickable.FirstOrDefault(d => d.Id == _config.MonitorDeviceId && !d.IsDefault)
            ?? _defaultDevice;

        Log.Write($"DetectDevices: default='{_defaultDevice?.Name}' vb='{_vbCable?.Name ?? "<null>"}' " +
                  $"endpoints={devices.Count}");

        if (_vbCable is null)
        {
            VbCableCheck.IsChecked = false;
            VbCableCheck.IsEnabled = false;
            StatusText.Text = "VB-Cable not found — install VB-Audio Virtual Cable to route into other apps.";
        }
        else
        {
            StatusText.Text = $"VB-Cable detected: \"{_vbCable.Name}\". Other apps: pick \"CABLE Output\" as their mic.";
        }
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add sounds",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Audio files")
                {
                    Patterns = new[] { "*.wav", "*.mp3", "*.ogg" }
                }
            }
        });

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            _sounds.Add(new SoundClip
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FilePath = path
            });
        }

        SaveConfig();
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SoundClip clip })
        {
            return;
        }

        try
        {
            Log.Write($"OnPlayClick '{clip.Name}' file='{clip.EffectivePath}' " +
                      $"monitorChecked={MonitorCheck.IsChecked} " +
                      $"monitorSel='{(MonitorDeviceCombo.SelectedItem as OutputDevice)?.Name ?? "<null>"}' " +
                      $"vbChecked={VbCableCheck.IsChecked} vb='{_vbCable?.Name ?? "<null>"}' " +
                      $"sliderValue={VolumeSlider.Value}");

            clip.Cached ??= new CachedSound(clip.EffectivePath);

            var targets = new List<OutputDevice>();
            if (MonitorCheck.IsChecked == true && MonitorDeviceCombo.SelectedItem is OutputDevice monitor)
            {
                targets.Add(monitor);
            }

            if (VbCableCheck.IsChecked == true && _vbCable is not null)
            {
                targets.Add(_vbCable);
            }

            if (targets.Count == 0)
            {
                Log.Write("  no targets selected");
                StatusText.Text = "No output selected — enable Monitor and/or VB-Cable.";
                return;
            }

            var vol = (float)(VolumeSlider.Value / 100.0);
            Log.Write($"  calling engine.Play with {targets.Count} target(s), vol={vol:0.00}");
            _engine.Play(clip.Cached, targets, vol, clip.Name);
            StatusText.Text = $"Playing \"{clip.Name}\".";
        }
        catch (Exception ex)
        {
            Log.Write($"  EXCEPTION: {ex}");
            StatusText.Text = $"Could not play \"{clip.Name}\": {ex.Message}";
        }
    }

    private async void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SoundClip clip })
        {
            return;
        }

        var newName = await new RenameDialog(clip.Name).ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(newName))
        {
            clip.Name = newName; // INotifyPropertyChanged updates the button text
            SaveConfig();
        }
    }

    private void OnFavoriteClick(object? sender, RoutedEventArgs e)
    {
        // IsChecked is two-way bound, so clip.IsFavorite is already toggled here.
        if (sender is MenuItem { DataContext: SoundClip })
        {
            ReorderFavorites();
            SaveConfig();
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SoundClip clip })
        {
            _sounds.Remove(clip);
            SaveConfig();
        }
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        _engine.StopAll();
        StatusText.Text = "Stopped.";
    }

    // --- Now Playing ---

    private void OnPlaybackStarted(Playback playback)
    {
        // Play() is invoked on the UI thread, so we can touch the collection here.
        playback.Finished += OnPlaybackFinished;
        _playing.Add(playback);
    }

    private void OnPlaybackFinished(Playback playback)
    {
        // Fired from the audio thread — marshal back to the UI thread to remove it.
        Dispatcher.UIThread.Post(() =>
        {
            playback.Finished -= OnPlaybackFinished;
            _playing.Remove(playback);
        });
    }

    private void OnStopPlayback(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Playback playback })
        {
            playback.Stop();
        }
    }

    private void SaveConfig()
    {
        _config.Sounds = _sounds.ToList();
        _config.Volume = VolumeSlider.Value;
        _config.RouteToMonitor = MonitorCheck.IsChecked == true;
        _config.RouteToVbCable = VbCableCheck.IsChecked == true;
        var selected = MonitorDeviceCombo.SelectedItem as OutputDevice;
        _config.MonitorDeviceId = selected is { IsDefault: false } ? selected.Id : null;
        _config.Save();
    }
}
