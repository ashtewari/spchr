using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using NAudio.Wave;
using Whisper.net.Ggml;

using System.Globalization;
using EchoSharp.Abstractions.SpeechTranscription;
using EchoSharp.Abstractions.VoiceActivityDetection;
using EchoSharp.NAudio;
using EchoSharp.SpeechTranscription;
using EchoSharp.WebRtc.WebRtcVadSharp;
using EchoSharp.Whisper.net;
using WebRtcVadSharp;

namespace SPCHR
{
    public partial class MainForm : Form
    {
        private bool isListening = false;
        private SpeechRecognizer? recognizer;
        private readonly IConfiguration configuration;
        private PictureBox microphoneIcon;
        private IRealtimeSpeechTranscriptor _transcriptor;
        private WaveInEvent? _waveIn;
        private MicrophoneInputSource _micAudioSource;
        private CancellationTokenSource? _transcriptionCancellation;

        private string _modelPath;
        private GgmlType _modelType = GgmlType.SmallEn;


        private bool useWhisper = false;
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modifiers for hotkeys
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_L = 0x4C;  // L key
        private const int HOTKEY_ID = 1;

        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;

        private bool _modelDownloaded = false;
        private Label _downloadStatusLabel;

        public MainForm()
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            InitializeComponent(); // This needs to happen before we access any controls
            
            // Update form properties
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ControlBox = true;
            this.StartPosition = FormStartPosition.Manual;

            Rectangle workingArea = Screen.GetWorkingArea(this);
            this.Location = new Point(
                workingArea.Right - this.Width - 20,
                workingArea.Top + 20
            );

            InitializeMicrophoneIcon();
            RegisterGlobalHotKey();
            InitializeSpeechRecognizer();
        }

        private async void InitializeSpeechRecognizer()
        {
            try
            {
                var subscriptionKey = configuration["AzureSpeech:SubscriptionKey"];
                var region = configuration["AzureSpeech:Region"];
                
                if (string.IsNullOrEmpty(subscriptionKey) || string.IsNullOrEmpty(region))
                {
                    useWhisper = true;
                    toggleButton.Enabled = false; // Disable until model is downloaded
                    _modelPath = Path.Combine(Application.StartupPath, "models", $"ggml-{_modelType.ToString().ToLower()}.bin");
                    await DownloadWhisperModel(_modelType);
                    await InitializeRealtimeTranscriptor();
                    return;
                }

                var config = SpeechConfig.FromSubscription(subscriptionKey, region);
                var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                recognizer = new SpeechRecognizer(config, audioConfig);

                recognizer.Recognized += Recognizer_Recognized;
            }
            catch (Exception ex)
            {
                useWhisper = true;
                toggleButton.Enabled = false; // Disable until model is downloaded
                _modelPath = Path.Combine(Application.StartupPath, "models", $"ggml-{_modelType.ToString().ToLower()}.bin");
                await DownloadWhisperModel(_modelType);
                await InitializeRealtimeTranscriptor();
                MessageBox.Show($"Falling back to local Whisper model: {ex.Message}");
            }
        }

