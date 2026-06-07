<div align="center">
  <img src="SimpleSoundboard/Assets/logo.png" width="120" alt="Simple Soundboard logo" />

  # Simple Soundboard

  A lightweight Windows soundboard that routes sounds **and your mic** into any app
  via [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) — built with C# and
  [Avalonia](https://avaloniaui.net/).
</div>

---

## What it does

Play sound clips into a virtual audio cable so other apps (Discord, OBS, games,
voice chat…) receive them as if they came from a microphone — optionally **mixed
with your real microphone** so people hear you *and* your sounds.

- 🎵 Plays **WAV, MP3, and OGG** (both Vorbis and Opus — e.g. Discord `.ogg` files)
- 🔀 **Dual output** — each sound plays to a *monitor* device you can hear **and**
  to VB-Cable at the same time
- 🎙️ **Mic passthrough** — pipe your live microphone into VB-Cable alongside the
  sounds, so other apps hear voice + soundboard on one input
- ▶️ **Now Playing** panel — live progress bar, elapsed/total time, and per-sound stop
- ⭐ **Favorites** (sorted to the front) and **rename** for every button
- 📦 **Bundled sounds** — drop files in `Assets/Sounds/` and they ship with the app
- 💾 Remembers your sounds, volume, devices, and routing between runs

## Requirements

- **Windows** (uses WASAPI for audio + device routing)
- [**.NET 9 SDK**](https://dotnet.microsoft.com/download)
- [**VB-Audio Virtual Cable**](https://vb-audio.com/Cable/) installed (free) —
  required to route audio into other apps

## Getting started

```bash
dotnet run --project SimpleSoundboard
```

1. Click **Add sound** and pick some audio files.
2. Pick the **Monitor** device you listen on (so you hear what you send).
3. Make sure **VB-Cable** is checked — the status bar confirms it was detected.
4. In your other app (Discord, OBS, …), set the **microphone/input** to
   **`CABLE Output (VB-Audio Virtual Cable)`**.
5. Click a sound — it plays in your headphones *and* into the other app.

### Hearing your voice too (mic passthrough)

Tick **Mic** in the routing row and choose your microphone. Your voice is then
mixed into VB-Cable with the sounds, so the other app (still listening to
`CABLE Output`) hears both. Leave that app's mic set to `CABLE Output` — not your
real mic.

> **Note:** software passthrough adds ~100 ms of latency to your voice. For
> zero-compromise latency, Windows' "Listen to this device" or Voicemeeter run
> lower at the cost of more setup.

## Usage notes

| Action | How |
| --- | --- |
| Play a sound | Click its button |
| Stop one sound | Click ■ next to it in **Now Playing** |
| Stop everything | **Stop all** |
| Rename | Right-click → **Rename…** |
| Favorite | Right-click → **Favorite** (pins it to the front) |
| Remove | Right-click → **Remove** |

### Bundled / default sounds

Any `.wav`, `.mp3`, or `.ogg` placed in
[`SimpleSoundboard/Assets/Sounds/`](SimpleSoundboard/Assets/Sounds) is copied next
to the app and **added to the board the first time it appears**. Each file is
seeded once — remove it in the app and it won't come back; add new files and they
show up on the next launch.

## Building

```bash
dotnet build SimpleSoundboard            # debug build
dotnet build SimpleSoundboard -c Release # release build
```

Config is stored at `%AppData%\SimpleSoundboard\sounds.json`.

### Replacing the logo

The app icon is generated from a single source image,
[`SimpleSoundboard/Assets/logo.png`](SimpleSoundboard/Assets/logo.png). To rebrand:

```bash
# 1. replace Assets/logo.png with your own square image
# 2. regenerate the multi-resolution .ico
dotnet run --project SimpleSoundboard -- --genicon SimpleSoundboard/Assets/logo.ico
# 3. rebuild so the exe re-embeds it
dotnet build SimpleSoundboard
```

## Tech stack

- **[.NET 9](https://dotnet.microsoft.com/)** / **[Avalonia 11](https://avaloniaui.net/)** — cross-platform UI (the audio layer is Windows-only)
- **[NAudio](https://github.com/naudio/NAudio)** — WASAPI playback, capture, and device enumeration
- **[NAudio.Vorbis](https://github.com/naudio/Vorbis)** — Ogg Vorbis decoding
- **[Concentus](https://github.com/lostromb/concentus)** — Ogg Opus decoding

### Diagnostics

A few headless CLI flags exist for troubleshooting audio:

```bash
dotnet run --project SimpleSoundboard -- --audiotest        # tone + engine self-test
dotnet run --project SimpleSoundboard -- --repro <file>     # decode + play one file
dotnet run --project SimpleSoundboard -- --genicon <path>   # rebuild the .ico from logo.png
```

A runtime log is written to `%AppData%\SimpleSoundboard\log.txt`.
