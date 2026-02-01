# AGENTS.md - SPCHR Project Development Guide

This guide is for agentic coding agents working on the SPCHR (Speech Transcription Tool) project.

## Project Overview

SPCHR is a Windows desktop application that provides real-time speech-to-text transcription using either Azure Speech Services or OpenAI's Whisper model locally. The application is built with .NET 8 Windows Forms and includes global hotkey support, automatic text pasting, and Vision AI capabilities.

## Build Commands

Since this is a .NET Windows Forms project, use these commands:

### Building the Project
```bash
dotnet build                    # Build the entire solution
dotnet build SPCHR.csproj      # Build the main project only
dotnet build -c Release         # Build in Release configuration
dotnet build -c Debug           # Build in Debug configuration
```

### Running the Application
```bash
dotnet run                     # Build and run the application
dotnet run -c Debug            # Run in Debug configuration
```

### Testing
This project currently does not have a dedicated test project. To add tests:
1. Create a new xUnit test project: `dotnet new xunit -n SPCHR.Tests`
2. Add reference: `dotnet add SPCHR.Tests reference SPCHR.csproj`
3. Run tests: `dotnet test`

### Package Management
```bash
dotnet restore                  # Restore NuGet packages
dotnet clean                   # Clean build artifacts
```

## Code Style Guidelines

### General Principles
- **Framework**: .NET 8 Windows Forms with nullable reference types enabled
- **Language**: C# 12 features where appropriate
- **Architecture**: Modular design with dependency injection for configuration
- **Threading**: Proper async/await patterns with UI thread marshaling via Invoke()

### Import Organization
Imports should be organized in this order:
1. System namespaces (alphabetical)
2. Microsoft namespaces (alphabetical) 
3. Third-party namespaces (alphabetical)
4. Local/namespace imports (alphabetical)

```csharp
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NAudio.Wave;
using Whisper.net.Ggml;
using SPCHR.Services;
```

### Naming Conventions
- **Classes**: PascalCase (e.g., `MainForm`, `OpenAIVisionService`)
- **Methods**: PascalCase (e.g., `InitializeSpeechRecognizer`, `ProcessResults`)
- **Properties**: PascalCase (e.g., `IsListening`, `ApiKey`)
- **Fields**: 
  - Private fields: camelCase with underscore prefix (e.g., `_transcriptor`, `_modelPath`)
  - Private readonly fields: underscore prefix (e.g., `_configuration`)
  - Constants: PascalCase or UPPER_SNAKE_CASE for Win32 constants
- **Variables**: camelCase (e.g., `subscriptionKey`, `region`)
- **Interfaces**: PascalCase with 'I' prefix (e.g., `IOpenAIVisionService`)

### Error Handling
- Use try-catch blocks for external dependencies (Azure, OpenAI, file I/O)
- Log errors using `WriteDebugLog()` method for consistent logging
- Show user-friendly messages via `MessageBox.Show()` for UI-facing errors
- Use `WriteDebugLog()` for troubleshooting and diagnostics
- Always validate external service responses before processing

### Async/Await Patterns
```csharp
// Correct: ConfigureAwait(false) for library code
public async Task<string> SomeMethodAsync()
{
    var result = await SomeExternalServiceAsync().ConfigureAwait(false);
    return result;
}

// For UI updates, use Invoke() to marshal to UI thread
this.Invoke(new Action(async () =>
{
    await ProcessResults(text);
}));
```

### Win32 Interop
- Use P/Invoke for Windows API calls with proper marshaling
- Include `[DllImport]` attributes with exact signatures
- Use constants for magic numbers and flags
- Group related imports together with comments
- Handle Win32 errors gracefully with fallback mechanisms

### Resource Management
- Implement `IDisposable` for classes that own native resources
- Use `using` statements for disposable objects
- Properly cleanup audio resources, transcription services, and Win32 handles
- Call `Dispose()` on forms and services during shutdown

### Configuration Management
- Use `Microsoft.Extensions.Configuration` for settings
- Structure configuration with logical sections (AzureSpeech, OpenAI, Hotkey)
- Provide sensible defaults for missing configuration values
- Support both appsettings.json and environment-specific overrides

### UI Development
- Follow Windows Forms naming conventions for controls
- Use descriptive control names (e.g., `toggleButton`, `toolStripStatusLabel1`)
- Implement proper form lifecycle management (Load, Closing, Dispose)
- Use `InvokeRequired` pattern for cross-thread UI updates
- Support system tray functionality with proper cleanup

### Debugging and Logging
- Use the centralized `WriteDebugLog()` method for consistent logging
- Log important state changes and error conditions
- Include timestamps and contextual information
- Debug logs are written to files with rotation for space management

## Key Dependencies

### Core Libraries
- `Microsoft.CognitiveServices.Speech` - Azure Speech Services
- `Whisper.net` - Local Whisper model support
- `NAudio` - Audio capture and processing
- `EchoSharp` libraries - Real-time transcription pipeline
- `Microsoft.Extensions.Configuration` - Configuration management

### UI Framework
- `UseWindowsForms` - Windows Forms support
- Windows Forms Designer files follow `*.Designer.cs` pattern

## Architecture Notes

### Main Components
1. **MainForm** - Primary UI and orchestration logic
2. **SettingsForm** - Configuration management UI
3. **OpenAIVisionService** - Vision AI integration for text enhancement
4. **Audio Pipeline** - NAudio + EchoSharp + Whisper/Azure integration

### Threading Model
- UI thread: Windows Forms message loop
- Background threads: Audio capture and transcription processing
- Proper synchronization via `Invoke()`, `CancellationTokenSource`, and `SemaphoreSlim`

### Configuration Structure
- Azure Speech Services credentials (optional)
- OpenAI API settings for Vision AI (optional)
- Global hotkey customization
- Model and endpoint configurations

## Development Notes

### Adding New Features
1. Follow existing async patterns and error handling
2. Add appropriate logging using `WriteDebugLog()`
3. Update configuration schema if needed
4. Test both with and without external services
5. Consider fallback mechanisms for service failures

### Modifying Transcription Pipeline
- The pipeline uses EchoSharp with pluggable VAD and transcription engines
- Support for both Azure cloud and local Whisper models
- Real-time processing with configurable language detection

### Debugging Tips
- Check debug log files in application directory for troubleshooting
- Use the "Open Debug Log" functionality (if implemented in settings)
- Verify audio device permissions and availability
- Test with different microphone configurations