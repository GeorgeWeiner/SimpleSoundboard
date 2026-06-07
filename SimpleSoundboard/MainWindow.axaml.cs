using System.Collections.ObjectModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SimpleSoundboard.Audio;
using SimpleSoundboard.Controls;
using SimpleSoundboard.Diagnostics;
using SimpleSoundboard.Hotkeys;
using SimpleSoundboard.Models;
using SimpleSoundboard.Theming;

namespace SimpleSoundboard;

public partial class MainWindow : Window
{
    private readonly AudioEngine _engine = new();
    private readonly SoundboardConfig _config;
    private readonly ObservableCollection<SoundClip> _sounds;
    private readonly ObservableCollection<SoundClip> _visibleSounds = new();
    private readonly ObservableCollection<CategoryTab> _tabs = new();
    private string? _activeCategory;
    private readonly HashSet<SoundClip> _selection = new();
    private SoundClip? _selectionAnchor;
    private KeyModifiers _lastModifiers;
    private readonly ObservableCollection<Playback> _playing = new();
    private readonly DispatcherTimer _uiTimer;
    private HotkeyManager? _hotkeys;
    private OutputDevice? _vbCable;
    private OutputDevice? _defaultDevice;
    private OutputDevice? _defaultMic;
    private bool _suppressMicEvents;

    public MainWindow()
    {
        InitializeComponent();
        Icon = LoadIcon();

        _config = SoundboardConfig.Load();
        UpdateWindowChrome(Themes.ByName(_config.Theme)); // resources already set by App
        _sounds = new ObservableCollection<SoundClip>(_config.Sounds);
        SoundList.ItemsSource = _visibleSounds;
        CategoryTabs.ItemsSource = _tabs;
        _activeCategory = ResolveCategory(_config.SelectedCategory);

        // Initialise controls from config BEFORE seeding, because seeding may
        // call SaveConfig() and that reads these controls.
        VolumeSlider.Value = _config.Volume;
        MonitorCheck.IsChecked = _config.RouteToMonitor;
        VbCableCheck.IsChecked = _config.RouteToVbCable;
        MicCheck.IsChecked = _config.RouteMic;

        DetectDevices();
        DetectInputDevices();
        ApplyMicRouting();

        SeedDefaultSounds();
        ReorderFavorites();
        RebuildTabs();
        RefreshVisibleSounds();

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

        // Capture modifiers at press time and handle right-click / background
        // selection. handledEventsToo so it still runs when a button handles it.
        // Tunnel fires before the button handles the press, so we see the
        // modifiers and can drive selection without fighting the click.
        SoundList.AddHandler(PointerPressedEvent, OnSoundPressed, RoutingStrategies.Tunnel);

        // Drag-and-drop audio files onto the window to add them.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        Closing += OnWindowClosing;

        // Global hotkeys need the native window handle, available once opened.
        Opened += (_, _) =>
        {
            _hotkeys = new HotkeyManager(this);
            if (_hotkeys.EnsureHooked())
            {
                RegisterAllHotkeys();
            }
        };

    }

    // --- Categories ---

    private string? ResolveCategory(string? name) =>
        !string.IsNullOrEmpty(name) && _config.Categories.Contains(name) ? name : null;

    /// <summary>Rebuilds the tab bar: "All" plus every category, marking the active one.</summary>
    private void RebuildTabs()
    {
        _tabs.Clear();
        _tabs.Add(new CategoryTab { Name = "All", IsAll = true, IsSelected = _activeCategory is null });
        foreach (var category in _config.Categories)
        {
            _tabs.Add(new CategoryTab { Name = category, IsSelected = category == _activeCategory });
        }
    }

    /// <summary>Rebuilds the visible grid from the master list, filtered by the active tab.</summary>
    private void RefreshVisibleSounds()
    {
        _visibleSounds.Clear();
        foreach (var clip in _sounds)
        {
            if (_activeCategory is null || clip.Category == _activeCategory)
            {
                _visibleSounds.Add(clip);
            }
        }

        EmptyHint.IsVisible = _visibleSounds.Count == 0;
    }

