# Vibing With CtrlEm

A Windows desktop application that watches a [CtrlEm](https://ctrlem.com/) activity log and translates logged events into haptic feedback commands sent to connected devices through [Intiface Central](https://intiface.com/central/).  

In short, whenever you get a command through CtrlEm, your vibrator goes off!

---

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Using the App](#using-the-app)
- [Configuration](#configuration)
  - [Config File Location](#config-file-location)
  - [Config File Format](#config-file-format)
  - [Command Mappings](#command-mappings)
  - [Default Commands](#default-commands)
- [Log File Monitoring](#log-file-monitoring)
- [System Tray](#system-tray)
- [Building from Source](#building-from-source)
- [Privacy](#privacy)
- [License](#license)

---

## Overview

Vibing With CtrlEm tails a live CtrlEm log file and, whenever a configured keyword is detected in a new log entry, fires a timed vibration at a specific intensity on any Buttplug-compatible device managed by [Intiface Central](https://intiface.com/central/).

The application **does not** pair with, scan for, or directly control Bluetooth hardware. All device discovery and pairing is handled by [Intiface Central](https://intiface.com/central/). Vibing With CtrlEm connects to Intiface over a local WebSocket as a standard Buttplug client.

Key behaviours:

- Tails the **most recently modified** `.log` file in the configured folder — so day-rollover log files are picked up automatically.
- Reads new log lines as they appear, starting from the **end of the file** at launch so only new events trigger commands.
- Opens log files in a **non-exclusive, non-locking** mode so CtrlEm can continue writing uninterrupted.
- All processing happens **locally** — no log content or device data is sent to any cloud service.

---

## Prerequisites

| Requirement | Notes |
|---|---|
| **Windows 10/11 x64** | Only supported platform currently |
| **Intiface Central** (or Intiface Engine) | [Download here](https://intiface.com/central/) — must be running before connecting |
| A Buttplug-compatible device | Paired and visible inside Intiface Central |
| CtrlEm configured to make log files | See CtrlEm instructions |

No additional .NET runtime is required — the release binary is fully self-contained.

---

## Getting Started

1. **Download** the latest `VibingWithCtrlEm.exe` from the [Releases](../../releases) page.
2. **Launch Intiface Central** and start the Intiface Server (the default WebSocket address is `ws://127.0.0.1:12345`).
3. **Pair your device** inside Intiface Central so it appears in the device list.
4. **Run `VibingWithCtrlEm.exe`** — no installer is needed.
5. On first run the app creates a default `config.json` in `%LOCALAPPDATA%\VibingWithCtrlEm\`.

---

## Using the App

The main window is divided into three panels:

### Left Panel — Intiface Connection

- Shows the **Intiface WebSocket URL** (editable; saved automatically to config).
- Displays the current **connection status**.
- **Connect / Disconnect** button to establish or drop the Buttplug session.

### Middle Panel — Log File

- Shows the **log folder path** (browseable via the folder picker; saved to config).
- Displays the **active log file** currently being tailed.

### Right Panel — Commands

- A scrollable list of all configured **keyword - intensity / duration** mappings.
- Reflects exactly what is in `config.json`.

### Bottom Bar

- **Start** — begins watching the log and firing haptic commands on keyword matches.
- **Stop** — immediately stops all vibration and log monitoring.
- Status messages appear here during operation.

---

## Configuration

### Config File Location

The configuration file is stored at:

```
%LOCALAPPDATA%\VibingWithCtrlEm\config.json
```

On a typical Windows installation this resolves to something like:

```
C:\Users\<YourName>\AppData\Local\VibingWithCtrlEm\config.json
```

The file is created automatically on first launch with sensible defaults. Any changes you make in the UI (URL, log path) are written back here automatically. You can also edit the file directly in any text editor while the app is not running.

---

### Config File Format

```json
{
  "IntifaceUrl": "ws://127.0.0.1:12345",
  "LogFolderPath": "C:\\Users\\YourName\\Documents\\CtrlEmClient\\Logs",
  "Commands": [
    {
      "Keyword": "screenshot",
      "Intensity": 0.9,
      "DurationMs": 1250
    }
  ]
}
```

| Field | Type | Description |
|---|---|---|
| `IntifaceUrl` | `string` | WebSocket URL of the running Intiface Central / Intiface Engine server |
| `LogFolderPath` | `string` | Path to the folder containing CtrlEm `.log` files |
| `Commands` | `array` | List of keyword-to-haptic mappings (see below) |

---

### Command Mappings

Each entry in the `Commands` array maps a log keyword to a haptic response:

| Field | Type | Range | Description |
|---|---|---|---|
| `Keyword` | `string` | — | Word to match in a log line (case-insensitive, whole-word match) |
| `Intensity` | `number` | `0.0` – `1.0` | Vibration strength (`0.0` = off, `1.0` = maximum) |
| `DurationMs` | `integer` | milliseconds | How long to run the vibration (`0` = skip the command) |

Keyword matching uses a **whole-word, case-insensitive** search, so `screenshot` will match `[12:34] screenshot taken` but not `screenshots`.

---

### Default Commands

The following mappings ship as defaults and reflect common CtrlEm event types:

| Keyword | Intensity | Duration |
|---|---|---|
| `changeWallpaper` | 0.50 | 750 ms |
| `lockinput` | 0.90 | 1000 ms |
| `openPage` | 0.40 | 500 ms |
| `openshock` | 0.65 | 1000 ms |
| `pishock` | 0.65 | 1000 ms |
| `popupImage` | 0.75 | 750 ms |
| `popupSound` | 0.25 | 500 ms |
| `reactionTest` | 0.85 | 2000 ms |
| `screenBlank` | 0.70 | 1000 ms |
| `screenshot` | 0.90 | 1250 ms |
| `sendMessage` | 0.40 | 500 ms |
| `sendOrDelete` | 0.65 | 800 ms |
| `videoOverlay` | 0.25 | 1500 ms |
| `webcamCapture` | 1.00 | 2500 ms |
| `writeForMe` | 0.50 | 1500 ms |

Edit `config.json` to add, remove, or tune any of these entries to your preference.

---

## Log File Monitoring

By default the app looks for log files in:

```
%USERPROFILE%\Documents\CtrlEmClient\Logs
```

The folder path can be changed via the UI or directly in `config.json`.

- The app always monitors the **most recently modified** `.log` file in the folder.
- When a new log file appears (e.g. at midnight), the app switches to it automatically.
- Monitoring starts from the **current end of the file**, so historical entries are not replayed on startup.
- The log folder is polled every 250 ms.

---

## System Tray

The app can be minimized to the system tray. Right-click the tray icon to **Show** the window or **Exit** the application.  You can also **Start** and **Stop** the log file monitoring from the tray icon.

---
## Building from Source

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or later)
- Windows (WPF requires Windows)

### Clone and build

```bash
git clone https://github.com/<your-username>/VibingWithCtrlEm.git
cd VibingWithCtrlEm
dotnet build VibingWithCtrlEm.slnx
```

### Run in development

```bash
dotnet run --project VibingWithCtrlEm/VibingWithCtrlEm.csproj
```

### Publish a self-contained single-file executable

All publish settings are already declared in the `.csproj`, so the publish command is simply:

```powershell
dotnet publish VibingWithCtrlEm/VibingWithCtrlEm.csproj -c Release
```

The output is a **single `VibingWithCtrlEm.exe`** (~165 MB — the full .NET runtime is bundled in)
placed in:

```
VibingWithCtrlEm/bin/Release/net10.0-windows/win-x64/publish/
```

### Dependencies

| Package | Version |
|---|---|
| [Buttplug](https://www.nuget.org/packages/Buttplug) | 4.0.0 |
| [Buttplug.Client.Connectors.WebsocketConnector](https://www.nuget.org/packages/Buttplug.Client.Connectors.WebsocketConnector) | 3.0.1 |

---

## Privacy

Vibing With CtrlEm operates entirely on your local machine:

- **No log content is transmitted** outside your PC.
- **No device telemetry** is collected or sent.
- The only network traffic is the local WebSocket connection to Intiface Central.

---
## Contact

Questions, feedback, or just want to chat? Find me on Discord: **TwistedPanties**

---

## License

Licensed under the **MIT License with Commons Clause**. See [LICENSE](LICENSE) for the full text.

**You are free to:**
- Use, copy, modify, and distribute the source code and binaries for any purpose.
- Build personal or non-commercial projects on top of it.

**You may not:**
- Sell this software, or offer a paid product or service whose value derives primarily from it (e.g. a hosted service, a commercial app, paid support contracts centred on this software).

---
> Written with [StackEdit](https://stackedit.io/).
