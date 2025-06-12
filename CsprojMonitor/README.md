# Csproj Monitor for Unity

A Unity Editor extension that provides seamless integration with .NET projects using `dotnet watch` command. Monitor, build, and automatically rebuild your external .NET projects directly from Unity Editor.

## Features

- **Real-time Project Monitoring**: Watch multiple .NET projects simultaneously using `dotnet watch`
- **Automatic Build Detection**: Intelligently detects project type (Library/Executable/Web) and uses appropriate commands
- **Unity Integration**: Built-in Unity Editor window with intuitive GUI
- **Auto-start Support**: Automatically start watching projects when Unity loads
- **Comprehensive Logging**: Configurable log levels (Debug, Warning, Error) with smart message categorization
- **Cross-platform**: Works on Windows, macOS, and Linux
- **Project-specific Settings**: Settings are saved per Unity project in ProjectSettings

## Installation

### Method 1: Unity Package Manager (Recommended)

1. Open Unity Package Manager (`Window` > `Package Manager`)
2. Click the `+` button and select `Add package from git URL...`
3. Enter the package URL: `https://github.com/your-repo/csproj-monitor.git`

### Method 2: Manual Installation

1. Download the latest release from [Releases](https://github.com/your-repo/csproj-monitor/releases)
2. Extract the package to your Unity project's `Packages` folder
3. Unity will automatically import the package

## Prerequisites

- Unity 2019.4 or later
- .NET Core SDK 3.1 or later installed on your system
- `dotnet` command available in your system PATH

### Installing .NET SDK

#### Windows
Download and install from [Microsoft .NET Downloads](https://dotnet.microsoft.com/download)

#### macOS
```bash
# Using Homebrew
brew install dotnet

# Using official installer
# Download from https://dotnet.microsoft.com/download
```

#### Linux (Ubuntu/Debian)
```bash
sudo apt update
sudo apt install dotnet-sdk-6.0
```

## Usage

### Opening the Tool

1. In Unity Editor, go to `Tools` > `CodeJunkie` > `Csproj Monitor`
2. The Csproj Monitor window will open

### Adding a Project

1. Click on the `Add New Project` foldout
2. Enter a project name (or it will be auto-filled from the .csproj file)
3. Browse and select your `.csproj` file
4. Click `Add Project`

### Starting/Stopping Monitoring

- **Individual Projects**: Use the `Start Watching` / `Stop Watching` buttons for each project
- **All Projects**: Use the `Start All Watching` / `Stop All Watching` buttons at the bottom

### Building Projects

- **Watch Mode**: Automatically rebuilds when files change
- **Manual Build**: Click `Build Once` for a single build
- **Auto-start**: Enable `Auto Start on Unity Load` to automatically start watching when Unity opens

## Configuration

### Global Settings

- **Enable Auto Start on Unity Load**: Globally enable/disable auto-start functionality
- **Log Level Settings**: Configure which types of messages to display in Unity Console
  - Debug Logs: General information and status messages
  - Warning Logs: Compiler warnings and non-critical issues
  - Error Logs: Compilation errors and critical failures

### Project Settings

Each project can be configured individually:

- **Auto Start on Unity Load**: Enable auto-start for this specific project
- **Project Path**: Path to the .csproj file
- **Watch Command**: Automatically determined based on project type
  - `dotnet watch build` for Library projects
  - `dotnet watch run` for Executable/Web projects

## Project Type Detection

The tool automatically detects your project type and uses the appropriate command:

| Project Type | OutputType | Command Used |
|--------------|------------|--------------|
| Class Library | `<OutputType>Library</OutputType>` | `dotnet watch build` |
| Console Application | `<OutputType>Exe</OutputType>` | `dotnet watch run` |
| Web Application | Uses `Microsoft.NET.Sdk.Web` | `dotnet watch run` |
| Default | No explicit OutputType | `dotnet watch build` |

## Logging and Output

All output from `dotnet watch` is displayed in Unity's Console window with appropriate log levels:

- **Info (White)**: Normal build output, file change notifications
- **Warning (Yellow)**: Compiler warnings, deprecated API usage
- **Error (Red)**: Compilation errors, build failures

Messages are prefixed with the project name: `[ProjectName] Message content`

## Settings Storage

Settings are stored in your Unity project's `ProjectSettings/CsprojMonitorSettings.json` file and are shared with your team through version control.

### Migrating from Previous Versions

The tool automatically migrates settings from Unity's EditorPrefs to the new project-specific format on first use.

## Troubleshooting

### "dotnet command not found"

**Solution**: Ensure .NET SDK is installed and `dotnet` is in your system PATH.

**Check installation**:
```bash
dotnet --version
```

**Common PATH locations**:
- macOS: `/usr/local/share/dotnet`, `/opt/homebrew/bin` (Apple Silicon)
- Linux: `/usr/bin/dotnet`, `/usr/local/bin/dotnet`
- Windows: `C:\Program Files\dotnet`

### Projects not auto-starting

1. Check that `Enable Auto Start on Unity Load` is enabled in Global Settings
2. Verify each project has `Auto Start on Unity Load` enabled
3. Ensure the .csproj file path is valid and accessible

### Build errors not showing

1. Check that `Enable Error Logs` is enabled in Global Settings
2. Verify the project builds successfully outside of Unity using `dotnet build`

### Permission errors on macOS/Linux

```bash
# Make sure dotnet is executable
chmod +x /usr/local/share/dotnet/dotnet

# Or reinstall with proper permissions
sudo chmod -R 755 /usr/local/share/dotnet
```

## Supported Project Types

- .NET Core Class Libraries
- .NET Core Console Applications  
- ASP.NET Core Web Applications
- .NET Standard Libraries
- .NET Framework projects (with .NET Core SDK)

## Limitations

- Requires .NET Core SDK (dotnet CLI) to be installed
- Only monitors .csproj files (not .sln files)
- Real-time watching depends on file system events
- Some antivirus software may interfere with file watching

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Changelog

### Version 1.0.0
- Initial release
- Basic project monitoring with dotnet watch
- Unity Editor integration
- Cross-platform support
- Project-specific settings storage

---

Made by CodeJunkie
