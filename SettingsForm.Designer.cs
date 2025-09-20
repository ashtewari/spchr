namespace SPCHR
{
    partial class SettingsForm
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
            this.groupBoxOpenAI = new GroupBox();
            this.btnToggleApiKey = new Button();
            this.txtOpenAIEndpoint = new TextBox();
            this.lblOpenAIEndpoint = new Label();
            this.txtOpenAIModel = new TextBox();
            this.lblOpenAIModel = new Label();
            this.txtOpenAIApiKey = new TextBox();
            this.lblOpenAIApiKey = new Label();
            
            this.groupBoxAzure = new GroupBox();
            this.txtAzureRegion = new TextBox();
            this.lblAzureRegion = new Label();
            this.txtAzureSubscriptionKey = new TextBox();
            this.lblAzureSubscriptionKey = new Label();
            
            this.btnSave = new Button();
            this.btnCancel = new Button();
            
            this.groupBoxOpenAI.SuspendLayout();
            this.groupBoxAzure.SuspendLayout();
            this.SuspendLayout();
            
            // 
            // groupBoxOpenAI
            // 
            this.groupBoxOpenAI.Controls.Add(this.btnToggleApiKey);
            this.groupBoxOpenAI.Controls.Add(this.txtOpenAIEndpoint);
            this.groupBoxOpenAI.Controls.Add(this.lblOpenAIEndpoint);
            this.groupBoxOpenAI.Controls.Add(this.txtOpenAIModel);
            this.groupBoxOpenAI.Controls.Add(this.lblOpenAIModel);
            this.groupBoxOpenAI.Controls.Add(this.txtOpenAIApiKey);
            this.groupBoxOpenAI.Controls.Add(this.lblOpenAIApiKey);
            this.groupBoxOpenAI.Location = new Point(12, 12);
            this.groupBoxOpenAI.Name = "groupBoxOpenAI";
            this.groupBoxOpenAI.Size = new Size(460, 140);
            this.groupBoxOpenAI.TabIndex = 0;
            this.groupBoxOpenAI.TabStop = false;
            this.groupBoxOpenAI.Text = "OpenAI Vision Settings";
            
            // 
            // lblOpenAIApiKey
            // 
            this.lblOpenAIApiKey.AutoSize = true;
            this.lblOpenAIApiKey.Location = new Point(6, 25);
            this.lblOpenAIApiKey.Name = "lblOpenAIApiKey";
            this.lblOpenAIApiKey.Size = new Size(57, 15);
            this.lblOpenAIApiKey.TabIndex = 0;
            this.lblOpenAIApiKey.Text = "API Key:";
            
            // 
            // txtOpenAIApiKey
            // 
            this.txtOpenAIApiKey.Location = new Point(100, 22);
            this.txtOpenAIApiKey.Name = "txtOpenAIApiKey";
            this.txtOpenAIApiKey.Size = new Size(280, 23);
            this.txtOpenAIApiKey.TabIndex = 1;
            this.txtOpenAIApiKey.UseSystemPasswordChar = true;
            
            // 
            // lblOpenAIModel
            // 
            this.lblOpenAIModel.AutoSize = true;
            this.lblOpenAIModel.Location = new Point(6, 54);
            this.lblOpenAIModel.Name = "lblOpenAIModel";
            this.lblOpenAIModel.Size = new Size(44, 15);
            this.lblOpenAIModel.TabIndex = 2;
            this.lblOpenAIModel.Text = "Model:";
            
            // 
            // txtOpenAIModel
            // 
            this.txtOpenAIModel.Location = new Point(100, 51);
            this.txtOpenAIModel.Name = "txtOpenAIModel";
            this.txtOpenAIModel.Size = new Size(280, 23);
            this.txtOpenAIModel.TabIndex = 3;
            
            // 
            // lblOpenAIEndpoint
            // 
            this.lblOpenAIEndpoint.AutoSize = true;
            this.lblOpenAIEndpoint.Location = new Point(6, 83);
            this.lblOpenAIEndpoint.Name = "lblOpenAIEndpoint";
            this.lblOpenAIEndpoint.Size = new Size(58, 15);
            this.lblOpenAIEndpoint.TabIndex = 4;
            this.lblOpenAIEndpoint.Text = "Endpoint:";
            
            // 
            // txtOpenAIEndpoint
            // 
            this.txtOpenAIEndpoint.Location = new Point(100, 80);
            this.txtOpenAIEndpoint.Name = "txtOpenAIEndpoint";
            this.txtOpenAIEndpoint.Size = new Size(280, 23);
            this.txtOpenAIEndpoint.TabIndex = 5;
            
            // 
            // btnToggleApiKey
            // 
            this.btnToggleApiKey.Location = new Point(386, 21);
            this.btnToggleApiKey.Name = "btnToggleApiKey";
            this.btnToggleApiKey.Size = new Size(65, 23);
            this.btnToggleApiKey.TabIndex = 6;
            this.btnToggleApiKey.Text = "Show";
            this.btnToggleApiKey.UseVisualStyleBackColor = true;
            this.btnToggleApiKey.Click += new EventHandler(this.btnToggleApiKey_Click);
            
            // 
            // groupBoxAzure
            // 
            this.groupBoxAzure.Controls.Add(this.txtAzureRegion);
            this.groupBoxAzure.Controls.Add(this.lblAzureRegion);
            this.groupBoxAzure.Controls.Add(this.txtAzureSubscriptionKey);
            this.groupBoxAzure.Controls.Add(this.lblAzureSubscriptionKey);
            this.groupBoxAzure.Location = new Point(12, 158);
            this.groupBoxAzure.Name = "groupBoxAzure";
            this.groupBoxAzure.Size = new Size(460, 85);
            this.groupBoxAzure.TabIndex = 1;
            this.groupBoxAzure.TabStop = false;
            this.groupBoxAzure.Text = "Azure Speech Services Settings";
            
            // 
            // lblAzureSubscriptionKey
            // 
            this.lblAzureSubscriptionKey.AutoSize = true;
            this.lblAzureSubscriptionKey.Location = new Point(6, 25);
            this.lblAzureSubscriptionKey.Name = "lblAzureSubscriptionKey";
            this.lblAzureSubscriptionKey.Size = new Size(96, 15);
            this.lblAzureSubscriptionKey.TabIndex = 0;
            this.lblAzureSubscriptionKey.Text = "Subscription Key:";
            
            // 
            // txtAzureSubscriptionKey
            // 
            this.txtAzureSubscriptionKey.Location = new Point(110, 22);
            this.txtAzureSubscriptionKey.Name = "txtAzureSubscriptionKey";
            this.txtAzureSubscriptionKey.Size = new Size(270, 23);
            this.txtAzureSubscriptionKey.TabIndex = 1;
            this.txtAzureSubscriptionKey.UseSystemPasswordChar = true;
            
            // 
            // lblAzureRegion
            // 
            this.lblAzureRegion.AutoSize = true;
            this.lblAzureRegion.Location = new Point(6, 54);
            this.lblAzureRegion.Name = "lblAzureRegion";
            this.lblAzureRegion.Size = new Size(47, 15);
            this.lblAzureRegion.TabIndex = 2;
            this.lblAzureRegion.Text = "Region:";
            
            // 
            // txtAzureRegion
            // 
            this.txtAzureRegion.Location = new Point(110, 51);
            this.txtAzureRegion.Name = "txtAzureRegion";
            this.txtAzureRegion.Size = new Size(270, 23);
            this.txtAzureRegion.TabIndex = 3;
            
            // 
            // btnSave
            // 
            this.btnSave.Location = new Point(316, 260);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new Size(75, 30);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new EventHandler(this.btnSave_Click);
            
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new Point(397, 260);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new Size(75, 30);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);
            
            // 
            // SettingsForm
            // 
            this.ClientSize = new Size(484, 302);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.groupBoxAzure);
            this.Controls.Add(this.groupBoxOpenAI);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "SPCHR Settings";
            
            this.groupBoxOpenAI.ResumeLayout(false);
            this.groupBoxOpenAI.PerformLayout();
            this.groupBoxAzure.ResumeLayout(false);
            this.groupBoxAzure.PerformLayout();
            this.ResumeLayout(false);
        }

        private GroupBox groupBoxOpenAI;
        private Button btnToggleApiKey;
        private TextBox txtOpenAIEndpoint;
        private Label lblOpenAIEndpoint;
        private TextBox txtOpenAIModel;
        private Label lblOpenAIModel;
        private TextBox txtOpenAIApiKey;
        private Label lblOpenAIApiKey;
        
        private GroupBox groupBoxAzure;
        private TextBox txtAzureRegion;
        private Label lblAzureRegion;
        private TextBox txtAzureSubscriptionKey;
        private Label lblAzureSubscriptionKey;
        
        private Button btnSave;
        private Button btnCancel;
    }
}
