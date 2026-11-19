namespace IIGO.Config
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            tblRoot = new TableLayoutPanel();
            grpFile = new GroupBox();
            tblFile = new TableLayoutPanel();
            txtPath = new TextBox();
            btnReload = new Button();
            grpEndpoint = new GroupBox();
            tblEndpoint = new TableLayoutPanel();
            lblHost = new Label();
            cboHost = new ComboBox();
            lblHostNote = new Label();
            lblPort = new Label();
            numPort = new NumericUpDown();
            lblUrl = new Label();
            lblUrlValue = new Label();
            grpCertificate = new GroupBox();
            tblCertificate = new TableLayoutPanel();
            rdoStore = new RadioButton();
            lblStore = new Label();
            cboStore = new ComboBox();
            lblCertificate = new Label();
            cboCertificate = new ComboBox();
            btnRefresh = new Button();
            chkAllowInvalid = new CheckBox();
            rdoFile = new RadioButton();
            lblFile = new Label();
            txtPfxPath = new TextBox();
            btnBrowse = new Button();
            lblPassword = new Label();
            txtPassword = new TextBox();
            lblPasswordNote = new Label();
            grpService = new GroupBox();
            tblService = new TableLayoutPanel();
            lblServiceStatus = new Label();
            btnStart = new Button();
            btnStop = new Button();
            btnRestart = new Button();
            tblButtons = new TableLayoutPanel();
            lblMessage = new Label();
            btnSave = new Button();
            btnSaveRestart = new Button();
            btnClose = new Button();
            dlgOpenPfx = new OpenFileDialog();
            tblRoot.SuspendLayout();
            grpFile.SuspendLayout();
            tblFile.SuspendLayout();
            grpEndpoint.SuspendLayout();
            tblEndpoint.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numPort).BeginInit();
            grpCertificate.SuspendLayout();
            tblCertificate.SuspendLayout();
            grpService.SuspendLayout();
            tblService.SuspendLayout();
            tblButtons.SuspendLayout();
            SuspendLayout();
            //
            // tblRoot
            //
            tblRoot.AutoSize = true;
            tblRoot.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tblRoot.ColumnCount = 1;
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblRoot.Controls.Add(grpFile, 0, 0);
            tblRoot.Controls.Add(grpEndpoint, 0, 1);
            tblRoot.Controls.Add(grpCertificate, 0, 2);
            tblRoot.Controls.Add(grpService, 0, 3);
            tblRoot.Controls.Add(tblButtons, 0, 4);
            tblRoot.Location = new Point(0, 0);
            tblRoot.MinimumSize = new Size(620, 0);
            tblRoot.Name = "tblRoot";
            tblRoot.Padding = new Padding(9);
            tblRoot.RowCount = 5;
            tblRoot.RowStyles.Add(new RowStyle());
            tblRoot.RowStyles.Add(new RowStyle());
            tblRoot.RowStyles.Add(new RowStyle());
            tblRoot.RowStyles.Add(new RowStyle());
            tblRoot.RowStyles.Add(new RowStyle());
            tblRoot.Size = new Size(620, 560);
            tblRoot.TabIndex = 0;
            //
            // grpFile
            //
            grpFile.AutoSize = true;
            grpFile.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grpFile.Controls.Add(tblFile);
            grpFile.Dock = DockStyle.Fill;
            grpFile.Name = "grpFile";
            grpFile.Padding = new Padding(6);
            grpFile.TabIndex = 0;
            grpFile.TabStop = false;
            grpFile.Text = "Settings file";
            //
            // tblFile
            //
            tblFile.AutoSize = true;
            tblFile.ColumnCount = 2;
            tblFile.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblFile.ColumnStyles.Add(new ColumnStyle());
            tblFile.Controls.Add(txtPath, 0, 0);
            tblFile.Controls.Add(btnReload, 1, 0);
            tblFile.Dock = DockStyle.Fill;
            tblFile.Name = "tblFile";
            tblFile.RowCount = 1;
            tblFile.RowStyles.Add(new RowStyle());
            tblFile.TabIndex = 0;
            //
            // txtPath
            //
            txtPath.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtPath.Name = "txtPath";
            txtPath.ReadOnly = true;
            txtPath.TabStop = false;
            txtPath.TabIndex = 0;
            //
            // btnReload
            //
            btnReload.AutoSize = true;
            btnReload.Name = "btnReload";
            btnReload.TabIndex = 1;
            btnReload.Text = "&Reload";
            btnReload.UseVisualStyleBackColor = true;
            btnReload.Click += btnReload_Click;
            //
            // grpEndpoint
            //
            grpEndpoint.AutoSize = true;
            grpEndpoint.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grpEndpoint.Controls.Add(tblEndpoint);
            grpEndpoint.Dock = DockStyle.Fill;
            grpEndpoint.Name = "grpEndpoint";
            grpEndpoint.Padding = new Padding(6);
            grpEndpoint.TabIndex = 1;
            grpEndpoint.TabStop = false;
            grpEndpoint.Text = "HTTPS endpoint";
            //
            // tblEndpoint
            //
            tblEndpoint.AutoSize = true;
            tblEndpoint.ColumnCount = 2;
            tblEndpoint.ColumnStyles.Add(new ColumnStyle());
            tblEndpoint.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblEndpoint.Controls.Add(lblHost, 0, 0);
            tblEndpoint.Controls.Add(cboHost, 1, 0);
            tblEndpoint.Controls.Add(lblHostNote, 1, 1);
            tblEndpoint.Controls.Add(lblPort, 0, 2);
            tblEndpoint.Controls.Add(numPort, 1, 2);
            tblEndpoint.Controls.Add(lblUrl, 0, 3);
            tblEndpoint.Controls.Add(lblUrlValue, 1, 3);
            tblEndpoint.Dock = DockStyle.Fill;
            tblEndpoint.Name = "tblEndpoint";
            tblEndpoint.RowCount = 4;
            tblEndpoint.RowStyles.Add(new RowStyle());
            tblEndpoint.RowStyles.Add(new RowStyle());
            tblEndpoint.RowStyles.Add(new RowStyle());
            tblEndpoint.RowStyles.Add(new RowStyle());
            tblEndpoint.TabIndex = 0;
            //
            // lblHost
            //
            lblHost.Anchor = AnchorStyles.Left;
            lblHost.AutoSize = true;
            lblHost.Name = "lblHost";
            lblHost.TabIndex = 0;
            lblHost.Text = "&Address:";
            //
            // cboHost
            //
            cboHost.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboHost.Name = "cboHost";
            cboHost.TabIndex = 1;
            cboHost.TextChanged += Endpoint_Changed;
            //
            // lblHostNote
            //
            lblHostNote.AutoSize = true;
            lblHostNote.ForeColor = SystemColors.GrayText;
            lblHostNote.MaximumSize = new Size(480, 0);
            lblHostNote.Name = "lblHostNote";
            lblHostNote.TabIndex = 2;
            lblHostNote.Text = "* listens on every address. localhost only allows connections from this server.";
            //
            // lblPort
            //
            lblPort.Anchor = AnchorStyles.Left;
            lblPort.AutoSize = true;
            lblPort.Name = "lblPort";
            lblPort.TabIndex = 2;
            lblPort.Text = "&Port:";
            //
            // numPort
            //
            numPort.Anchor = AnchorStyles.Left;
            numPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numPort.Name = "numPort";
            numPort.Size = new Size(90, 23);
            numPort.TabIndex = 3;
            numPort.Value = new decimal(new int[] { 8443, 0, 0, 0 });
            numPort.ValueChanged += Endpoint_Changed;
            //
            // lblUrl
            //
            lblUrl.Anchor = AnchorStyles.Left;
            lblUrl.AutoSize = true;
            lblUrl.Name = "lblUrl";
            lblUrl.TabIndex = 4;
            lblUrl.Text = "URL:";
            //
            // lblUrlValue
            //
            lblUrlValue.Anchor = AnchorStyles.Left;
            lblUrlValue.AutoSize = true;
            lblUrlValue.Font = new Font(Font, FontStyle.Bold);
            lblUrlValue.Name = "lblUrlValue";
            lblUrlValue.TabIndex = 5;
            //
            // grpCertificate
            //
            grpCertificate.AutoSize = true;
            grpCertificate.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grpCertificate.Controls.Add(tblCertificate);
            grpCertificate.Dock = DockStyle.Fill;
            grpCertificate.Name = "grpCertificate";
            grpCertificate.Padding = new Padding(6);
            grpCertificate.TabIndex = 2;
            grpCertificate.TabStop = false;
            grpCertificate.Text = "Certificate";
            //
            // tblCertificate
            //
            tblCertificate.AutoSize = true;
            tblCertificate.ColumnCount = 3;
            tblCertificate.ColumnStyles.Add(new ColumnStyle());
            tblCertificate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblCertificate.ColumnStyles.Add(new ColumnStyle());
            tblCertificate.Controls.Add(rdoStore, 0, 0);
            tblCertificate.Controls.Add(lblStore, 0, 1);
            tblCertificate.Controls.Add(cboStore, 1, 1);
            tblCertificate.Controls.Add(lblCertificate, 0, 2);
            tblCertificate.Controls.Add(cboCertificate, 1, 2);
            tblCertificate.Controls.Add(btnRefresh, 2, 2);
            tblCertificate.Controls.Add(chkAllowInvalid, 1, 3);
            tblCertificate.Controls.Add(rdoFile, 0, 4);
            tblCertificate.Controls.Add(lblFile, 0, 5);
            tblCertificate.Controls.Add(txtPfxPath, 1, 5);
            tblCertificate.Controls.Add(btnBrowse, 2, 5);
            tblCertificate.Controls.Add(lblPassword, 0, 6);
            tblCertificate.Controls.Add(txtPassword, 1, 6);
            tblCertificate.Controls.Add(lblPasswordNote, 1, 7);
            tblCertificate.Dock = DockStyle.Fill;
            tblCertificate.Name = "tblCertificate";
            tblCertificate.RowCount = 8;
            tblCertificate.RowStyles.Add(new RowStyle());
            tblCertificate.RowStyles.Add(new RowStyle());
            tblCertificate.RowStyles.Add(new RowStyle());
            tblCertificate.RowStyles.Add(new RowStyle());
            tblCertificate.RowStyles.Add(new RowStyle());
            tblCertificate.RowStyles.Add(new RowStyle());
            tblCertificate.RowStyles.Add(new RowStyle());
            tblCertificate.RowStyles.Add(new RowStyle());
            tblCertificate.TabIndex = 0;
            //
            // rdoStore
            //
            rdoStore.AutoSize = true;
            rdoStore.Checked = true;
            tblCertificate.SetColumnSpan(rdoStore, 3);
            rdoStore.Name = "rdoStore";
            rdoStore.TabIndex = 0;
            rdoStore.TabStop = true;
            rdoStore.Text = "From the &local computer's certificate store";
            rdoStore.UseVisualStyleBackColor = true;
            rdoStore.CheckedChanged += Source_CheckedChanged;
            //
            // lblStore
            //
            lblStore.Anchor = AnchorStyles.Left;
            lblStore.AutoSize = true;
            lblStore.Margin = new Padding(20, 0, 3, 0);
            lblStore.Name = "lblStore";
            lblStore.TabIndex = 1;
            lblStore.Text = "S&tore:";
            //
            // cboStore
            //
            cboStore.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboStore.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStore.Name = "cboStore";
            cboStore.TabIndex = 2;
            cboStore.SelectedIndexChanged += cboStore_SelectedIndexChanged;
            //
            // lblCertificate
            //
            lblCertificate.Anchor = AnchorStyles.Left;
            lblCertificate.AutoSize = true;
            lblCertificate.Margin = new Padding(20, 0, 3, 0);
            lblCertificate.Name = "lblCertificate";
            lblCertificate.TabIndex = 3;
            lblCertificate.Text = "&Certificate:";
            //
            // cboCertificate
            //
            cboCertificate.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboCertificate.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCertificate.Name = "cboCertificate";
            cboCertificate.TabIndex = 4;
            cboCertificate.SelectedIndexChanged += Endpoint_Changed;
            //
            // btnRefresh
            //
            btnRefresh.AutoSize = true;
            btnRefresh.Name = "btnRefresh";
            btnRefresh.TabIndex = 5;
            btnRefresh.Text = "Re&fresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            //
            // chkAllowInvalid
            //
            chkAllowInvalid.AutoSize = true;
            tblCertificate.SetColumnSpan(chkAllowInvalid, 2);
            chkAllowInvalid.Name = "chkAllowInvalid";
            chkAllowInvalid.TabIndex = 6;
            chkAllowInvalid.Text = "Allow an &invalid certificate (self-signed, expired or not trusted)";
            chkAllowInvalid.UseVisualStyleBackColor = true;
            chkAllowInvalid.CheckedChanged += Endpoint_Changed;
            //
            // rdoFile
            //
            rdoFile.AutoSize = true;
            tblCertificate.SetColumnSpan(rdoFile, 3);
            rdoFile.Name = "rdoFile";
            rdoFile.TabIndex = 7;
            rdoFile.Text = "From a .&pfx file";
            rdoFile.UseVisualStyleBackColor = true;
            rdoFile.CheckedChanged += Source_CheckedChanged;
            //
            // lblFile
            //
            lblFile.Anchor = AnchorStyles.Left;
            lblFile.AutoSize = true;
            lblFile.Margin = new Padding(20, 0, 3, 0);
            lblFile.Name = "lblFile";
            lblFile.TabIndex = 8;
            lblFile.Text = "F&ile:";
            //
            // txtPfxPath
            //
            txtPfxPath.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtPfxPath.Name = "txtPfxPath";
            txtPfxPath.TabIndex = 9;
            txtPfxPath.TextChanged += Endpoint_Changed;
            //
            // btnBrowse
            //
            btnBrowse.AutoSize = true;
            btnBrowse.Name = "btnBrowse";
            btnBrowse.TabIndex = 10;
            btnBrowse.Text = "&Browse...";
            btnBrowse.UseVisualStyleBackColor = true;
            btnBrowse.Click += btnBrowse_Click;
            //
            // lblPassword
            //
            lblPassword.Anchor = AnchorStyles.Left;
            lblPassword.AutoSize = true;
            lblPassword.Margin = new Padding(20, 0, 3, 0);
            lblPassword.Name = "lblPassword";
            lblPassword.TabIndex = 11;
            lblPassword.Text = "Pass&word:";
            //
            // txtPassword
            //
            txtPassword.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtPassword.Name = "txtPassword";
            txtPassword.TabIndex = 12;
            txtPassword.UseSystemPasswordChar = true;
            txtPassword.TextChanged += Endpoint_Changed;
            //
            // lblPasswordNote
            //
            lblPasswordNote.AutoSize = true;
            tblCertificate.SetColumnSpan(lblPasswordNote, 2);
            lblPasswordNote.ForeColor = SystemColors.GrayText;
            lblPasswordNote.MaximumSize = new Size(480, 0);
            lblPasswordNote.Name = "lblPasswordNote";
            lblPasswordNote.TabIndex = 13;
            lblPasswordNote.Text = "The password is saved in appsettings.json as plain text, where any local user can read it. A certificate from the store is safer.";
            //
            // grpService
            //
            grpService.AutoSize = true;
            grpService.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grpService.Controls.Add(tblService);
            grpService.Dock = DockStyle.Fill;
            grpService.Name = "grpService";
            grpService.Padding = new Padding(6);
            grpService.TabIndex = 3;
            grpService.TabStop = false;
            grpService.Text = "IIGOPanel service";
            //
            // tblService
            //
            tblService.AutoSize = true;
            tblService.ColumnCount = 4;
            tblService.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblService.ColumnStyles.Add(new ColumnStyle());
            tblService.ColumnStyles.Add(new ColumnStyle());
            tblService.ColumnStyles.Add(new ColumnStyle());
            tblService.Controls.Add(lblServiceStatus, 0, 0);
            tblService.Controls.Add(btnStart, 1, 0);
            tblService.Controls.Add(btnStop, 2, 0);
            tblService.Controls.Add(btnRestart, 3, 0);
            tblService.Dock = DockStyle.Fill;
            tblService.Name = "tblService";
            tblService.RowCount = 1;
            tblService.RowStyles.Add(new RowStyle());
            tblService.TabIndex = 0;
            //
            // lblServiceStatus
            //
            lblServiceStatus.Anchor = AnchorStyles.Left;
            lblServiceStatus.AutoSize = true;
            lblServiceStatus.Name = "lblServiceStatus";
            lblServiceStatus.TabIndex = 0;
            lblServiceStatus.Text = "Status:";
            //
            // btnStart
            //
            btnStart.AutoSize = true;
            btnStart.Name = "btnStart";
            btnStart.TabIndex = 1;
            btnStart.Text = "St&art";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            //
            // btnStop
            //
            btnStop.AutoSize = true;
            btnStop.Name = "btnStop";
            btnStop.TabIndex = 2;
            btnStop.Text = "St&op";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            //
            // btnRestart
            //
            btnRestart.AutoSize = true;
            btnRestart.Name = "btnRestart";
            btnRestart.TabIndex = 3;
            btnRestart.Text = "R&estart";
            btnRestart.UseVisualStyleBackColor = true;
            btnRestart.Click += btnRestart_Click;
            //
            // tblButtons
            //
            tblButtons.AutoSize = true;
            tblButtons.ColumnCount = 4;
            tblButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblButtons.ColumnStyles.Add(new ColumnStyle());
            tblButtons.ColumnStyles.Add(new ColumnStyle());
            tblButtons.ColumnStyles.Add(new ColumnStyle());
            tblButtons.Controls.Add(lblMessage, 0, 0);
            tblButtons.Controls.Add(btnSave, 1, 0);
            tblButtons.Controls.Add(btnSaveRestart, 2, 0);
            tblButtons.Controls.Add(btnClose, 3, 0);
            tblButtons.Dock = DockStyle.Fill;
            tblButtons.Name = "tblButtons";
            tblButtons.RowCount = 1;
            tblButtons.RowStyles.Add(new RowStyle());
            tblButtons.TabIndex = 4;
            //
            // lblMessage
            //
            lblMessage.Anchor = AnchorStyles.Left;
            lblMessage.AutoSize = true;
            lblMessage.MaximumSize = new Size(300, 0);
            lblMessage.Name = "lblMessage";
            lblMessage.TabIndex = 0;
            //
            // btnSave
            //
            btnSave.AutoSize = true;
            btnSave.Name = "btnSave";
            btnSave.TabIndex = 1;
            btnSave.Text = "&Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            //
            // btnSaveRestart
            //
            btnSaveRestart.AutoSize = true;
            btnSaveRestart.Name = "btnSaveRestart";
            btnSaveRestart.TabIndex = 2;
            btnSaveRestart.Text = "Save and restart &service";
            btnSaveRestart.UseVisualStyleBackColor = true;
            btnSaveRestart.Click += btnSaveRestart_Click;
            //
            // btnClose
            //
            btnClose.AutoSize = true;
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.Name = "btnClose";
            btnClose.TabIndex = 3;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            //
            // dlgOpenPfx
            //
            dlgOpenPfx.Filter = "Certificate files (*.pfx;*.p12)|*.pfx;*.p12|All files (*.*)|*.*";
            dlgOpenPfx.Title = "Choose the certificate file";
            //
            // MainForm
            //
            AcceptButton = btnSaveRestart;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            CancelButton = btnClose;
            Controls.Add(tblRoot);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "IIGO Panel Configuration";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            tblRoot.ResumeLayout(false);
            tblRoot.PerformLayout();
            grpFile.ResumeLayout(false);
            grpFile.PerformLayout();
            tblFile.ResumeLayout(false);
            tblFile.PerformLayout();
            grpEndpoint.ResumeLayout(false);
            grpEndpoint.PerformLayout();
            tblEndpoint.ResumeLayout(false);
            tblEndpoint.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numPort).EndInit();
            grpCertificate.ResumeLayout(false);
            grpCertificate.PerformLayout();
            tblCertificate.ResumeLayout(false);
            tblCertificate.PerformLayout();
            grpService.ResumeLayout(false);
            grpService.PerformLayout();
            tblService.ResumeLayout(false);
            tblService.PerformLayout();
            tblButtons.ResumeLayout(false);
            tblButtons.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TableLayoutPanel tblRoot;
        private GroupBox grpFile;
        private TableLayoutPanel tblFile;
        private TextBox txtPath;
        private Button btnReload;
        private GroupBox grpEndpoint;
        private TableLayoutPanel tblEndpoint;
        private Label lblHost;
        private ComboBox cboHost;
        private Label lblHostNote;
        private Label lblPort;
        private NumericUpDown numPort;
        private Label lblUrl;
        private Label lblUrlValue;
        private GroupBox grpCertificate;
        private TableLayoutPanel tblCertificate;
        private RadioButton rdoStore;
        private Label lblStore;
        private ComboBox cboStore;
        private Label lblCertificate;
        private ComboBox cboCertificate;
        private Button btnRefresh;
        private CheckBox chkAllowInvalid;
        private RadioButton rdoFile;
        private Label lblFile;
        private TextBox txtPfxPath;
        private Button btnBrowse;
        private Label lblPassword;
        private TextBox txtPassword;
        private Label lblPasswordNote;
        private GroupBox grpService;
        private TableLayoutPanel tblService;
        private Label lblServiceStatus;
        private Button btnStart;
        private Button btnStop;
        private Button btnRestart;
        private TableLayoutPanel tblButtons;
        private Label lblMessage;
        private Button btnSave;
        private Button btnSaveRestart;
        private Button btnClose;
        private OpenFileDialog dlgOpenPfx;
    }
}
