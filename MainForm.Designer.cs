namespace SPCHR
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
            settingsButton = new Button();
            statusStrip = new StatusStrip();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            ToolTip toolTip1 = new ToolTip();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // toggleButton
            // 
            toggleButton.Location = new Point(80, 30);
            toggleButton.Name = "toggleButton";
            toggleButton.Size = new Size(140, 30);
            toggleButton.TabIndex = 0;
            toggleButton.Text = "Start Listening";
            toolTip1.SetToolTip(toggleButton, "Click to start/stop speech recognition");
            toggleButton.Click += toggleButton_Click;
            // 
            // settingsButton
            // 
            settingsButton.Location = new Point(230, 30);
            settingsButton.Name = "settingsButton";
            settingsButton.Size = new Size(33, 30);
            settingsButton.TabIndex = 4;
            settingsButton.Text = "...";
            toolTip1.SetToolTip(settingsButton, "Open settings");
            settingsButton.UseVisualStyleBackColor = true;
            settingsButton.Click += settingsButton_Click;
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel1 });
            statusStrip.Location = new Point(0, 119);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(320, 22);
            statusStrip.TabIndex = 2;
            statusStrip.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(0, 17);
            // 
            // MainForm
            // 
            ClientSize = new Size(320, 141);
            Controls.Add(statusStrip);
            Controls.Add(settingsButton);
            Controls.Add(toggleButton);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "MainForm";
            Text = "SPCHR";
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private Button toggleButton;
        private Button settingsButton;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel toolStripStatusLabel1;
    }
} 