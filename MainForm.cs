using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using System.Windows.Forms;
using System.Drawing;
using System.IO;

namespace VoiceTyper
{
    public partial class MainForm : Form
    {
        private bool isListening = false;
        private SpeechRecognizer? recognizer;
        private readonly IConfiguration configuration;
        private PictureBox microphoneIcon;
        
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

            // Don't forget to keep the existing hotkey handling
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                ToggleListening();
            }

            base.WndProc(ref m);
        }

        private async void ToggleListening()
        {
            if (recognizer == null) return;
            
            if (!isListening)
            {
                await recognizer.StartContinuousRecognitionAsync();
                toggleButton.Text = "Stop Listening";
                statusLabel.Text = "Listening...";
                SetMicrophoneActive(true);
            }
            else
            {
                await recognizer.StopContinuousRecognitionAsync();
                toggleButton.Text = "Start Listening";
                statusLabel.Text = "Not listening";
                SetMicrophoneActive(false);
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
            if (isListening && recognizer != null)
            {
                e.Cancel = true; // Temporarily cancel closing
                
                // Stop listening
                await recognizer.StopContinuousRecognitionAsync();
                isListening = false;
                
                // Now close the form
                this.Close();
                return;
            }

            // Regular cleanup
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            if (recognizer != null)
            {
                recognizer.Dispose();
            }
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
    }
} 