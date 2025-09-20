using Microsoft.Extensions.Configuration;
using System.ComponentModel;

namespace SPCHR
{
    public partial class SettingsForm : Form
    {
        private readonly IConfiguration _configuration;
        private readonly MainForm _mainForm;
        private Keys _currentHotkey = Keys.None;
        private string _currentModifiers = "";

        public SettingsForm(IConfiguration configuration, MainForm mainForm)
        {
            _configuration = configuration;
            _mainForm = mainForm;
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load current settings from configuration
            txtOpenAIApiKey.Text = _configuration["OpenAI:ApiKey"] ?? "";
            txtOpenAIModel.Text = _configuration["OpenAI:Model"] ?? "o4-mini";
            txtOpenAIEndpoint.Text = _configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/";
            
            txtAzureSubscriptionKey.Text = _configuration["AzureSpeech:SubscriptionKey"] ?? "";
            txtAzureRegion.Text = _configuration["AzureSpeech:Region"] ?? "";
            
            // Load hotkey settings
            _currentModifiers = _configuration["Hotkey:Modifiers"] ?? "Control,Alt";
            string keyString = _configuration["Hotkey:Key"] ?? "L";
            
            if (Enum.TryParse<Keys>(keyString, out Keys key))
            {
                _currentHotkey = key;
            }
            else
            {
                _currentHotkey = Keys.L;
            }
            
            UpdateHotkeyDisplay();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate hotkey
                if (_currentHotkey == Keys.None)
                {
                    MessageBox.Show("Please set a valid hotkey.", "Invalid Hotkey", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Save settings to appsettings.json
                var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                
                var settings = new
                {
                    OpenAI = new
                    {
                        ApiKey = txtOpenAIApiKey.Text.Trim(),
                        Model = txtOpenAIModel.Text.Trim(),
                        Endpoint = txtOpenAIEndpoint.Text.Trim()
                    },
                    AzureSpeech = new
                    {
                        SubscriptionKey = txtAzureSubscriptionKey.Text.Trim(),
                        Region = txtAzureRegion.Text.Trim()
                    },
                    Hotkey = new
                    {
                        Modifiers = _currentModifiers,
                        Key = _currentHotkey.ToString()
                    }
                };

                string jsonString = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(settingsPath, jsonString);

                // Reload hotkey settings immediately without requiring restart
                _mainForm.ReloadHotkeySettings();

                MessageBox.Show("Settings saved successfully! Hotkey settings have been applied immediately. Other settings may require an application restart.", 
                    "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnToggleApiKey_Click(object sender, EventArgs e)
        {
            // Toggle API key visibility
            txtOpenAIApiKey.UseSystemPasswordChar = !txtOpenAIApiKey.UseSystemPasswordChar;
            
            // Update button text based on current state
            btnToggleApiKey.Text = txtOpenAIApiKey.UseSystemPasswordChar ? "Show" : "Hide";
        }

        private void txtHotkey_KeyDown(object sender, KeyEventArgs e)
        {
            // Prevent default handling
            e.SuppressKeyPress = true;
            e.Handled = true;

            // Ignore modifier keys by themselves
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Alt || 
                e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            {
                return;
            }

            // Build modifier string
            List<string> modifiers = new List<string>();
            if (e.Control) modifiers.Add("Control");
            if (e.Alt) modifiers.Add("Alt");
            if (e.Shift) modifiers.Add("Shift");

            // Require at least one modifier to avoid conflicts with normal typing
            if (modifiers.Count == 0)
            {
                MessageBox.Show("Please use at least one modifier key (Ctrl, Alt, or Shift) with your hotkey to avoid conflicts.",
                    "Invalid Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Validate against common system and application hotkeys
            if (IsSystemHotkey(modifiers, e.KeyCode))
            {
                string conflictType = IsCommonApplicationHotkey(modifiers, e.KeyCode) ? 
                    "This hotkey combination is commonly used by applications (like Ctrl+C for Copy). Using it would break that functionality system-wide." :
                    "This hotkey combination is reserved by the system.";
                    
                MessageBox.Show($"{conflictType} Please choose a different combination like Ctrl+Alt+[Key].",
                    "Hotkey Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _currentModifiers = string.Join(",", modifiers);
            _currentHotkey = e.KeyCode;
            
            UpdateHotkeyDisplay();
        }

        private bool IsSystemHotkey(List<string> modifiers, Keys key)
        {
            // Check for common Windows system hotkeys that should be avoided
            bool hasCtrl = modifiers.Contains("Control");
            bool hasAlt = modifiers.Contains("Alt");
            bool hasShift = modifiers.Contains("Shift");

            // Common system hotkeys to avoid
            if (hasCtrl && hasAlt)
            {
                switch (key)
                {
                    case Keys.Delete: // Ctrl+Alt+Del
                    case Keys.Tab:    // Ctrl+Alt+Tab (sometimes used)
                        return true;
                }
            }

            if (hasAlt)
            {
                switch (key)
                {
                    case Keys.Tab:    // Alt+Tab
                    case Keys.F4:     // Alt+F4
                    case Keys.Space:  // Alt+Space
                    case Keys.Enter:  // Alt+Enter
                        return true;
                }
            }

            if (hasCtrl && !hasAlt && !hasShift)
            {
                // Common Ctrl+Key shortcuts that should never be overridden
                switch (key)
                {
                    case Keys.A:      // Select All
                    case Keys.C:      // Copy
                    case Keys.V:      // Paste
                    case Keys.X:      // Cut
                    case Keys.Z:      // Undo
                    case Keys.Y:      // Redo
                    case Keys.S:      // Save
                    case Keys.O:      // Open
                    case Keys.N:      // New
                    case Keys.P:      // Print
                    case Keys.F:      // Find
                    case Keys.H:      // Replace
                    case Keys.B:      // Bold
                    case Keys.I:      // Italic
                    case Keys.U:      // Underline
                    case Keys.W:      // Close
                    case Keys.T:      // New Tab
                    case Keys.R:      // Refresh
                    case Keys.L:      // Address bar
                    case Keys.D:      // Bookmark/Desktop
                    case Keys.E:      // Explorer
                    case Keys.G:      // Go to
                    case Keys.K:      // Insert link
                    case Keys.Q:      // Quit
                    case Keys.M:      // Minimize
                    case Keys.J:      // Downloads
                    case Keys.Escape: // Ctrl+Esc
                        return true;
                }
            }

            if (hasCtrl)
            {
                // Additional Ctrl combinations that might be problematic
                switch (key)
                {
                    case Keys.Escape: // Ctrl+Esc (system)
                        return true;
                }
            }

            // Function keys that might be problematic
            if (key >= Keys.F1 && key <= Keys.F12)
            {
                // Some F-keys are commonly used by system/applications
                if (hasAlt && (key == Keys.F4)) return true; // Alt+F4 already covered above
                if (hasCtrl && hasShift && key == Keys.Escape) return true;
            }

            return false;
        }

        private bool IsCommonApplicationHotkey(List<string> modifiers, Keys key)
        {
            bool hasCtrl = modifiers.Contains("Control");
            bool hasAlt = modifiers.Contains("Alt");
            bool hasShift = modifiers.Contains("Shift");

            // Check if it's a common Ctrl+Key application shortcut
            if (hasCtrl && !hasAlt && !hasShift)
            {
                switch (key)
                {
                    case Keys.A: case Keys.C: case Keys.V: case Keys.X: case Keys.Z:
                    case Keys.Y: case Keys.S: case Keys.O: case Keys.N: case Keys.P:
                    case Keys.F: case Keys.H: case Keys.B: case Keys.I: case Keys.U:
                    case Keys.W: case Keys.T: case Keys.R: case Keys.L: case Keys.D:
                    case Keys.E: case Keys.G: case Keys.K: case Keys.Q: case Keys.M:
                    case Keys.J:
                        return true;
                }
            }

            return false;
        }

        private void UpdateHotkeyDisplay()
        {
            if (_currentHotkey != Keys.None)
            {
                string displayText = _currentModifiers.Replace(",", " + ") + " + " + _currentHotkey.ToString();
                txtHotkey.Text = displayText;
            }
            else
            {
                txtHotkey.Text = "Click here and press a key combination";
            }
        }
    }
}
