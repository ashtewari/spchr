using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using Whisper.net;
using Whisper.net.Ggml;
using NAudio.Wave;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SPCHR
{
    public partial class MainForm : Form
    {
        private bool isListening = false;
        private SpeechRecognizer? recognizer;
        private readonly IConfiguration configuration;
        private PictureBox microphoneIcon;
        private WhisperProcessor? _processor;
        private WaveInEvent? _waveIn;
        // Removed: private MemoryStream? _audioStream;
        // Removed: private WaveFileWriter? _writer;

        // For on‐the‐fly Whisper transcription we now accumulate raw PCM bytes:
        // Instead of writing a WAV file header ourselves and then stripping it out,
        // we accumulate raw PCM bytes and then later wrap them with a proper WAV header.
        private readonly List<byte> _rawAudioBuffer = new List<byte>();
        private int _processedBytes = 0;
        private readonly object _bufferLock = new object();
        private CancellationTokenSource? _transcriptionCts;

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

        public MainForm()
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            // Update these form properties
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow; // Makes it a small tool window
            this.TopMost = true; // Keeps it on top of other windows
            this.ShowInTaskbar = false; // Optionally hide from taskbar
            this.MinimizeBox = false; // Disable minimize button
            this.MaximizeBox = false; // Disable maximize button
            this.ControlBox = true; // Show the control box (for close button)
            this.StartPosition = FormStartPosition.Manual; // Allows us to position it manually

            // Position the window in the top-right corner of the screen
            Rectangle workingArea = Screen.GetWorkingArea(this);
            this.Location = new Point(
                workingArea.Right - this.Width - 20,
                workingArea.Top + 20
            );

            InitializeComponent();
            InitializeSpeechRecognizer();
            RegisterGlobalHotKey();
            InitializeMicrophoneIcon();
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
                    await InitializeWhisper();
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
                await InitializeWhisper();
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
            if (useWhisper)
            {
                if (!isListening)
                {
                    StartWhisperRecording();
                    toggleButton.Text = "Stop Listening";
                    statusLabel.Text = "Listening (Whisper)...";
                    SetMicrophoneActive(true);
                }
                else
                {
                    await StopWhisperRecordingAndTranscribe();
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
            // If we're currently listening, stop it first
            if (isListening)
            {
                e.Cancel = true; // Temporarily cancel closing
                
                if (useWhisper)
                {
                    await StopWhisperRecordingAndTranscribe();
                }
                else if (recognizer != null)
                {
                    await recognizer.StopContinuousRecognitionAsync();
                }
                isListening = false;
                
                // Now close the form
                this.Close();
                return;
            }

            // Regular cleanup
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            recognizer?.Dispose();
            _processor?.Dispose();
            _waveIn?.Dispose();
            // Removed disposal of _audioStream and _writer since they are no longer used.
            
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

        private async Task InitializeWhisper()
        {
            try
            {
                var modelPath = Path.Combine(Application.StartupPath, "whisper-model.bin");
                
                if (!File.Exists(modelPath))
                {
                    using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
                    using var fileStream = File.Create(modelPath);
                    await modelStream.CopyToAsync(fileStream);
                }

                // Create the factory and build the processor correctly
                var factory = WhisperFactory.FromPath(modelPath);
                _processor = factory.CreateBuilder()
                                  .WithLanguage("en")  // Set English as the default language
                                  .Build();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Whisper: {ex.Message}", "Whisper Initialization Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Whisper Recording and On-The-Fly Transcription

        /// <summary>
        /// Creates a valid WAV stream (16kHz, 16-bit, mono) from raw PCM data.
        /// </summary>
        private MemoryStream CreateWavStream(byte[] pcmData)
        {
            MemoryStream wavStream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(wavStream, Encoding.ASCII, leaveOpen: true))
            {
                int pcmDataLength = pcmData.Length;
                int headerSize = 44;
                int fileSize = headerSize + pcmDataLength - 8;
                short audioFormat = 1; // PCM
                short numChannels = 1;
                int sampleRate = 16000;
                short bitsPerSample = 16;
                int byteRate = sampleRate * numChannels * bitsPerSample / 8;
                short blockAlign = (short)(numChannels * bitsPerSample / 8);

                // Write the RIFF header.
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(fileSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // Write the fmt chunk.
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // Subchunk1Size for PCM.
                writer.Write(audioFormat);
                writer.Write(numChannels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);

                // Write the data chunk header.
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(pcmDataLength);

                writer.Flush();
            }
            // Append the raw PCM data.
            wavStream.Write(pcmData, 0, pcmData.Length);
            wavStream.Position = 0;
            return wavStream;
        }

        private void StartWhisperRecording()
        {
            if (_waveIn != null) return;

            // Clear any previous data.
            lock (_bufferLock)
            {
                _rawAudioBuffer.Clear();
                _processedBytes = 0;
            }

            // Create a CancellationTokenSource for our background transcription task.
            _transcriptionCts = new CancellationTokenSource();

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, 16-bit, mono
            };

            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.StartRecording();

            // Start background processing of the raw PCM buffer.
            Task.Run(() => ProcessAudioBufferAsync(_transcriptionCts.Token));
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            lock (_bufferLock)
            {
                // Append exactly the recorded bytes.
                _rawAudioBuffer.AddRange(e.Buffer.Take(e.BytesRecorded));
            }
        }

        // This background task periodically checks if enough new audio data has accumulated,
        // and if so, it passes the new data (wrapped in a valid WAV stream) to the Whisper processor.
        private async Task ProcessAudioBufferAsync(CancellationToken ct)
        {
            // Define a minimum chunk size (e.g. about 2 seconds of audio).
            const int MIN_CHUNK_SIZE = 32000; // ~2 seconds of 16kHz, 16-bit, mono audio.

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Wait about one second between checks.
                    await Task.Delay(1000, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                byte[]? chunk = null;
                lock (_bufferLock)
                {
                    int available = _rawAudioBuffer.Count - _processedBytes;
                    if (available >= MIN_CHUNK_SIZE)
                    {
                        // Take all available new bytes.
                        chunk = _rawAudioBuffer.Skip(_processedBytes).Take(available).ToArray();
                        _processedBytes += available;

                        // Optionally: to avoid indefinite growth of the list, remove processed bytes.
                        if (_processedBytes > 64000)
                        {
                            _rawAudioBuffer.RemoveRange(0, _processedBytes);
                            _processedBytes = 0;
                        }
                    }
                }

                if (chunk != null && chunk.Length > 0)
                {
                    try
                    {
                        var transcribedText = new StringBuilder();
                        // Wrap the raw PCM chunk in a valid WAV header.
                        using var wavStream = CreateWavStream(chunk);
                        await foreach (var segment in _processor.ProcessAsync(wavStream))
                        {
                            transcribedText.Append(segment.Text);
                        }
                        if (transcribedText.Length > 0)
                        {
                            // Update the UI on the UI thread.
                            this.Invoke(() => PasteText(transcribedText.ToString()));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during on-the-fly transcription: {ex.Message}");
                    }
                }
            }
        }

        // When stopping recording, we cancel the background task and process any remaining audio.
        private async Task StopWhisperRecordingAndTranscribe()
        {
            if (_waveIn == null) return;

            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;

            if (_transcriptionCts != null)
            {
                _transcriptionCts.Cancel();
                _transcriptionCts.Dispose();
                _transcriptionCts = null;
            }

            // Process any remaining unprocessed audio.
            byte[] remaining;
            lock (_bufferLock)
            {
                int available = _rawAudioBuffer.Count - _processedBytes;
                remaining = available > 0 ? _rawAudioBuffer.Skip(_processedBytes).Take(available).ToArray() : Array.Empty<byte>();
            }
            if (remaining.Length > 0)
            {
                try
                {
                    var transcribedText = new StringBuilder();
                    using var wavStream = CreateWavStream(remaining);
                    await foreach (var segment in _processor.ProcessAsync(wavStream))
                    {
                        transcribedText.Append(segment.Text);
                    }
                    if (transcribedText.Length > 0)
                    {
                        this.Invoke(() => PasteText(transcribedText.ToString()));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error transcribing final audio: {ex.Message}");
                }
            }

            // Clear the buffer.
            lock (_bufferLock)
            {
                _rawAudioBuffer.Clear();
                _processedBytes = 0;
            }
        }

        #endregion

        // These buttons (if you choose to add them) can still start/stop recording manually.
        private async void btnStartRecording_Click(object sender, EventArgs e)
        {
            StartWhisperRecording();
        }

        private async void btnStopRecording_Click(object sender, EventArgs e)
        {
            await StopWhisperRecordingAndTranscribe();
        }

        // Make sure to initialize Whisper when the form loads.
        private async void MainForm_Load(object sender, EventArgs e)
        {
            await InitializeWhisper();
        }
    }
}
