# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- Nothing yet

### Changed
- Nothing yet

### Deprecated
- Nothing yet

### Removed
- Nothing yet

### Fixed
- Nothing yet

### Security
- Nothing yet

## [1.0.0] - 2024-12-19

### Added
- Initial release of Csproj Monitor for Unity
- Unity Editor window for managing .NET project monitoring
- Real-time project monitoring using `dotnet watch` command
- Support for multiple project configurations
- Automatic project type detection (Library/Executable/Web)
- Intelligent command selection based on project type
- Cross-platform support (Windows, macOS, Linux)
- Auto-start functionality for projects when Unity loads
- Comprehensive logging system with configurable log levels
- Smart message categorization (Debug, Warning, Error)
- Project-specific settings storage in ProjectSettings
- Settings migration from EditorPrefs to project-specific format
- Build once functionality for manual builds
- Batch operations (Start All/Stop All watching)
- Browse dialog for .csproj file selection
- Environment variable setup for dotnet CLI
- Process management for watch processes
- Unity Console integration for build output
- Settings reset functionality
- Error handling and user feedback dialogs

### Features
- **Project Management**
  - Add/remove projects from monitoring list
  - Individual project configuration (name, path, auto-start)
  - Project status indicators (watching/stopped)
  
- **Build Integration**
  - Automatic rebuilds on file changes
  - Manual build triggers
  - Asset database refresh after successful builds
  
- **Logging System**
  - Configurable log levels (Debug, Warning, Error)
  - Smart message parsing and categorization
  - Project-specific log prefixes
  
- **Cross-platform Support**
  - Windows dotnet CLI detection
  - macOS dotnet CLI detection (Intel and Apple Silicon)
  - Linux dotnet CLI detection
  
- **Settings Management**
  - Project-specific settings storage
  - Global auto-start configuration
  - Settings backup and restore
  - Migration from legacy EditorPrefs

### Technical Details
- Unity 2019.4+ compatibility
- .NET Core SDK 3.1+ requirement
- Editor-only package (no runtime dependencies)
- Async process management for non-blocking operations
- JSON-based settings serialization
- Automatic dotnet CLI path detection
