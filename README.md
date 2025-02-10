# SPCHR (Speech Transcription Tool)

A Windows desktop application that provides real-time speech-to-text transcription using either Azure Speech Services or OpenAI's Whisper model locally.

## Features

- Real-time speech-to-text transcription
- Automatic fallback to local Whisper model if Azure credentials aren't available
- Global hotkey support (Ctrl+Alt+L) to start/stop recording
- Automatic text pasting to active window
- Support for both cloud (Azure) and local (Whisper) transcription

## Prerequisites

- Windows OS
- .NET 6.0 or later
- Visual Studio 2019 or later (for development)
- Azure Speech Services subscription (optional)

## Installation

1. Clone the repository
2. Build the solution using Visual Studio
3. Configure settings in `appsettings.json` if you want to use Azure Speech Services:
   ```json
   {
     "AzureSpeech": {
       "SubscriptionKey": "your-key-here",
       "Region": "your-region-here"
     }
   }
   ```

## Usage

1. Launch the application
2. Use the global hotkey (Ctrl+Alt+L) to start/stop recording
3. Speak clearly into your microphone
4. Transcribed text will automatically be pasted into the active window

## Technical Details

- Uses EchoSharp library for real-time streaming transcription
- Uses Whisper.net for transcription
- Implements OpenAI's Whisper model locally via Whisper.net
- Uses NAudio for audio capture

## Dependencies

- EchoSharp
- Whisper.net
- Whisper.net.Runtime
- NAudio
- Microsoft.CognitiveServices.Speech

## Development

The application uses a modular architecture with:
- Real-time audio capture and buffering
- Background processing for continuous transcription
- Proper resource management and cleanup
- Error handling and fallback mechanisms

## License

[MIT License](LICENSE)

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change. 