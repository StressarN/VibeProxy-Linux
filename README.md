# VibeProxy for Linux

<p align="center">
<a href="https://github.com/StressarN" rel="nofollow"><img alt="StressarN" src="https://img.shields.io/badge/By-StressarN-4b3baf" style="max-width: 100%;"></a>
<a href="https://github.com/StressarN/VibeProxy-Linux/blob/main/LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/License-MIT-28a745" style="max-width: 100%;"></a>
<a href="http://x.com/intent/follow?screen_name=StressarN" rel="nofollow"><img alt="Follow on 𝕏" src="https://img.shields.io/badge/Follow-%F0%9D%95%8F/@StressarN-1c9bf0" style="max-width: 100%;"></a>
<a href="https://github.com/StressarN/VibeProxy-Linux"><img alt="Star this repo" src="https://img.shields.io/github/stars/StressarN/VibeProxy-Linux.svg?style=social&amp;label=Star%20this%20repo&amp;maxAge=60" style="max-width: 100%;"></a></p>
</p>

> [!WARNING]
> **HIGHLY EXPERIMENTAL BUT FUNCTIONAL** - This project is in active development. While core features work, expect rough edges and breaking changes.

**Stop paying twice for AI.** VibeProxy is a native Linux desktop application that lets you use your existing Claude Code, ChatGPT, **Gemini**, and **Qwen** subscriptions with powerful AI coding tools like **[Factory Droids](https://app.factory.ai/r/FM8BJHFQ)** – no separate API keys required.

Built on [CLIProxyAPIPlus](https://github.com/router-for-me/CLIProxyAPIPlus), it handles OAuth authentication, token management, and API routing automatically. One click to authenticate, zero friction to code.

> [!IMPORTANT]
> **Gemini and Qwen Support! 🎉** VibeProxy supports Google's Gemini AI and Qwen AI with full OAuth authentication. Connect your accounts and use Gemini and Qwen with your favorite AI coding tools!

> [!IMPORTANT]
> **Extended Thinking Support! 🧠** VibeProxy supports Claude's extended thinking feature with dynamic budgets (4K, 10K, 32K tokens). Use model names like `claude-sonnet-4-5-20250929-thinking-10000` to enable extended thinking. See the [Factory Setup Guide](FACTORY_SETUP.md#step-3-configure-factory-cli) for details.

<p align="center">
<br>
  <a href="https://www.loom.com/share/5cf54acfc55049afba725ab443dd3777"><img src="vibeproxy-factory-video.webp" width="600" height="380" alt="VibeProxy Screenshot" border="0"></a>
</p>

> [!TIP]
> Check out our [Factory Setup Guide](FACTORY_SETUP.md) for step-by-step instructions on how to use VibeProxy with Factory Droids.


## Features

- 🐧 **Native Linux Experience** - Clean Avalonia UI that integrates with your Linux desktop
- 🚀 **One-Click Server Management** - Start/stop the proxy server from the system tray
- 🔐 **OAuth Integration** - Authenticate with Codex, Claude Code, Gemini, and Qwen directly from the app
- 📊 **Real-Time Status** - Live connection status and automatic credential detection
- 🔄 **Auto-Updates** - Monitors auth files and updates UI in real-time
- 🎨 **System Tray Integration** - Convenient access from your system tray
- 💾 **Self-Contained** - Everything bundled in the application (server binary, config, static files)


## Installation

**⚠️ Requirements:** Linux with .NET 8.0 or later

### Download Pre-built Release (Recommended)

1. Go to the [**Releases**](https://github.com/StressarN/VibeProxy-Linux/releases) page
2. Download the latest Linux release package
3. Extract and run the application
4. Launch VibeProxy

### Build from Source

Want to build it yourself? See [**INSTALLATION.md**](INSTALLATION.md) for detailed build instructions.

### Release Automation

- Every pushed commit triggers the GitHub Actions Linux build workflow.
- Branch pushes upload snapshot artifacts named with the version, branch, and commit SHA.
- Pushing a tag like `v0.5.0` publishes a GitHub Release with the packaged ZIP and checksum file.

## Usage

### First Launch

1. Launch VibeProxy - you'll see a system tray icon
2. Click the icon and select "Open Settings"
3. The server will start automatically
4. Click "Connect" for Claude Code, Codex, Gemini, or Qwen to authenticate

### Authentication

When you click "Connect":
1. Your browser opens with the OAuth page
2. Complete the authentication in the browser
3. VibeProxy automatically detects your credentials
4. Status updates to show you're connected

### Server Management

- **Toggle Server**: Click the status (Running/Stopped) to start/stop
- **System Tray Icon**: Shows active/inactive state
- **Launch at Login**: Toggle to start VibeProxy automatically

## Requirements

- Linux (x64 or ARM64)
- .NET 8.0 Runtime or later

## Development

### Project Structure

```
VibeProxy/
├── src/
│   └── VibeProxy.Linux/
│       ├── Program.cs                    # App entry point
│       ├── App.axaml.cs                  # Application lifecycle
│       ├── MainWindow.axaml              # Main UI window
│       ├── MainWindow.axaml.cs           # Main window logic
│       ├── Services/
│       │   ├── CliProxyService.cs        # CLIProxyAPIPlus process control
│       │   ├── AuthStatusService.cs      # Auth file monitoring
│       │   ├── TrayService.cs            # System tray integration
│       │   ├── NotificationService.cs    # Desktop notifications
│       │   ├── AuthCommand.cs            # OAuth authentication
│       │   ├── ThinkingProxyServer.cs    # Extended thinking proxy
│       │   ├── ThinkingModelTransformer.cs # Model name transformer
│       │   └── LaunchAtLoginService.cs   # Auto-start service
│       ├── ViewModels/
│       │   └── SettingsViewModel.cs      # Settings UI logic
│       ├── Models/
│       │   └── AuthStatus.cs             # Auth status model
│       ├── Utilities/
│       │   ├── ObservableObject.cs       # MVVM base class
│       │   ├── RelayCommand.cs           # Command implementation
│       │   ├── AsyncCommand.cs           # Async command implementation
│       │   ├── RingBuffer.cs             # Circular buffer for logs
│       │   └── BooleanNegationConverter.cs # UI converter
│       └── Resources/
│           ├── cli-proxy-api             # CLIProxyAPIPlus binary
│           ├── config.yaml               # CLIProxyAPIPlus config
│           ├── icon-active.png           # Tray icon (active)
│           ├── icon-inactive.png         # Tray icon (inactive)
│           ├── icon-claude.png           # Claude Code service icon
│           ├── icon-codex.png            # Codex service icon
│           ├── icon-gemini.png           # Gemini service icon
│           └── icon-qwen.png             # Qwen service icon
├── tests/
│   └── VibeProxy.Linux.Tests/            # Unit tests
├── VibeProxy.Linux.sln                   # Solution file
├── scripts/
│   └── build-linux.sh                    # Build script
└── Makefile                              # Build automation
```

### Key Components

- **CliProxyService**: Controls the cli-proxy-api server process and OAuth authentication
- **TrayService**: Manages the system tray icon and menu
- **SettingsViewModel**: Avalonia MVVM viewmodel for the main settings UI
- **AuthStatusService**: Monitors `~/.cli-proxy-api/` for authentication files
- **ThinkingProxyServer**: Intercepts requests and adds extended thinking support
- **File Monitoring**: Real-time updates when auth files are added/removed

### Building

```bash
# Build in Debug mode
make build

# Build in Release mode
make release

# Run tests
make test
```

## Credits

VibeProxy is built on top of [CLIProxyAPIPlus](https://github.com/router-for-me/CLIProxyAPIPlus), an excellent unified proxy server for AI services.

Special thanks to:
- The CLIProxyAPIPlus project for providing the core functionality that makes VibeProxy possible
- **[Automaze, Ltd.](https://automaze.io)** for the original VibeProxy concept and implementation - this Linux version builds upon their excellent foundation

## License

MIT License - see LICENSE file for details

## Support

- **Report Issues**: [GitHub Issues](https://github.com/StressarN/VibeProxy-Linux/issues)

---

© 2025 StressarN. All rights reserved.