        private void Recognizer_Recognized(object? sender, SpeechRecognitionEventArgs e)
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                string recognizedText = e.Result.Text;
                if (!string.IsNullOrEmpty(recognizedText))
                {
                    Console.Write(recognizedText);
                    this.Invoke(() => PasteText(recognizedText));
                }
            }
        }

        private void PasteText(string text)
        {
            Clipboard.SetText(text);
            
            // Simulate Ctrl+V
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void RegisterGlobalHotKey()
        {
            try
            {
                if (!RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_L))
                {
                    MessageBox.Show("Could not register hotkey Ctrl+Alt+L. It may be in use by another application.",
                        "Hotkey Registration Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error registering hotkey: {ex.Message}");
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MINIMIZE = 0xF020;

            if (m.Msg == WM_SYSCOMMAND)
            {
                int command = m.WParam.ToInt32() & 0xFFF0;
                if (command == SC_MINIMIZE)
                {
                    return; // Ignore minimize command
                }
            }

            // Hotkey handling remains unchanged.
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                ToggleListening();
            }

            base.WndProc(ref m);
        }

        private async void ToggleListening()
        {
            if (useWhisper && !_modelDownloaded)
            {
                MessageBox.Show("Please wait for the model to finish downloading.", 
                    "Model Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (useWhisper)
            {
                if (!isListening)
                {
                    await InitializeRealtimeTranscriptor();
                    _micAudioSource.StartRecording();
                    toggleButton.Text = "Stop Listening";
                    statusLabel.Text = "Listening (Local)...";
                    SetMicrophoneActive(true);
                }
                else
                {
                    if (_micAudioSource != null)
                    {
                        _micAudioSource.StopRecording();
                        _transcriptionCancellation?.Cancel();
                        _transcriptionCancellation?.Dispose();
                        _transcriptionCancellation = null;
                        _micAudioSource.Dispose();
                        _micAudioSource = null;
                    }
                    toggleButton.Text = "Start Listening";
                    statusLabel.Text = "Not listening";
                    SetMicrophoneActive(false);
                }
            }
            else if (recognizer != null)
            {
                if (!isListening)
                {
                    await recognizer.StartContinuousRecognitionAsync();
                    toggleButton.Text = "Stop Listening";
                    statusLabel.Text = "Listening (Azure)...";
                    SetMicrophoneActive(true);
                }
                else
                {
                    await recognizer.StopContinuousRecognitionAsync();
                    toggleButton.Text = "Start Listening";
                    statusLabel.Text = "Not listening";
                    SetMicrophoneActive(false);
                }
            }

            isListening = !isListening;
        }

        private async void toggleButton_Click(object sender, EventArgs e)
        {
            ToggleListening();
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            // If we're currently listening, stop it first.
            if (isListening)
            {
                e.Cancel = true; // Temporarily cancel closing
                
                if (useWhisper)
                {
                    if (_micAudioSource != null)
                    {
                        _micAudioSource.StopRecording();
                        _transcriptionCancellation?.Cancel();
                        _transcriptionCancellation?.Dispose();
                        _transcriptionCancellation = null;
                        _micAudioSource.Dispose();
                        _micAudioSource = null;
                    }
                }
                else if (recognizer != null)
                {
                    await recognizer.StopContinuousRecognitionAsync();
                }
                isListening = false;
                
                // Now close the form.
                this.Close();
                return;
            }

            // Regular cleanup.
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            recognizer?.Dispose();
            _transcriptor = null;
            _micAudioSource?.Dispose();
            
            base.OnFormClosing(e);
        }

        private void InitializeMicrophoneIcon()
        {
            microphoneIcon = new PictureBox
            {
                Size = new Size(32, 32),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(
                    toggleButton.Location.X - 42,
                    toggleButton.Location.Y + (toggleButton.Height - 32) / 2
                ),
                Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "microphone-inactive.png"))
            };
            
            this.Controls.Add(microphoneIcon);

            _downloadStatusLabel = new Label
            {
                AutoSize = true,
                Location = new Point(
                    toggleButton.Location.X,  // Align with toggle button
                    toggleButton.Location.Y - 25  // Place above the toggle button
                ),
                Text = "Downloading model...",
                Visible = false,
                Padding = new Padding(3), 
            };
            
            // Ensure the label is shown on top of other controls
            _downloadStatusLabel.BringToFront();
            this.Controls.Add(_downloadStatusLabel);
        }

        public void SetMicrophoneActive(bool isActive)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetMicrophoneActive(isActive)));
                return;
            }

            string iconName = isActive ? "microphone-active.png" : "microphone-inactive.png";
            microphoneIcon.Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", iconName));
        }

        private async Task InitializeRealtimeTranscriptor()
        {
            try
            {
                // Set up the VAD detector and speech transcription factories.
                var vadDetectorFactory = GetVadDetector("webrtc"); // Choose VAD detector method.
                var speechTranscriptorFactory = await GetSpeechTranscriptor("whisper.net"); // Choose transcription engine.

                // Create a microphone input source (deviceNumber: 0 uses the default device).
                _micAudioSource = new MicrophoneInputSource(deviceNumber: 0);

                // Get the realtime transcriptor factory from EchoSharp using the factories above.
                var realTimeFactory = await GetRealTimeTranscriptorFactory("echo sharp", speechTranscriptorFactory, vadDetectorFactory);

                // Configure options for realtime transcription.
                var options = new RealtimeSpeechTranscriptorOptions()
                {
                    AutodetectLanguageOnce = false,             // Detect language for each segment
                    IncludeSpeechRecogizingEvents = true,         // Include "recognizing" events in the transcript stream
                    RetrieveTokenDetails = true,                  // Retrieve token details if needed
                    LanguageAutoDetect = false,                   // Do not auto-detect language (use supplied language)
                    Language = new CultureInfo("en-US")           // Use U.S. English for transcription
                };

                // Create the realtime transcriptor.
                _transcriptor = realTimeFactory.Create(options);

                // Start the background transcription loop.
                _transcriptionCancellation = new CancellationTokenSource();
                _ = Task.Run(async () =>
                {
                    await foreach (var segment in _transcriptor.TranscribeAsync(_micAudioSource, _transcriptionCancellation.Token))
                    {
                        string text = string.Empty;
                        if (segment is RealtimeSegmentRecognizing recognizing)
                        {
                            //text = recognizing.Segment.Text;
                        }
                        else if (segment is RealtimeSegmentRecognized recognized)
                        {
                            text = recognized.Segment.Text;
                        }

                        if (!string.IsNullOrWhiteSpace(text) && !text.ToLower().Contains("[blank_audio]") && !text.ToLower().Contains("[silence]"))
                        {
                            // Ensure UI thread invocation.
                            this.Invoke(new Action(() => PasteText(text)));
                        }

                    }
                }, _transcriptionCancellation.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing local transcription engine: {ex.Message}",
                    "Local Transcription Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        IRealtimeSpeechTranscriptorFactory GetEchoSharpTranscriptorFactory(ISpeechTranscriptorFactory speechTranscriptorFactory, IVadDetectorFactory vadDetectorFactory)
        {
            return new EchoSharpRealtimeTranscriptorFactory(speechTranscriptorFactory, vadDetectorFactory, echoSharpOptions: new EchoSharpRealtimeOptions()
            {
                ConcatenateSegmentsToPrompt = false // Flag to concatenate segments to prompt when new segment is recognized (for the whole session)
            });
        }

        private async Task<IRealtimeSpeechTranscriptorFactory> GetRealTimeTranscriptorFactory(string type, ISpeechTranscriptorFactory speechTranscriptorFactory, IVadDetectorFactory vadDetectorFactory)
        {
            return type switch
            {
                "echo sharp" => GetEchoSharpTranscriptorFactory(speechTranscriptorFactory, vadDetectorFactory),
                //"azure" => GetAzureAIRealtimeTranscriptorFactory(),
                _ => throw new NotSupportedException()
            };
        }

        private async Task<ISpeechTranscriptorFactory> GetSpeechTranscriptor(string type)
        {
            return type switch
            {
                "whisper.net" => await GetWhisperTranscriptor(),
                _ => throw new NotSupportedException()
            };
        }

        private async Task<ISpeechTranscriptorFactory> GetWhisperTranscriptor()
        {
            return new WhisperSpeechTranscriptorFactory(_modelPath);
        }

        private async Task DownloadWhisperModel(GgmlType modelType)
        {
            try
            {
                if (toggleButton != null)
                {
                    toggleButton.Enabled = false;
                }
                
                if (_downloadStatusLabel != null)
                {
                    _downloadStatusLabel.Visible = true;
                    _downloadStatusLabel.Text = "Downloading Whisper model...";
                }

                // Download the model if it doesn't exist
                if (!File.Exists(_modelPath))
                {
                    using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(modelType);
                    Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)); // Create models directory if it doesn't exist
                    using var fileStream = File.Create(_modelPath);
                    await modelStream.CopyToAsync(fileStream);
                }

                _modelDownloaded = true;

                if (_downloadStatusLabel != null)
                {
                    _downloadStatusLabel.Text = "Model ready";
                    await Task.Delay(2000); // Show "Model ready" for 2 seconds
                    _downloadStatusLabel.Visible = false;
                }

                if (toggleButton != null)
                {
                    toggleButton.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                if (_downloadStatusLabel != null)
                {
                    _downloadStatusLabel.Text = "Error downloading model";
                }
                
                MessageBox.Show($"Error downloading Whisper model: {ex.Message}", 
                    "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    
                if (toggleButton != null)
                {
                    toggleButton.Enabled = true;
                }
            }
        }

        IVadDetectorFactory GetVadDetector(string vad)
        {
            return vad switch
            {
                "webrtc" => GetWebRtcVadSharpDetector(),
                _ => throw new NotSupportedException()
            };
        }

        IVadDetectorFactory GetWebRtcVadSharpDetector()
        {
            return new WebRtcVadSharpDetectorFactory(new WebRtcVadSharpOptions()
            {
                OperatingMode = OperatingMode.HighQuality, // The operating mode of the VAD. The default is OperatingMode.HighQuality.
            });
        }        

        // Make sure to initialize RealtimeTranscriptor when the form loads.
        private async void MainForm_Load(object sender, EventArgs e)
        {
            await InitializeRealtimeTranscriptor();
        }
    }
}
