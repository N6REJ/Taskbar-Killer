![GitHub release (latest by date)](https://img.shields.io/github/v/release/N6REJ/taskbar-killer)
![GitHub top language](https://img.shields.io/github/languages/top/N6REJ/taskbar-killer)
![GitHub All Releases](https://img.shields.io/github/downloads/N6REJ/taskbar-killer/total)
![License: GPL v3](https://img.shields.io/badge/License-MIT-blue.svg)

# Taskbar Killer

A comprehensive Windows utility that automatically manages taskbar auto-hide functionality and resolves dual monitor taskbar conflicts, particularly useful for maintaining taskbar settings after system resume, session unlock events, and display configuration changes.

## Features

- **Automatic Taskbar Management**: Automatically re-enables taskbar auto-hide after Windows resume or unlock events
- **Dual Monitor Support**: Handles taskbar conflicts when switching between TV and monitor modes on dual monitor setups
- **Taskbar Conflict Resolution**: Automatically detects and resolves "can't have 2 taskbars" popup errors
- **Display Change Monitoring**: Monitors display configuration changes and automatically fixes taskbar issues
- **System Tray Integration**: Runs quietly in the system tray with intuitive visual indicators
- **Visual Status Icons**: 
  - `taskbar-up.png` - Displayed when taskbar auto-hide is disabled (normal state)
  - `taskbar-down.png` - Displayed when taskbar auto-hide is enabled (minimized state)
- **Easy Toggle**: Left-click or double-click the tray icon to toggle taskbar auto-hide
- **Startup Management**: Built-in options to add/remove from Windows startup
- **Lightweight**: Minimal resource usage and system impact

## How It Works

Windows sometimes resets the taskbar auto-hide setting and creates taskbar conflicts after:
- System resume from sleep/hibernation
- User session unlock
- Display configuration changes (especially with dual monitors)
- TV/Monitor mode switching on displays that support both modes
- Monitor disconnect/reconnect events

Taskbar Killer monitors these events and automatically:
- Restores your preferred auto-hide setting
- Closes "can't have 2 taskbars" error popups
- Resets conflicting taskbar registry entries
- Refreshes all taskbar instances across multiple monitors
- Ensures taskbar functionality remains consistent across display changes

## Installation

1. Download the latest release from the [project page](https://hallhome.us/taskbar)
2. Extract the files to your preferred location
3. Run `TaskbarAutoHideOnResume.exe`
4. Right-click the system tray icon and select "Add to Startup" if you want it to start automatically

## Usage

### System Tray Icon
- **Left Click**: Toggle taskbar auto-hide on/off
- **Double Click**: Toggle taskbar auto-hide on/off
- **Right Click**: Access context menu with options

### Context Menu Options
- **Toggle Auto-Hide**: Manually toggle the taskbar auto-hide setting
- **Add to Startup**: Add the application to Windows startup
- **Remove from Startup**: Remove the application from Windows startup
- **Exit**: Close the application

### Visual Indicators
- **Taskbar Up Icon**: Taskbar auto-hide is disabled (taskbar always visible)
- **Taskbar Down Icon**: Taskbar auto-hide is enabled (taskbar hidden until mouse hover)

## Requirements

- Windows 10 or later
- .NET 6.0 Runtime (Windows)

## Building from Source

### Prerequisites
- .NET 6.0 SDK
- Windows 10 or later
- Visual Studio 2022 (recommended for MSI installer)

### Simple Visual Studio Workflow

#### For Development
1. Open `Taskbar killer.sln` in Visual Studio
2. Set configuration to `Debug`
3. Build → Clean Solution, then Build → Rebuild Solution (or F6)

#### For Deployment with MSI Installer
1. Open `Taskbar killer.sln` in Visual Studio
2. Set configuration to `Release`
3. Build → Clean Solution, then Build → Rebuild Solution
4. Right-click `Installer` project → Rebuild
5. MSI installer will be created in: `Installer\Release\Taskbar Killer Installer.msi`

### Build Outputs
- **Debug build**: `bin\Debug\net6.0-windows\` (requires .NET 6 runtime)
- **Release build**: `bin\Release\net6.0-windows\` (lightweight, 294KB executable)
- **MSI Installer**: `Installer\Release\Taskbar Killer Installer.msi`

### Command Line (Alternative)
```bash
# Debug build
dotnet build "Taskbar Killer.csproj" -c Debug

# Release build  
dotnet build "Taskbar Killer.csproj" -c Release
```

### Visual Studio Integration
- **Debug configuration**: Standard build for development and debugging
- **Release configuration**: Lightweight optimized build (requires .NET 6 runtime)
- **MSI Installer**: Automatically packages the Release build
- **IntelliSense**: Full support for debugging and development

## Technical Details

- Built with C# and Windows Forms
- Uses Windows Registry to manage taskbar settings
- Monitors system power and session events
- Lightweight system tray application (294KB executable)
- Requires .NET 6.0 Runtime

## Support

For bug reports, feature requests, or general support, please visit:
[https://github.com/N6REJ/Taskbar-Killer/issues](https://github.com/N6REJ/Taskbar-Killer/issues)

## Project Information

- **Project Website**: [https://hallhome.us/taskbar](https://hallhome.us/taskbar)
- **Support**: [https://github.com/N6REJ/Taskbar-Killer/issues](https://github.com/N6REJ/Taskbar-Killer/issues)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

*Taskbar Killer - Keep your taskbar hidden, automatically.*
