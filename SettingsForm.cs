using Microsoft.Extensions.Configuration;
using System.ComponentModel;

namespace SPCHR
{
    public partial class SettingsForm : Form
    {
        private readonly IConfiguration _configuration;
        private readonly MainForm _mainForm;

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
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
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
                    }
                };

                string jsonString = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(settingsPath, jsonString);

                MessageBox.Show("Settings saved successfully! Please restart the application for changes to take effect.", 
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
    }
}
