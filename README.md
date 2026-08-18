<p align="center">
  <img src="StageManager/Assets/stagebar-icon.png" width="128" height="128" alt="StageBar icon">
</p>

<h1 align="center">StageBar</h1>

<p align="center">
  A macOS Stage Manager-inspired live window switcher for Windows 10 and Windows 11.
</p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> · <strong>English</strong>
</p>

<p align="center">
  <a href="https://github.com/renkai9418/stage-bar/actions/workflows/build.yml"><img src="https://github.com/renkai9418/stage-bar/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://github.com/renkai9418/stage-bar/releases"><img src="https://img.shields.io/github/v/release/renkai9418/stage-bar" alt="Release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/renkai9418/stage-bar" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4" alt="Windows 10/11">
</p>

> [!NOTE]
> StageBar is currently an early preview. Version `0.0.3` adds faster event-driven discovery, four persistent pinned applications, MRU ordering, adjustable preview opacity, a transparent sidebar, and lower idle overhead.

## Overview

StageBar places live previews of your open application windows along the left edge of the desktop. It uses the Windows Desktop Window Manager (DWM) thumbnail API, so previews remain live without repeatedly capturing or repainting target windows.

Two display modes are available:

- **Fixed:** reserves a strip of desktop work area through the Windows AppBar API. Maximized applications use the remaining area and do not cover StageBar.
- **Floating:** stays hidden until the pointer reaches the left screen edge, then appears immediately above other windows and automatically hides after the pointer leaves.

## Download

Download the latest installer from [GitHub Releases](https://github.com/renkai9418/stage-bar/releases/latest):

- [StageBar installer for Windows x64](https://github.com/renkai9418/stage-bar/releases/latest/download/StageBar-Setup-0.0.3-x64.exe)

The installer is self-contained; the target computer does not need a separate .NET installation. It installs for the current user and does not require administrator privileges.

> [!WARNING]
> Release `0.0.3` is not code-signed. Windows SmartScreen or Smart App Control may block it. Verify the SHA-256 value shown on the release page before running the installer.

## Features

- Live DWM previews of regular desktop application windows
- Up to four applications pinned at the top; remaining windows follow Windows Z/MRU order
- No destructive badge controls; right-click previews to Pin/Unpin
- Adjustable live-preview opacity with a transparent, shadow-free sidebar
- Incremental thumbnail reuse and no continuous per-frame layout loop
- Stable compositor-based previews without `PrintWindow` repaint loops
- Fixed AppBar mode that reserves desktop workspace
- Instant floating mode with left-edge activation and automatic hiding
- Configurable panel width and visible card count
- Card-count limit calculated from the current monitor height
- Immediate mouse-wheel card navigation
- Immediate window activation when a card is selected
- Application icon badges rendered above DWM thumbnails
- Multi-monitor positioning and per-monitor DPI awareness
- Live settings: changes apply and save immediately
- Global `Ctrl + Alt + Space` panel toggle
- System tray menu and background operation
- Automatic exclusion of desktop, taskbar, hidden, cloaked, and tool windows
- No telemetry and no network communication

## Requirements

- Windows 10 version 2004 (`10.0.19041`) or newer
- Windows 11 is supported
- x64 processor
- Desktop Window Manager enabled

StageBar can preview elevated applications, but Windows may prevent a non-elevated StageBar process from activating a window running as administrator.

## Usage

1. Install and launch StageBar.
2. Open the tray menu and choose **Settings** to select Fixed or Floating mode.
3. In Floating mode, move the pointer to the leftmost 2 pixels of any monitor.
4. Scroll over the cards to browse windows.
5. Click a card to restore and activate that window.

Additional controls:

| Action | Control |
| --- | --- |
| Show or hide the floating panel | `Ctrl + Alt + Space` |
| Open the tray menu | Right-click the StageBar tray icon |
| Toggle the panel | Double-click the tray icon |
| Browse window cards | Mouse wheel |
| Pin or unpin an application | Right-click its preview |

| Remove all remembered pins | Tray menu → **Clear pinned apps** |

Settings are stored at `%LOCALAPPDATA%\StageManager\settings.json`. The legacy directory name is intentionally retained so upgrades preserve settings from earlier builds.

## How it works

| Component | Responsibility |
| --- | --- |
| WPF | Panel, settings interface, layout, and interaction |
| DWM thumbnails | Real-time window previews composed by Windows |
| Transparent icon overlay | Keeps application badges above compositor-owned thumbnails |
| Windows AppBar API | Reserves desktop work area in Fixed mode |
| Win32 window catalog | Discovers, filters, restores, and activates application windows |

## Build from source

Prerequisites:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11

```powershell
git clone git@github.com:renkai9418/stage-bar.git
cd stage-bar
dotnet build .\StageManager\StageManager.csproj -c Release
dotnet run --project .\StageManager\StageManager.csproj
```

### Build the installer

Install [Inno Setup 6](https://jrsoftware.org/isdl.php), then run:

```powershell
.\installer\build-installer.ps1 -Version 0.0.3
```

The script publishes a self-contained single-file `win-x64` executable and creates:

```text
artifacts\installer\StageBar-Setup-0.0.3-x64.exe
```

## Release process

Pushing a version tag triggers the Windows release workflow:

```powershell
git tag v0.0.3
git push origin v0.0.3
```

The workflow builds the installer and attaches it to a GitHub Release. If `release-notes/<version>.md` exists, it is used as the release description.

## Known limitations

- DRM-protected video, some games, and exclusive full-screen applications may not expose usable DWM thumbnails.
- Windows privilege isolation may prevent activation of elevated applications.
- StageBar currently displays individual windows; application grouping and workspaces are not implemented yet.
- The first preview release is unsigned.

## Contributing

Issues and pull requests are welcome. For code changes:

1. Fork the repository and create a focused branch.
2. Keep the DWM preview path compositor-based; avoid capture/repaint loops.
3. Build the project in Release configuration and test both display modes.
4. Describe behavior changes and manual verification in the pull request.

Please use [GitHub Issues](https://github.com/renkai9418/stage-bar/issues) for bug reports and feature proposals.

## Privacy

StageBar processes window handles, titles, icons, and DWM thumbnails locally. It has no analytics, telemetry, account system, or network service.

## License

StageBar is released under the [MIT License](LICENSE).
