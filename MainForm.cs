using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace VoiceTyper
{
    public partial class MainForm : Form
    {
        private bool isListening = false;
        private SpeechRecognizer? recognizer;
        private readonly IConfiguration configuration;
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;

        public MainForm()
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            InitializeComponent();
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
                    throw new InvalidOperationException("Azure Speech configuration is missing in appsettings.json");
                }

                var config = SpeechConfig.FromSubscription(subscriptionKey, region);
                var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                recognizer = new SpeechRecognizer(config, audioConfig);

                recognizer.Recognized += Recognizer_Recognized;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing speech recognizer: {ex.Message}");
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

        private async void toggleButton_Click(object sender, EventArgs e)
        {
            if (recognizer == null) return;

            if (!isListening)
            {
                await recognizer.StartContinuousRecognitionAsync();
                toggleButton.Text = "Stop Listening";
                statusLabel.Text = "Listening...";
            }
            else
            {
                await recognizer.StopContinuousRecognitionAsync();
                toggleButton.Text = "Start Listening";
                statusLabel.Text = "Not listening";
            }
            
            isListening = !isListening;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (recognizer != null)
            {
                recognizer.Dispose();
            }
            base.OnFormClosing(e);
        }
    }
} 