namespace VoiceTyper
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            toggleButton = new Button();
            statusLabel = new Label();
            
            // Form settings
            this.Text = "Voice Typer";
            this.Size = new Size(300, 150);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            
            // Toggle button
            toggleButton.Text = "Start Listening";
            toggleButton.Location = new Point(80, 30);
            toggleButton.Size = new Size(140, 30);
            toggleButton.Click += toggleButton_Click;
            
            // Status label
            statusLabel.Text = "Not listening";
            statusLabel.Location = new Point(80, 70);
            statusLabel.Size = new Size(140, 20);
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            
            // Add controls to form
            this.Controls.Add(toggleButton);
            this.Controls.Add(statusLabel);
        }

        private Button toggleButton;
        private Label statusLabel;
    }
} 