# SPCHR

SPCHR is a Windows application that enables real-time voice-to-text input using Azure Speech Services. It provides a lightweight, always-on-top interface that allows you to type with your voice in any text input field.

## Features

- Real-time voice-to-text conversion
- Global hotkey support (Ctrl+Alt+L to toggle listening)
- Visual feedback with microphone status icon
- Always-on-top window for easy access
- Works with any text input field

## Prerequisites

- Windows OS
- .NET 6.0 or later
- Azure Speech Services subscription
- Visual Studio 2022 (for development)

## Setup

1. Clone the repository
2. Create an `appsettings.json` file in the project root with your Azure Speech Services credentials:
   ```json
   {
     "AzureSpeech": {
       "SubscriptionKey": "your-subscription-key",
       "Region": "your-region"
     }
   }
   ```
3. Build and run the application

## Usage

1. Launch SPCHR
2. Click the "Start Listening" button or press Ctrl+Alt+L to begin voice input
3. Speak clearly into your microphone
4. Your speech will be converted to text and automatically typed at the cursor position
5. Press Ctrl+Alt+L again or click "Stop Listening" to stop voice input

## Development

The project is built using:
- C# / .NET 6.0
- Windows Forms
- Azure Speech Services SDK
- NAudio for audio visualization

## Configuration

You can modify the following settings in the code:
- Hotkey combination (MainForm.cs)
- Window position and appearance
- Audio visualization parameters

## License

[MIT License](LICENSE)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request 