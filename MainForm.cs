using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Text;
using Newtonsoft.Json;
using SPCHR.Services;

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
        private GgmlType _modelType = GgmlType.TinyEn;

        // OpenAI and Semantic Kernel
        private IOpenAIVisionService _openAIService;
        private bool _useOpenAiVision = true;

        private bool useWhisper = false;
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        // Add PrintWindow API import near other DLL imports
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        // Constants for PrintWindow
        private const uint PW_CLIENTONLY = 0x00000001;
        private const uint PW_RENDERFULLCONTENT = 0x00000002;

        // RECT structure
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left, top, right, bottom;
        }

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        // Additional APIs for direct text insertion
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        // Additional method for UI Automation approach
        [DllImport("oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint dwObjectID, ref Guid riid, out IntPtr ppvObject);

        // Constants for UI Automation
        private const uint OBJID_CARET = 0xFFFFFFF8;

        // Constants for direct text insertion
        private const uint EM_REPLACESEL = 0x00C2;
        private const uint WM_CHAR = 0x0102;
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        // INPUT structure for SendInput
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int SRCCOPY = 0x00CC0020; // BitBlt raster operation code

        private const uint GA_ROOT = 2; // Retrieves the top-level window

        // Modifiers for hotkeys
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const int HOTKEY_ID = 1;
        
        // Dynamic hotkey settings
        private uint _hotkeyModifiers = MOD_CONTROL | MOD_ALT;
        private uint _hotkeyVirtualKey = 0x4C; // Default to L key

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;

        private bool _modelDownloaded = false;
        private Label _downloadStatusLabel;

        private CheckBox openAICheckBox;

        private string _screenshotPath;

        // Semaphore to prevent concurrent text insertion operations
        private static readonly SemaphoreSlim _textInsertionSemaphore = new SemaphoreSlim(1, 1);

        // Debug logging
        private static readonly string _debugLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"debug_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        private static readonly object _logLock = new object();

        public MainForm()
        {
            // Initialize debug logging
            ClearDebugLog();
            WriteDebugLog("=== SPCHR Application Started ===");
            WriteDebugLog($"Debug log location: {_debugLogPath}");
            
            configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            // Initialize OpenAI settings
            _openAIService = new OpenAIVisionService(configuration["OpenAI:ApiKey"], configuration["OpenAI:Model"] ?? "o4-mini", configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/");
            
            if (string.IsNullOrEmpty(_openAIService.ApiKey))
            {
                _useOpenAiVision = false; // Disable OpenAI if no API key
            }

            InitializeComponent(); // This needs to happen before we access any controls
            
            // Update form properties
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.TopMost = true;
            this.ShowInTaskbar = true;
            this.MinimizeBox = true;
            this.MaximizeBox = false;
            this.ControlBox = true;
            this.StartPosition = FormStartPosition.Manual;

            //Rectangle workingArea = Screen.GetWorkingArea(this);
            //this.Location = new Point(
            //    workingArea.Right - this.Width - 20,
            //    workingArea.Top + 20
            //);

            trayIcon = new NotifyIcon()
            {
                Icon = this.Icon,
                Visible = true
            };
            
            // Add context menu to tray icon
            var contextMenu = new ContextMenuStrip();
            var settingsMenuItem = new ToolStripMenuItem("Settings");
            settingsMenuItem.Click += SettingsMenuItem_Click;
            contextMenu.Items.Add(settingsMenuItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            var hotkeyInfoMenuItem = new ToolStripMenuItem("Hotkey: Loading...");
            hotkeyInfoMenuItem.Enabled = false;
            contextMenu.Items.Add(hotkeyInfoMenuItem);
            
            var exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, e) => 
            {
                // Ensure proper cleanup before exiting
                DisposeTrayIcon();
                Application.Exit();
            };
            contextMenu.Items.Add(exitMenuItem);
            trayIcon.ContextMenuStrip = contextMenu;

            InitializeMicrophoneIcon();
            LoadHotkeySettings();
            RegisterGlobalHotKey();
            InitializeSpeechRecognizer();

            // Add OpenAI Checkbox (after InitializeComponent has been called)
            this.openAICheckBox = new CheckBox();
            this.openAICheckBox.AutoSize = true;
            this.openAICheckBox.Location = new Point(80,70);
            this.openAICheckBox.Name = "openAICheckBox";
            this.openAICheckBox.Size = new Size(180, 19);
            this.openAICheckBox.TabIndex = 3;
            this.openAICheckBox.Text = "Enable Vision AI";
            this.openAICheckBox.UseVisualStyleBackColor = true;
            this.openAICheckBox.Checked = _useOpenAiVision;
            this.openAICheckBox.CheckedChanged += new EventHandler(this.openAICheckBox_CheckedChanged);
            this.openAICheckBox.Enabled = !string.IsNullOrEmpty(_openAIService.ApiKey);
            
            // Add the checkbox to controls
            this.Controls.Add(this.openAICheckBox);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide(); // Hide the form when minimized
                trayIcon.Visible = true;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Properly dispose of the tray icon when closing the form
            DisposeTrayIcon();
            base.OnClosing(e);
        }

        private void LogMicrophoneDevice(string context)
        {
            try
            {
                if (WaveInEvent.DeviceCount > 0)
                {
                    var capabilities = WaveInEvent.GetCapabilities(0);
                    WriteDebugLog($"{context} using microphone: {capabilities.ProductName}");
                    Console.WriteLine($"{context} using microphone: {capabilities.ProductName}");
                }
                else
                {
                    WriteDebugLog($"No microphone devices found for {context}");
                    Console.WriteLine($"No microphone devices found for {context}");
                }
            }
            catch (Exception ex)
            {
                WriteDebugLog($"Failed to get microphone name for {context}: {ex.Message}");
            }
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
                
                // Log the microphone device name for Azure Speech Services
                LogMicrophoneDevice("Azure Speech");
                
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
                    this.Invoke(async () =>
                    {
                        await ProcessResults(recognizedText);
                    });
                }
            }
        }

        private async Task PasteText(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    WriteDebugLog("Cannot paste empty text");
                    return;
                }

                // Save current clipboard content
                string originalClipboard = null;
                bool hadClipboardContent = false;
                
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        originalClipboard = Clipboard.GetText();
                        hadClipboardContent = true;
                    }
                }
                catch (Exception ex)
                {
                    WriteDebugLog($"Warning: Could not read current clipboard content: {ex.Message}");
                }

                GetTopLevelParentWindow(); // This now gets and sets focus to the appropriate window
                
                // Set the text to clipboard
                Clipboard.SetText(text);
                
                // Small delay to ensure clipboard is ready
                await Task.Delay(50);
                
                // Simulate Ctrl+V
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                keybd_event(VK_V, 0, (uint)KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, (uint)KEYEVENTF_KEYUP, UIntPtr.Zero);

                // Wait for paste operation to complete before restoring clipboard
                await Task.Delay(100);

                // Restore original clipboard content
                if (hadClipboardContent && originalClipboard != null)
                {
                    try
                    {
                        Clipboard.SetText(originalClipboard);
                    }
                    catch (Exception ex)
                    {
                        WriteDebugLog($"Warning: Could not restore original clipboard content: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDebugLog($"Error pasting text: {ex.Message}");
            }
        }

        /// <summary>
        /// Alternative text insertion method that bypasses clipboard and directly inserts text
        /// at the current cursor position using Windows API
        /// </summary>
        private async Task InsertTextDirect(string text)
        {
            // Prevent concurrent text insertion operations
            if (!await _textInsertionSemaphore.WaitAsync(100)) // 100ms timeout
            {
                WriteDebugLog("Text insertion already in progress - skipping duplicate request");
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    WriteDebugLog("Cannot insert empty text");
                    return;
                }

                // Get the foreground window (where the cursor is)
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    WriteDebugLog("No foreground window found");
                    return;
                }

                // Get window title for debugging
                var windowTitle = new System.Text.StringBuilder(256);
                GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);
                WriteDebugLog($"Target window: '{windowTitle}' (Handle: {foregroundWindow})");
                WriteDebugLog($"Text to insert: '{text}' (Length: {text.Length})");
                
                // Check if running as administrator (affects SendInput)
                bool isAdmin = IsRunningAsAdministrator();
                WriteDebugLog($"Running as administrator: {isAdmin}");
                
                if (!isAdmin)
                {
                    WriteDebugLog("WARNING: Not running as administrator - SendInput may fail due to UIPI (User Interface Privilege Isolation)");
                    WriteDebugLog("SUGGESTION: For better compatibility, right-click the executable and select 'Run as administrator'");
                }

                // Try multiple methods in order of preference
                bool success = false;

                // Special handling for applications that use custom controls
                string windowTitleStr = windowTitle.ToString();
                
                WriteDebugLog($"Detected {windowTitleStr} - using simulated typing method");
                success = InsertWithSimulatedTyping(text);
                if (success)
                {
                    WriteDebugLog("Text inserted using simulated typing");
                    return;
                }
                WriteDebugLog($"{windowTitleStr} simulated typing failed, trying other methods");
                
                // Fallback to clipboard method as last resort - must be on UI thread
                await Task.Run(() => this.Invoke(async () => await PasteText(text)));
            }
            catch (Exception ex)
            {
                WriteDebugLog($"Error in direct text insertion: {ex.Message}");
                WriteDebugLog($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _textInsertionSemaphore.Release();
            }
        }

        /// <summary>
        /// Write debug message to log file with timestamp
        /// </summary>
        private static void WriteDebugLog(string message)
        {
            try
            {
                lock (_logLock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logEntry = $"[{timestamp}] {message}";
                    
                    // Also write to Debug output for development
                    System.Diagnostics.Debug.WriteLine(logEntry);
                    
                    // Write to file
                    File.AppendAllText(_debugLogPath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // Fallback to Debug output only if file writing fails
                System.Diagnostics.Debug.WriteLine($"Failed to write to debug log: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Original message: {message}");
            }
        }

        /// <summary>
        /// Clear the debug log file
        /// </summary>
        private static void ClearDebugLog()
        {
            try
            {
                lock (_logLock)
                {
                    if (File.Exists(_debugLogPath))
                    {
                        File.Delete(_debugLogPath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear debug log: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if the application is running as administrator
        /// </summary>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Simulated typing method using keybd_event - works without admin privileges
        /// </summary>
        private bool InsertWithSimulatedTyping(string text)
        {
            try
            {
                WriteDebugLog($"  - Using simulated typing for {text.Length} characters");
                
                // Save original clipboard content once at the beginning
                string originalClipboard = null;
                bool hadClipboard = false;
                
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        originalClipboard = Clipboard.GetText();
                        hadClipboard = true;
                        WriteDebugLog($"  - Saved original clipboard content ({originalClipboard?.Length ?? 0} chars)");
                    }
                }
                catch (Exception ex)
                {
                    WriteDebugLog($"  - Warning: Could not save clipboard content: {ex.Message}");
                }
                
                try
                {
                    // Set the entire text to clipboard once
                    Clipboard.SetText(text);
                    WriteDebugLog($"  - Set text to clipboard");
                    Thread.Sleep(100); // Give clipboard time to update
                    
                    // Verify clipboard content was set correctly
                    string clipboardCheck = Clipboard.GetText();
                    if (clipboardCheck != text)
                    {
                        WriteDebugLog($"  - WARNING: Clipboard verification failed. Expected {text.Length} chars, got {clipboardCheck?.Length ?? 0} chars");
                    }
                    
                    // Paste the text once
                    keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_V, 0, (uint)KEYEVENTF_KEYUP, UIntPtr.Zero);
                    keybd_event(VK_CONTROL, 0, (uint)KEYEVENTF_KEYUP, UIntPtr.Zero);
                    
                    WriteDebugLog($"  - Executed paste command");
                    
                    // Wait longer for Word to process the paste operation
                    // Word can be slow, especially with longer text
                    int waitTime = Math.Min(500, text.Length * 2); // 2ms per character, max 500ms
                    Thread.Sleep(waitTime);
                    WriteDebugLog($"  - Waited {waitTime}ms for paste to complete");
                    
                    WriteDebugLog($"  - Simulated typing: {text.Length}/{text.Length} characters processed");
                    return true;
                }
                finally
                {
                    // Add additional delay before restoring clipboard to ensure paste is completely done
                    Thread.Sleep(200);
                    WriteDebugLog($"  - Additional 200ms delay before clipboard restoration");
                    
                    // Restore original clipboard content
                    try
                    {
                        if (hadClipboard && originalClipboard != null)
                        {
                            Clipboard.SetText(originalClipboard);
                            WriteDebugLog($"  - Restored original clipboard content ({originalClipboard.Length} chars)");
                        }
                        else
                        {
                            Clipboard.Clear();
                            WriteDebugLog($"  - Cleared clipboard");
                        }
                        
                        // Verify the restoration worked
                        string finalClipboard = Clipboard.GetText();
                        if (hadClipboard && originalClipboard != null)
                        {
                            if (finalClipboard == originalClipboard)
                            {
                                WriteDebugLog($"  - Clipboard restoration verified successfully");
                            }
                            else
                            {
                                WriteDebugLog($"  - WARNING: Clipboard restoration verification failed");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteDebugLog($"  - Warning: Could not restore clipboard: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDebugLog($"  - Simulated typing method failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Open the debug log file in the default text editor
        /// </summary>
        public void OpenDebugLog()
        {
            try
            {
                if (File.Exists(_debugLogPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _debugLogPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show($"Debug log file not found at: {_debugLogPath}", 
                        "Debug Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open debug log: {ex.Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private nint GetTopLevelParentWindow()
        {
            // First try to get the current foreground window
            IntPtr foregroundWindow = GetForegroundWindow();
            
            // Try to get text from the window for diagnostic purposes
            var windowTitle = new System.Text.StringBuilder(256);
            if (GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity) > 0)
            {
                WriteDebugLog($"Foreground window title: {windowTitle}");
            }
            
            if (foregroundWindow != IntPtr.Zero)
            {
                WriteDebugLog($"Foreground window handle: {foregroundWindow}");
                // Get the process ID of the foreground window
                GetWindowThreadProcessId(foregroundWindow, out int processId);
                WriteDebugLog($"Foreground window process ID: {processId}");
                
                return foregroundWindow;
            }
            
            // Fall back to GetFocus if GetForegroundWindow failed
            IntPtr focusedHandle = GetFocus();
            if (focusedHandle == IntPtr.Zero)
            {
                WriteDebugLog("No control is currently focused.");
                return focusedHandle;
            }

            IntPtr topLevelParent = GetAncestor(focusedHandle, GA_ROOT);  
            if (topLevelParent != IntPtr.Zero)
            {
                WriteDebugLog($"Top-level parent window handle: {topLevelParent}");
            }
            else
            {
                WriteDebugLog("Failed to retrieve top-level parent window.");
            }

            return topLevelParent;
        }

        private void LoadHotkeySettings()
        {
            try
            {
                // Load hotkey settings from configuration
                string modifiersString = configuration["Hotkey:Modifiers"] ?? "Control,Alt";
                string keyString = configuration["Hotkey:Key"] ?? "L";
                
                // Parse modifiers
                _hotkeyModifiers = 0;
                string[] modifiers = modifiersString.Split(',');
                foreach (string modifier in modifiers)
                {
                    switch (modifier.Trim())
                    {
                        case "Control":
                            _hotkeyModifiers |= MOD_CONTROL;
                            break;
                        case "Alt":
                            _hotkeyModifiers |= MOD_ALT;
                            break;
                        case "Shift":
                            _hotkeyModifiers |= MOD_SHIFT;
                            break;
                    }
                }
                
                // Parse key - convert from Keys enum to virtual key code
                if (Enum.TryParse<Keys>(keyString, out Keys key))
                {
                    _hotkeyVirtualKey = (uint)key;
                }
                else
                {
                    _hotkeyVirtualKey = 0x4C; // Default to L key
                }
            }
            catch (Exception ex)
            {
                WriteDebugLog($"Error loading hotkey settings: {ex.Message}");
                // Use defaults if loading fails
                _hotkeyModifiers = MOD_CONTROL | MOD_ALT;
                _hotkeyVirtualKey = 0x4C;
            }
        }

        private void RegisterGlobalHotKey()
        {
            try
            {
                if (!RegisterHotKey(this.Handle, HOTKEY_ID, _hotkeyModifiers, _hotkeyVirtualKey))
                {
                    string hotkeyDescription = GetHotkeyDescription();
                    MessageBox.Show($"Could not register hotkey {hotkeyDescription}. It may be in use by another application.",
                        "Hotkey Registration Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    // Update tray icon context menu to show current hotkey
                    UpdateTrayIconHotkeyDisplay();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error registering hotkey: {ex.Message}");
            }
        }
        
        private void UpdateTrayIconHotkeyDisplay()
        {
            if (trayIcon?.ContextMenuStrip != null)
            {
                // Find the hotkey info menu item (should be index 2: Settings, Separator, Hotkey)
                if (trayIcon.ContextMenuStrip.Items.Count > 2)
                {
                    var hotkeyMenuItem = trayIcon.ContextMenuStrip.Items[2];
                    hotkeyMenuItem.Text = $"Hotkey: {GetHotkeyDescription()}";
                }
            }
        }
        
        private string GetHotkeyDescription()
        {
            List<string> parts = new List<string>();
            if ((_hotkeyModifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((_hotkeyModifiers & MOD_ALT) != 0) parts.Add("Alt");
            if ((_hotkeyModifiers & MOD_SHIFT) != 0) parts.Add("Shift");
            
            if (Enum.IsDefined(typeof(Keys), (int)_hotkeyVirtualKey))
            {
                Keys key = (Keys)_hotkeyVirtualKey;
                parts.Add(key.ToString());
            }
            else
            {
                parts.Add($"Key{_hotkeyVirtualKey:X}");
            }
            
            return string.Join(" + ", parts);
        }

        protected override void WndProc(ref Message m)
        {
            // Hotkey handling
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
                    toolStripStatusLabel1.Text = "Listening (Local)...";
                    SetMicrophoneActive(true);
                    TakeScreenshotOfParentWindow();
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
                    toolStripStatusLabel1.Text = "Not listening";
                    SetMicrophoneActive(false);
                }
            }
            else if (recognizer != null)
            {
                if (!isListening)
                {
                    await recognizer.StartContinuousRecognitionAsync();
                    toggleButton.Text = "Stop Listening";
                    toolStripStatusLabel1.Text = "Listening (Azure)...";
                    SetMicrophoneActive(true);
                    TakeScreenshotOfParentWindow();
                }
                else
                {
                    await recognizer.StopContinuousRecognitionAsync();
                    toggleButton.Text = "Start Listening";
                    toolStripStatusLabel1.Text = "Not listening";
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
            
            // Ensure tray icon is properly disposed
            DisposeTrayIcon();
            
            WriteDebugLog("=== SPCHR Application Closing ===");
            
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

                if(_micAudioSource == null)
                {
                    throw new Exception("Failed to create microphone input source.");
                }

                // Log the microphone device name
                LogMicrophoneDevice("local");

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
                            this.Invoke(new Action(async () =>
                            {
                                await ProcessResults(text);
                            }));
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

        private async Task ProcessResults(string text)
        {
            WriteDebugLog($"=== PROCESSING SPEECH RESULT ===");
            WriteDebugLog($"Recognized text: '{text}' (Length: {text.Length})");
            WriteDebugLog($"Vision AI enabled: {_useOpenAiVision}");
            
            TakeScreenshotOfParentWindow();

            if (_useOpenAiVision && !string.IsNullOrEmpty(_openAIService.ApiKey) && !string.IsNullOrEmpty(_screenshotPath) && File.Exists(_screenshotPath))
            {
                try
                {
                    WriteDebugLog($"Processing with OpenAI Vision using screenshot: {_screenshotPath}");
                    toolStripStatusLabel1.Text = "Processing with Vision AI...";
                    Application.DoEvents(); // Update UI

                    string enhancedText = await _openAIService.EnhanceText(text, _screenshotPath);

                    if (!string.IsNullOrEmpty(enhancedText))
                    {
                        WriteDebugLog($"OpenAI enhanced text: '{enhancedText}'");
                        toolStripStatusLabel1.Text = isListening ? (useWhisper ? "Listening (Local)..." : "Listening (Azure)...") : "Not listening";
                        await InsertTextDirect(enhancedText);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    WriteDebugLog($"OpenAI processing failed: {ex.Message}");
                    toolStripStatusLabel1.Text = isListening ? (useWhisper ? "Listening (Local)..." : "Listening (Azure)...") : "Not listening";
                    // Fallback to original text if OpenAI fails
                }
            }
            else
            {
                WriteDebugLog($"Skipping OpenAI processing - Vision AI disabled or no screenshot available");
            }

            // Fallback to original functionality
            WriteDebugLog($"Using original text without OpenAI enhancement");
            await InsertTextDirect(text);
        }

        private void TakeScreenshotOfParentWindow()
        {
            // Only take screenshot if Vision AI is enabled
            if (_useOpenAiVision && !string.IsNullOrEmpty(_openAIService.ApiKey))
            {
                var wHandle = GetTopLevelParentWindow();
                _screenshotPath = CaptureScreenshotByWindowHandle(wHandle);
            }
            else
            {
                _screenshotPath = string.Empty; // Clear any existing screenshot path
            }
        }

        /// <summary>
        /// Centralized method to properly dispose of the system tray icon
        /// </summary>
        private void DisposeTrayIcon()
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }

        private void CleanupOldScreenshots()
        {
            try
            {
                string screenshotsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                if (!Directory.Exists(screenshotsDir))
                    return;
                
                var files = Directory.GetFiles(screenshotsDir, "screenshot_*.png");
                
                // Sort by creation time (oldest first)
                var orderedFiles = files
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.CreationTime)
                    .ToList();
                
                // Keep only the 20 most recent files
                int filesToDelete = orderedFiles.Count - 20;
                if (filesToDelete > 0)
                {
                    for (int i = 0; i < filesToDelete; i++)
                    {
                        try
                        {
                            File.Delete(orderedFiles[i].FullName);
                        }
                        catch (Exception ex)
                        {
                            WriteDebugLog($"Failed to delete old screenshot: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDebugLog($"Error cleaning up screenshots: {ex.Message}");
            }
        }

        private string CaptureScreenshotByWindowHandle(nint topLevelParent)
        {
            try
            {
                if (!GetWindowRect(topLevelParent, out RECT rect))
                {
                    WriteDebugLog("Failed to get window dimensions.");
                    return string.Empty;
                }

                int width = rect.right - rect.left;
                int height = rect.bottom - rect.top;

                if (width <= 0 || height <= 0)
                {
                    WriteDebugLog("Invalid window size.");
                    return string.Empty;
                }

                Bitmap screenshot = null;
                bool captureSuccessful = false;

                // Method 1: Try PrintWindow API first (works best with modern applications)
                try
                {
                    screenshot = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    using (Graphics gfx = Graphics.FromImage(screenshot))
                    {
                        IntPtr hdcBitmap = gfx.GetHdc();
                        // Try with PW_RENDERFULLCONTENT flag first (better for complex apps)
                        if (PrintWindow(topLevelParent, hdcBitmap, PW_RENDERFULLCONTENT))
                        {
                            captureSuccessful = true;
                            WriteDebugLog("Screenshot captured using PrintWindow with PW_RENDERFULLCONTENT");
                        }
                        // Fallback to standard PrintWindow
                        else if (PrintWindow(topLevelParent, hdcBitmap, 0))
                        {
                            captureSuccessful = true;
                            WriteDebugLog("Screenshot captured using PrintWindow (standard)");
                        }
                        gfx.ReleaseHdc(hdcBitmap);
                    }
                }
                catch (Exception ex)
                {
                    WriteDebugLog($"PrintWindow method failed: {ex.Message}");
                    screenshot?.Dispose();
                    screenshot = null;
                }

                // Method 2: Try Graphics.CopyFromScreen if PrintWindow failed
                if (!captureSuccessful)
                {
                    try
                    {
                        screenshot = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        using (Graphics gfx = Graphics.FromImage(screenshot))
                        {
                            gfx.CopyFromScreen(rect.left, rect.top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                            captureSuccessful = true;
                            WriteDebugLog("Screenshot captured using Graphics.CopyFromScreen");
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteDebugLog($"CopyFromScreen method failed: {ex.Message}");
                        screenshot?.Dispose();
                        screenshot = null;
                    }
                }

                // Method 3: Fallback to original BitBlt method
                if (!captureSuccessful)
                {
                    try
                    {
                        screenshot = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        using (Graphics gfx = Graphics.FromImage(screenshot))
                        {
                            IntPtr hdcBitmap = gfx.GetHdc();
                            IntPtr hdcWindow = GetDC(topLevelParent);
                            if (BitBlt(hdcBitmap, 0, 0, width, height, hdcWindow, 0, 0, SRCCOPY))
                            {
                                captureSuccessful = true;
                                WriteDebugLog("Screenshot captured using BitBlt (fallback)");
                            }
                            ReleaseDC(topLevelParent, hdcWindow);
                            gfx.ReleaseHdc(hdcBitmap);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteDebugLog($"BitBlt method failed: {ex.Message}");
                        screenshot?.Dispose();
                        return string.Empty;
                    }
                }

                if (!captureSuccessful || screenshot == null)
                {
                    WriteDebugLog("All screenshot capture methods failed.");
                    return string.Empty;
                }

                // Check if the screenshot is completely black (common issue with some methods)
                if (IsImageBlank(screenshot))
                {
                    WriteDebugLog("Screenshot appears to be blank, attempting alternative capture...");
                    screenshot.Dispose();
                    
                    // Try CopyFromScreen as last resort for blank images
                    try
                    {
                        screenshot = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        using (Graphics gfx = Graphics.FromImage(screenshot))
                        {
                            gfx.CopyFromScreen(rect.left, rect.top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteDebugLog($"Final fallback method failed: {ex.Message}");
                        screenshot?.Dispose();
                        return string.Empty;
                    }
                }

                // Create screenshots directory if it doesn't exist
                string screenshotsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                Directory.CreateDirectory(screenshotsDir);

                // Save the image with a timestamp
                string filePath = Path.Combine(screenshotsDir, $"screenshot_{DateTime.Now.Ticks}.png");
                screenshot.Save(filePath, ImageFormat.Png);
                screenshot.Dispose();
                
                // Clean up old screenshots to prevent disk space issues
                CleanupOldScreenshots();
                
                return filePath;
            }
            catch (Exception ex)
            {
                WriteDebugLog($"Error capturing screenshot: {ex.Message}");
                return string.Empty;
            }
        }

        // Helper method to check if an image is blank/black
        private bool IsImageBlank(Bitmap bitmap)
        {
            try
            {
                // Sample a few pixels to check if the image is mostly black/blank
                int sampleCount = 0;
                int blackPixelCount = 0;
                int step = Math.Max(1, bitmap.Width / 10); // Sample every 10th pixel

                for (int x = 0; x < bitmap.Width; x += step)
                {
                    for (int y = 0; y < bitmap.Height; y += step)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        sampleCount++;
                        
                        // Consider a pixel "black" if all RGB components are very low
                        if (pixel.R < 10 && pixel.G < 10 && pixel.B < 10)
                        {
                            blackPixelCount++;
                        }
                    }
                }

                // If more than 90% of sampled pixels are black, consider the image blank
                return sampleCount > 0 && (blackPixelCount / (double)sampleCount) > 0.9;
            }
            catch
            {
                return false; // If we can't check, assume it's not blank
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

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            }
        }

        private void openAICheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _useOpenAiVision = openAICheckBox.Checked;
            
            if (_useOpenAiVision && string.IsNullOrEmpty(_openAIService.ApiKey))
            {
                MessageBox.Show("Please provide an OpenAI API key in the appsettings.json file.", 
                    "OpenAI Configuration", MessageBoxButtons.OK, MessageBoxIcon.Information);
                openAICheckBox.Checked = false;
                _useOpenAiVision = false;
            }
        }

        private void SettingsMenuItem_Click(object sender, EventArgs e)
        {
            using (var settingsForm = new SettingsForm(configuration, this))
            {
                settingsForm.ShowDialog();
            }
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            using (var settingsForm = new SettingsForm(configuration, this))
            {
                settingsForm.ShowDialog();
            }
        }
        
        public void ReloadHotkeySettings()
        {
            // Unregister the current hotkey
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            
            // Reload configuration (create new instance to pick up file changes)
            var newConfiguration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();
                
            // Update the configuration reference
            // Note: This is a limitation - we can't easily update the readonly field
            // So we'll load settings directly from the new config
            string modifiersString = newConfiguration["Hotkey:Modifiers"] ?? "Control,Alt";
            string keyString = newConfiguration["Hotkey:Key"] ?? "L";
            
            // Parse modifiers
            _hotkeyModifiers = 0;
            string[] modifiers = modifiersString.Split(',');
            foreach (string modifier in modifiers)
            {
                switch (modifier.Trim())
                {
                    case "Control":
                        _hotkeyModifiers |= MOD_CONTROL;
                        break;
                    case "Alt":
                        _hotkeyModifiers |= MOD_ALT;
                        break;
                    case "Shift":
                        _hotkeyModifiers |= MOD_SHIFT;
                        break;
                }
            }
            
            // Parse key
            if (Enum.TryParse<Keys>(keyString, out Keys key))
            {
                _hotkeyVirtualKey = (uint)key;
            }
            else
            {
                _hotkeyVirtualKey = 0x4C; // Default to L key
            }
            
            // Register the new hotkey
            RegisterGlobalHotKey();
        }
    }
}