    private void SetActiveCategory(string? category)
    {
        ClearSelection(); // selection shouldn't span category tabs
        _activeCategory = category;
        foreach (var tab in _tabs)
        {
            tab.IsSelected = tab.IsAll ? category is null : tab.Name == category;
        }

        RefreshVisibleSounds();
        _config.SelectedCategory = category;
        SaveConfig();
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CategoryTab tab })
        {
            SetActiveCategory(tab.IsAll ? null : tab.Name);
        }
    }

    private async void OnAddCategory(object? sender, RoutedEventArgs e)
    {
        var name = await new RenameDialog("", "New category", "Category name").ShowDialog<string?>(this);
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (name == "All" || _config.Categories.Contains(name))
        {
            StatusText.Text = $"Category \"{name}\" already exists.";
            return;
        }

        _config.Categories.Add(name);
        RebuildTabs();
        SetActiveCategory(name); // selects it + saves
    }

    private async void OnRenameCategory(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CategoryTab tab } || tab.IsAll)
        {
            return;
        }

        var newName = await new RenameDialog(tab.Name, "Rename category", "Category name")
            .ShowDialog<string?>(this);
        newName = newName?.Trim();
        if (string.IsNullOrEmpty(newName) || newName == tab.Name)
        {
            return;
        }

        if (newName == "All" || _config.Categories.Contains(newName))
        {
            StatusText.Text = $"Category \"{newName}\" already exists.";
            return;
        }

        var old = tab.Name;
        int index = _config.Categories.IndexOf(old);
        if (index >= 0)
        {
            _config.Categories[index] = newName;
        }

        foreach (var clip in _sounds.Where(s => s.Category == old))
        {
            clip.Category = newName;
        }

        if (_activeCategory == old)
        {
            _activeCategory = newName;
        }

        RebuildTabs();
        RefreshVisibleSounds();
        _config.SelectedCategory = _activeCategory;
        SaveConfig();
    }

    private void OnDeleteCategory(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CategoryTab tab } || tab.IsAll)
        {
            return;
        }

        _config.Categories.Remove(tab.Name);
        foreach (var clip in _sounds.Where(s => s.Category == tab.Name))
        {
            clip.Category = "";
        }

        if (_activeCategory == tab.Name)
        {
            _activeCategory = null;
        }

        RebuildTabs();
        RefreshVisibleSounds();
        _config.SelectedCategory = _activeCategory;
        SaveConfig();
        StatusText.Text = $"Deleted category \"{tab.Name}\".";
    }

    private async void OnSetCategory(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SoundClip clip })
        {
            return;
        }

        var targets = ActionTargets(clip);
        var current = targets.Count == 1 ? clip.Category : "";
        var chosen = await new CategoryDialog(_config.Categories, current).ShowDialog<string?>(this);
        if (chosen is null) // cancelled
        {
            return;
        }

        foreach (var target in targets)
        {
            target.Category = chosen;
        }

        ClearSelection();
        RefreshVisibleSounds(); // they may leave the current filtered view
        SaveConfig();
    }

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

    private async void OnSettings(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_config.Theme, ApplyTheme);
        await dialog.ShowDialog(this);
    }

    /// <summary>Applies a theme live: resources, window chrome, and persistence.</summary>
    private void ApplyTheme(Theme theme)
    {
        ThemeManager.Apply(Application.Current!, theme);
        UpdateWindowChrome(theme);
        _config.Theme = theme.Name;
        SaveConfig();
    }

    /// <summary>Re-tints the acrylic glass and the depth gradient for a theme.</summary>
    private void UpdateWindowChrome(Theme theme)
    {
        if (AcrylicBorder.Material is ExperimentalAcrylicMaterial material)
        {
            material.TintColor = theme.Background;
        }

        var bg = theme.Background;
        var primary = theme.Primary;
        BackgroundGradient.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x40, primary.R, primary.G, primary.B), 0),
                new GradientStop(Color.FromArgb(0x00, bg.R, bg.G, bg.B), 0.45),
                new GradientStop(Color.FromArgb(0x33, bg.R, bg.G, bg.B), 1)
            }
        };
    }

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private bool _allowClose;

    /// <summary>Lets the next close actually exit (used by the tray "Quit" item).</summary>
    public void AllowClose() => _allowClose = true;

    /// <summary>Restores the window from the system tray.</summary>
    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// Closing the window hides it to the tray instead of quitting, so playback
    /// keeps running. A real exit only happens via the tray "Quit" item.
    /// </summary>
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveConfig();

        if (_allowClose)
        {
            _hotkeys?.UnregisterAll();
            _engine.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
        StatusText.Text = "Running in the tray — playback continues.";
    }

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

    private void DetectInputDevices()
    {
        List<OutputDevice> inputs;
        try
        {
            _defaultMic = _engine.GetDefaultInputDevice();
            // Exclude the cable's own capture side (CABLE Output) — routing it
            // into CABLE Input would create an audio feedback loop.
            inputs = _engine.GetInputDevices()
                .Where(d => !d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase)
                            && !d.Name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Write($"DetectInputDevices failed: {ex.Message}");
            MicCheck.IsEnabled = false;
            MicDeviceCombo.IsEnabled = false;
            return;
        }

        _suppressMicEvents = true;
        try
        {
            var pickable = new List<OutputDevice>();
            if (_defaultMic is not null)
            {
                pickable.Add(_defaultMic);
            }

            pickable.AddRange(inputs);
            MicDeviceCombo.ItemsSource = pickable;
            MicDeviceCombo.SelectedItem =
                pickable.FirstOrDefault(d => d.Id == _config.MicDeviceId && !d.IsDefault)
                ?? _defaultMic
                ?? pickable.FirstOrDefault();

            // Mic routing only makes sense with both a microphone and VB-Cable.
            bool canRoute = _vbCable is not null && pickable.Count > 0;
            MicCheck.IsEnabled = canRoute;
            MicDeviceCombo.IsEnabled = canRoute;
            if (!canRoute)
            {
                MicCheck.IsChecked = false;
            }
        }
        finally
        {
            _suppressMicEvents = false;
        }
    }

    /// <summary>Starts or stops the mic-into-VB-Cable passthrough to match the UI.</summary>
    private void ApplyMicRouting()
    {
        _engine.StopMicPassthrough();

        if (MicCheck.IsChecked != true || _vbCable is null ||
            MicDeviceCombo.SelectedItem is not OutputDevice mic)
        {
            return;
        }

        try
        {
            _engine.StartMicPassthrough(mic, _vbCable);
            StatusText.Text = $"Mic → VB-Cable live ({mic.Name}).";
        }
        catch (Exception ex)
        {
            Log.Write($"Mic passthrough failed: {ex}");
            StatusText.Text = $"Could not start mic routing: {ex.Message}";
            _suppressMicEvents = true;
            MicCheck.IsChecked = false;
            _suppressMicEvents = false;
        }
    }

    private void OnMicToggle(object? sender, RoutedEventArgs e)
    {
        if (_suppressMicEvents)
        {
            return;
        }

        ApplyMicRouting();
        SaveConfig();
    }

    private void OnMicDeviceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressMicEvents)
        {
            return;
        }

        if (MicCheck.IsChecked == true)
        {
            ApplyMicRouting();
        }

        SaveConfig();
    }

    private static readonly string[] AudioExtensions = { ".wav", ".mp3", ".ogg" };

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

        AddSoundFiles(files.Select(f => f.TryGetLocalPath()));
    }

    /// <summary>Adds audio files (by path) as new sounds, skipping non-audio. Returns the count added.</summary>
    private int AddSoundFiles(IEnumerable<string?> paths)
    {
        int added = 0;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !AudioExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                continue;
            }

            _sounds.Add(new SoundClip
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FilePath = path,
                Category = _activeCategory ?? ""
            });
            added++;
        }

        if (added > 0)
        {
            RefreshVisibleSounds();
            SaveConfig();
        }

        return added;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool hasFiles = e.Data.Contains(DataFormats.Files);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.IsVisible = hasFiles;
    }

    private void OnDragLeave(object? sender, RoutedEventArgs e) => DropOverlay.IsVisible = false;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;

        var files = e.Data.GetFiles();
        if (files is null)
        {
            return;
        }

        int added = AddSoundFiles(files.Select(f => f.TryGetLocalPath()));
        StatusText.Text = added > 0
            ? $"Added {added} sound{(added == 1 ? "" : "s")}."
            : "No supported audio files in that drop.";
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SoundClip clip })
        {
            return;
        }

        // Ctrl / Shift turn the click into a (de)selection instead of a play.
        // Modifiers are captured at press time in OnSoundPressed.
        if (_lastModifiers.HasFlag(KeyModifiers.Control))
        {
            ToggleSelect(clip);
            return;
        }

        if (_lastModifiers.HasFlag(KeyModifiers.Shift))
        {
            RangeSelect(clip);
            return;
        }

        ClearSelection();
        PlayClip(clip);
    }

    private void PlayClip(SoundClip clip)
    {
        try
        {
            Log.Write($"PlayClip '{clip.Name}' file='{clip.EffectivePath}' " +
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
            Log.Write($"  calling engine.Play with {targets.Count} target(s), vol={vol:0.00} loop={clip.IsLooping}");
            _engine.Play(clip.Cached, targets, vol, clip.Name, clip.IsLooping);
            StatusText.Text = clip.IsLooping ? $"Looping \"{clip.Name}\"." : $"Playing \"{clip.Name}\".";
        }
        catch (Exception ex)
        {
            Log.Write($"  EXCEPTION: {ex}");
            StatusText.Text = $"Could not play \"{clip.Name}\": {ex.Message}";
        }
    }

    // --- Multi-selection ---

    private void OnSoundPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastModifiers = e.KeyModifiers;
        var clip = (e.Source as StyledElement)?.DataContext as SoundClip;
        var props = e.GetCurrentPoint(SoundList).Properties;

        if (props.IsRightButtonPressed)
        {
            // Right-clicking an unselected sound selects just it, so the context
            // menu acts on a sensible target. A selected one keeps the selection.
            if (clip is not null && !_selection.Contains(clip))
            {
                SelectOnly(clip);
            }
        }
        else if (props.IsLeftButtonPressed && clip is null)
        {
            ClearSelection(); // clicked empty space in the grid
        }
    }

    /// <summary>Targets for a context action: the whole selection if the clicked clip is part of it.</summary>
    private List<SoundClip> ActionTargets(SoundClip clicked) =>
        _selection.Contains(clicked) && _selection.Count > 1
            ? _selection.ToList()
            : new List<SoundClip> { clicked };

    private void SelectOnly(SoundClip clip)
    {
        ClearSelection();
        _selection.Add(clip);
        clip.IsSelected = true;
        _selectionAnchor = clip;
    }

    private void ToggleSelect(SoundClip clip)
    {
        if (_selection.Remove(clip))
        {
            clip.IsSelected = false;
        }
        else
        {
            _selection.Add(clip);
            clip.IsSelected = true;
        }

        _selectionAnchor = clip;
    }

    private void RangeSelect(SoundClip clip)
    {
        if (_selectionAnchor is null || !_visibleSounds.Contains(_selectionAnchor))
        {
            SelectOnly(clip);
            return;
        }

        int a = _visibleSounds.IndexOf(_selectionAnchor);
        int b = _visibleSounds.IndexOf(clip);
        if (a < 0 || b < 0)
        {
            SelectOnly(clip);
            return;
        }

        if (a > b)
        {
            (a, b) = (b, a);
        }

        foreach (var c in _selection)
        {
            c.IsSelected = false;
        }

        _selection.Clear();
        for (int i = a; i <= b; i++)
        {
            _selection.Add(_visibleSounds[i]);
            _visibleSounds[i].IsSelected = true;
        }
        // keep the existing anchor for further range extension
    }

    private void ClearSelection()
    {
        foreach (var clip in _selection)
        {
            clip.IsSelected = false;
        }

        _selection.Clear();
        _selectionAnchor = null;
    }

    private async void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        // Rename is single-target (renaming many to one name makes no sense).
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
        // IsChecked is two-way bound, so clip.IsFavorite is already toggled here;
        // mirror that value onto the rest of the selection.
        if (sender is MenuItem { DataContext: SoundClip clip })
        {
            foreach (var target in ActionTargets(clip))
            {
                target.IsFavorite = clip.IsFavorite;
            }

            ReorderFavorites();
            RefreshVisibleSounds();
            SaveConfig();
        }
    }

    private void OnLoopClick(object? sender, RoutedEventArgs e)
    {
        // IsChecked is two-way bound, so clip.IsLooping is already toggled here.
        if (sender is MenuItem { DataContext: SoundClip clip })
        {
            foreach (var target in ActionTargets(clip))
            {
                target.IsLooping = clip.IsLooping;
            }

            SaveConfig();
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SoundClip clip })
        {
            foreach (var target in ActionTargets(clip))
            {
                _sounds.Remove(target);
            }

            ClearSelection();
            RegisterAllHotkeys(); // drop hotkeys of removed sounds
            RefreshVisibleSounds();
            SaveConfig();
        }
    }

    // --- Global hotkeys ---

    /// <summary>Re-registers every sound's hotkey (works regardless of the active category).</summary>
    private void RegisterAllHotkeys()
    {
        if (_hotkeys is null)
        {
            return;
        }

        _hotkeys.UnregisterAll();
        foreach (var clip in _sounds)
        {
            if (string.IsNullOrEmpty(clip.Hotkey))
            {
                continue;
            }

            KeyGesture gesture;
            try
            {
                gesture = KeyGesture.Parse(clip.Hotkey);
            }
            catch
            {
                Log.Write($"Unparseable hotkey '{clip.Hotkey}' on '{clip.Name}'");
                continue;
            }

            var captured = clip;
            if (!_hotkeys.TryRegister(gesture, () => PlayClip(captured)))
            {
                Log.Write($"Hotkey register failed for '{clip.Name}' ({clip.Hotkey}) — in use?");
            }
        }
    }

    private async void OnSetHotkey(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: SoundClip clip })
        {
            return;
        }

        var result = await new HotkeyDialog(clip.Hotkey).ShowDialog<string?>(this);
        if (result is null) // cancelled
        {
            return;
        }

        if (!string.IsNullOrEmpty(result))
        {
            // A gesture maps to one sound — take it off any other clip.
            foreach (var other in _sounds.Where(s => s != clip && s.Hotkey == result))
            {
                other.Hotkey = "";
            }
        }

        clip.Hotkey = result;
        RegisterAllHotkeys();
        RefreshVisibleSounds();
        SaveConfig();
        StatusText.Text = string.IsNullOrEmpty(result)
            ? $"Cleared hotkey for \"{clip.Name}\"."
            : $"Hotkey {result} → \"{clip.Name}\".";
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
        _config.RouteMic = MicCheck.IsChecked == true;
        var mic = MicDeviceCombo.SelectedItem as OutputDevice;
        _config.MicDeviceId = mic is { IsDefault: false } ? mic.Id : null;
        _config.Save();
    }
}
