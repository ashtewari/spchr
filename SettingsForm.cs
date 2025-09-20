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

            // Validate against common system hotkeys
            if (IsSystemHotkey(modifiers, e.KeyCode))
            {
                MessageBox.Show("This hotkey combination is reserved by the system. Please choose a different combination.",
                    "System Hotkey Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            if (hasCtrl)
            {
                switch (key)
                {
                    case Keys.Escape: // Ctrl+Esc
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
