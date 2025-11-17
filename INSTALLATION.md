# Installing VibeProxy for Linux

**⚠️ Requirements:** Linux (x64 or ARM64) with .NET 8.0 or later

## Option 1: Download Pre-built Release (Recommended)

### Step 1: Download

1. Go to the [**Releases**](https://github.com/automazeio/vibeproxy/releases) page
2. Download the latest Linux release package (e.g., `vibeproxy-linux-x64.tar.gz`)
3. Extract the archive

### Step 2: Install

```bash
# Extract the archive
tar -xzf vibeproxy-linux-*.tar.gz
cd vibeproxy-linux

# Make the binary executable
chmod +x VibeProxy.Linux

# Optionally, move to a system path (requires sudo)
sudo mv VibeProxy.Linux /usr/local/bin/vibeproxy
```

### Step 3: Launch

Run the application:

```bash
# If installed to system path
vibeproxy

# Or run from extracted directory
./VibeProxy.Linux
```

---

## Option 2: Build from Source

### Prerequisites

- Linux (x64 or ARM64)
- .NET 8.0 SDK or later
- Git

### Install .NET SDK

**Ubuntu/Debian:**
```bash
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

**Fedora:**
```bash
sudo dnf install dotnet-sdk-8.0
```

**Arch Linux:**
```bash
sudo pacman -S dotnet-sdk
```

For other distributions, see: https://learn.microsoft.com/en-us/dotnet/core/install/linux

### Build Instructions

1. **Clone the repository**
   ```bash
   git clone https://github.com/automazeio/vibeproxy.git
   cd vibeproxy
   ```

2. **Build the application**
   ```bash
   # Build in Release mode
   make release
   
   # Or use the build script directly
   bash scripts/build-linux.sh Release
   ```

   This will:
   - Build the .NET application in release mode
   - Create a self-contained executable
   - Bundle CLIProxyAPI and resources
   - Output to `out/publish/`

3. **Run the application**
   ```bash
   cd out/publish
   ./VibeProxy.Linux
   ```

### Build Commands

```bash
# Build in Debug mode
make build

# Build in Release mode
make release

# Run tests
make test

# Clean build artifacts
make clean
```

### Build Options

The build script supports different configurations:

```bash
# Debug build (with symbols and debugging)
bash scripts/build-linux.sh Debug

# Release build (optimized)
bash scripts/build-linux.sh Release
```

---

## Verifying Downloads

Before installing any downloaded package, verify its authenticity:

### 1. Download from Official Source

Only download from the official [GitHub Releases](https://github.com/automazeio/vibeproxy/releases) page.

### 2. Verify Checksum (Optional)

Each release includes SHA-256 checksums:

```bash
# Download the checksum file
curl -LO https://github.com/automazeio/vibeproxy/releases/download/vX.X.X/vibeproxy-linux-x64.tar.gz.sha256

# Verify the download
sha256sum -c vibeproxy-linux-x64.tar.gz.sha256
```

Expected output: `vibeproxy-linux-x64.tar.gz: OK`

### 3. Inspect the Code

All source code is available in this repository - feel free to review before building.

---

## Troubleshooting

### Missing .NET Runtime

If you get "dotnet not found" or similar errors:

```bash
# Verify .NET is installed
dotnet --version

# Install .NET Runtime (if you only need to run, not build)
sudo apt-get install -y dotnet-runtime-8.0  # Ubuntu/Debian
sudo dnf install dotnet-runtime-8.0         # Fedora
```

### Permission Denied

Make sure the binary is executable:

```bash
chmod +x VibeProxy.Linux
```

### Port Already in Use

If port 8317 is already in use:

```bash
# Find the process using the port
sudo lsof -i :8317

# Kill the process (replace PID with actual process ID)
kill -9 <PID>
```

### Build Fails

**Error: .NET SDK not found**
```bash
# Install .NET SDK (see prerequisites section above)
dotnet --version
```

**Error: Permission denied when running build script**
```bash
# Make the build script executable
chmod +x scripts/build-linux.sh
```

### System Tray Icon Not Showing

Some desktop environments require additional packages:

**GNOME:**
```bash
# Install AppIndicator extension
sudo apt-get install gnome-shell-extension-appindicator
```

**KDE Plasma:**
System tray should work out of the box.

**XFCE:**
```bash
sudo apt-get install xfce4-indicator-plugin
```

### Still Having Issues?

- **Check System Requirements**: Linux with .NET 8.0 or later
- **Check Logs**: Run with `--verbose` flag or check system logs
- **Report an Issue**: [GitHub Issues](https://github.com/automazeio/vibeproxy/issues)

---

**Questions?** Open an [issue](https://github.com/automazeio/vibeproxy/issues) or check the [README](README.md).
