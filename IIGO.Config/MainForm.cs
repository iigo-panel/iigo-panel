using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using Newtonsoft.Json;

namespace IIGO.Config
{
    public partial class MainForm : Form
    {
        private sealed record StoreOption(string Name, string Label)
        {
            public override string ToString() => Label;
        }

        private readonly string settingsPath;
        private readonly System.Windows.Forms.Timer statusTimer = new() { Interval = 2000 };

        private AppSettingsFile? settings;
        // The endpoint as it is in the file, which is what the running service uses
        private HttpsEndpoint? savedEndpoint;
        private bool loading;
        private bool dirty;
        private bool busy;

        public MainForm(string settingsPath)
        {
            InitializeComponent();
            this.settingsPath = settingsPath;

            if (Environment.ProcessPath is { } exe)
                Icon = Icon.ExtractAssociatedIcon(exe);

            statusTimer.Tick += (_, _) => RefreshServiceStatus();
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            AddHostChoices();
            LoadSettings();
            RefreshServiceStatus();
            statusTimer.Start();
            ActiveControl = cboHost;
        }

        private void AddHostChoices()
        {
            cboHost.Items.Add("*");
            cboHost.Items.Add("localhost");

            try
            {
                var addresses = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address)
                    .Where(a => !IPAddress.IsLoopback(a) && !a.IsIPv6LinkLocal)
                    .OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                    .Select(a => a.ToString())
                    .Distinct();

                foreach (var address in addresses)
                    cboHost.Items.Add(address);
            }
            catch (NetworkInformationException)
            {
                // The list is only a convenience; any address can be typed
            }
        }

        private void LoadSettings()
        {
            loading = true;
            settings = null;
            savedEndpoint = null;
            txtPath.Text = settingsPath;

            try
            {
                if (!File.Exists(settingsPath))
                {
                    ShowMessage($"{settingsPath} was not found.");
                    MessageBox.Show(this, $"{settingsPath} was not found. Run this program from the IIGO Panel folder, " +
                        "or pass the path to appsettings.json on the command line.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                settings = AppSettingsFile.Load(settingsPath);
                var endpoint = settings.Endpoint;
                savedEndpoint = settings.HasHttpsEndpoint ? endpoint : null;

                cboHost.Text = endpoint.Host;
                numPort.Value = Math.Clamp(endpoint.Port, (int)numPort.Minimum, (int)numPort.Maximum);

                cboStore.Items.Clear();
                cboStore.Items.Add(new StoreOption("My", "Personal (My)"));
                cboStore.Items.Add(new StoreOption("WebHosting", "Web Hosting (WebHosting)"));
                var store = cboStore.Items.Cast<StoreOption>().FirstOrDefault(s => s.Name.Equals(endpoint.StoreName, StringComparison.OrdinalIgnoreCase));
                if (store == null)
                {
                    store = new StoreOption(endpoint.StoreName, endpoint.StoreName);
                    cboStore.Items.Add(store);
                }
                cboStore.SelectedItem = store;

                chkAllowInvalid.Checked = endpoint.AllowInvalid;
                txtPfxPath.Text = endpoint.FilePath;
                txtPassword.Text = endpoint.FilePassword;
                rdoStore.Checked = endpoint.Source == CertificateSource.Store;
                rdoFile.Checked = endpoint.Source == CertificateSource.File;

                LoadCertificates(subject: endpoint.Subject);

                if (!settings.HasHttpsEndpoint)
                    ShowMessage("No HTTPS endpoint is set yet. Choose a certificate and save.");
                else if (endpoint.Source == CertificateSource.Store && endpoint.Subject.Length > 0 && cboCertificate.SelectedItem == null)
                    ShowMessage($"No certificate for \"{endpoint.Subject}\" is in this store. Choose another one.");
                else if (endpoint.Source == CertificateSource.Store && !endpoint.StoreLocation.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase))
                    ShowMessage($"The file uses the {endpoint.StoreLocation} store location. Saving changes it to LocalMachine.");
                else
                    ShowMessage("");
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                settings = null;
                ShowMessage("The settings file could not be read.");
                MessageBox.Show(this, $"{settingsPath} could not be read: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                loading = false;
                dirty = false;
                UpdateControls();
            }
        }

        private string SelectedStoreName => (cboStore.SelectedItem as StoreOption)?.Name ?? "My";

        /// <summary>
        /// Fills the certificate list, keeping the current choice when it is still there
        /// </summary>
        private void LoadCertificates(string? subject = null)
        {
            var thumbprint = (cboCertificate.SelectedItem as CertificateItem)?.Certificate.Thumbprint;

            cboCertificate.BeginUpdate();
            try
            {
                cboCertificate.Items.Clear();
                foreach (var item in CertificateStore.List(SelectedStoreName))
                    cboCertificate.Items.Add(item);

                var items = cboCertificate.Items.Cast<CertificateItem>().ToList();
                // The list is sorted newest first within a name, so a subject picks its latest certificate, as Kestrel does
                cboCertificate.SelectedItem =
                    (subject is { Length: > 0 } ? items.FirstOrDefault(i => i.SubjectName.Equals(subject, StringComparison.OrdinalIgnoreCase)) : null)
                    ?? (thumbprint != null ? items.FirstOrDefault(i => i.Certificate.Thumbprint == thumbprint) : null);
            }
            catch (CryptographicException ex)
            {
                ShowMessage($"The {SelectedStoreName} store could not be read: {ex.Message}");
            }
            finally
            {
                cboCertificate.EndUpdate();
            }
        }

        private HttpsEndpoint BuildEndpoint() => new()
        {
            Host = NormalizeHost(cboHost.Text),
            Port = (int)numPort.Value,
            Source = rdoFile.Checked ? CertificateSource.File : CertificateSource.Store,
            Subject = (cboCertificate.SelectedItem as CertificateItem)?.SubjectName ?? "",
            StoreName = SelectedStoreName,
            // The service runs as LocalSystem, so it reads the local machine's stores
            StoreLocation = "LocalMachine",
            AllowInvalid = chkAllowInvalid.Checked,
            FilePath = txtPfxPath.Text.Trim(),
            FilePassword = txtPassword.Text
        };

        private static string NormalizeHost(string host)
        {
            host = host.Trim().TrimStart('[').TrimEnd(']');
            return host.Length == 0 ? "*" : host;
        }

        /// <summary>
        /// Problems that would stop the panel from starting. Null when there are none.
        /// </summary>
        private string? Validate(HttpsEndpoint endpoint)
        {
            var host = endpoint.Host;
            if (host is not ("*" or "+") && !IPAddress.TryParse(host, out _) && Uri.CheckHostName(host) != UriHostNameType.Dns)
                return $"\"{host}\" is not an address or host name. Use * to listen on every address.";

            if (endpoint.Source == CertificateSource.Store)
            {
                if (cboCertificate.SelectedItem == null)
                    return "Choose a certificate.";
                if (endpoint.Subject.Length == 0)
                    return "The chosen certificate has no subject name, so Kestrel can't find it in the store. Choose another certificate or use a .pfx file.";
                return null;
            }

            if (endpoint.FilePath.Length == 0)
                return "Choose the .pfx file.";
            if (!File.Exists(endpoint.FilePath))
                return $"{endpoint.FilePath} was not found.";

            try
            {
                using var certificate = X509CertificateLoader.LoadPkcs12FromFile(endpoint.FilePath, endpoint.FilePassword.Length > 0 ? endpoint.FilePassword : null);
                if (!certificate.HasPrivateKey)
                    return "The .pfx file has no private key.";
            }
            catch (CryptographicException ex)
            {
                return $"The .pfx file could not be opened. Check the password. ({ex.Message})";
            }

            return null;
        }

        /// <summary>
        /// Kestrel finds store certificates by subject, so warn when it would load a different certificate than the one
        /// chosen, or none at all. True to go ahead and save.
        /// </summary>
        private bool ConfirmCertificate(HttpsEndpoint endpoint)
        {
            if (endpoint.Source != CertificateSource.Store || cboCertificate.SelectedItem is not CertificateItem selected)
                return true;

            X509Certificate2? choice;
            using (new WaitCursor())
                choice = CertificateStore.FindKestrelChoice(endpoint.Subject, endpoint.StoreName, endpoint.StoreLocation, endpoint.AllowInvalid);

            string? warning = null;
            if (choice == null)
                warning = $"The certificate for \"{endpoint.Subject}\" isn't valid on this server (it may be expired, self-signed or not trusted), " +
                    "so the panel won't start with it. Tick \"Allow an invalid certificate\" or choose another certificate.";
            else if (choice.Thumbprint != selected.Certificate.Thumbprint)
                warning = $"Kestrel finds certificates by name and uses the one that expires last. With these settings it will use " +
                    $"{new CertificateItem(choice)} instead of the one you chose.";

            return warning == null
                || MessageBox.Show(this, warning + "\n\nSave anyway?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private bool Save()
        {
            if (settings == null)
                return false;

            var endpoint = BuildEndpoint();
            if (Validate(endpoint) is { } error)
            {
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!ConfirmCertificate(endpoint))
                return false;

            try
            {
                settings.Save(endpoint);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, $"{settingsPath} could not be saved: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            savedEndpoint = endpoint;
            dirty = false;
            ShowMessage("Saved. Restart the service to use the new settings.");
            return true;
        }

        private void RefreshServiceStatus()
        {
            if (busy)
                return;

            var status = PanelService.GetStatus();
            lblServiceStatus.Text = status switch
            {
                null => "Status: not installed",
                ServiceControllerStatus.StartPending => "Status: starting",
                ServiceControllerStatus.StopPending => "Status: stopping",
                ServiceControllerStatus s => $"Status: {s.ToString().ToLowerInvariant()}"
            };

            btnStart.Enabled = status == ServiceControllerStatus.Stopped;
            btnStop.Enabled = status == ServiceControllerStatus.Running;
            btnRestart.Enabled = status == ServiceControllerStatus.Running;
            btnSaveRestart.Enabled = settings != null && status != null;
        }

        private void UpdateControls()
        {
            var fromStore = rdoStore.Checked;
            cboStore.Enabled = fromStore;
            cboCertificate.Enabled = fromStore;
            btnRefresh.Enabled = fromStore;
            chkAllowInvalid.Enabled = fromStore;
            txtPfxPath.Enabled = !fromStore;
            btnBrowse.Enabled = !fromStore;
            txtPassword.Enabled = !fromStore;

            lblUrlValue.Text = BuildEndpoint().Url;

            var canEdit = settings != null && !busy;
            grpEndpoint.Enabled = canEdit;
            grpCertificate.Enabled = canEdit;
            btnSave.Enabled = canEdit;
            btnReload.Enabled = !busy;
            RefreshServiceStatus();
        }

        private void ShowMessage(string message) => lblMessage.Text = message;

        private async Task RunServiceAction(string progress, Func<Task<ServiceResult>> action)
        {
            busy = true;
            UseWaitCursor = true;
            btnStart.Enabled = btnStop.Enabled = btnRestart.Enabled = btnSave.Enabled = btnSaveRestart.Enabled = btnReload.Enabled = false;
            grpEndpoint.Enabled = grpCertificate.Enabled = false;
            lblServiceStatus.Text = progress;
            ShowMessage("");

            try
            {
                var result = await action();
                ShowMessage(result.Success ? result.Message : "The service had a problem.");
                if (!result.Success)
                    MessageBox.Show(this, result.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or System.ServiceProcess.TimeoutException)
            {
                ShowMessage("The service had a problem.");
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                UseWaitCursor = false;
                UpdateControls();
            }
        }

        private void Endpoint_Changed(object? sender, EventArgs e)
        {
            if (loading)
                return;

            dirty = true;
            UpdateControls();
        }

        private void Source_CheckedChanged(object? sender, EventArgs e)
        {
            // Both buttons raise this; handle it once, for the one that is now checked
            if (sender is RadioButton { Checked: true })
                Endpoint_Changed(sender, e);
        }

        private void cboStore_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (loading)
                return;

            using (new WaitCursor())
                LoadCertificates();
            Endpoint_Changed(sender, e);
        }

        private void btnRefresh_Click(object? sender, EventArgs e)
        {
            using (new WaitCursor())
                LoadCertificates();
            Endpoint_Changed(sender, e);
        }

        private void btnBrowse_Click(object? sender, EventArgs e)
        {
            if (File.Exists(txtPfxPath.Text))
                dlgOpenPfx.FileName = txtPfxPath.Text;

            if (dlgOpenPfx.ShowDialog(this) == DialogResult.OK)
                txtPfxPath.Text = dlgOpenPfx.FileName;
        }

        private void btnReload_Click(object? sender, EventArgs e)
        {
            if (dirty && MessageBox.Show(this, "Discard your changes and read the file again?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            LoadSettings();
        }

        private void btnSave_Click(object? sender, EventArgs e) => Save();

        private async void btnSaveRestart_Click(object? sender, EventArgs e)
        {
            if (!Save())
                return;

            await RunServiceAction("Restarting...", () => PanelService.RestartAsync(savedEndpoint));
        }

        private async void btnStart_Click(object? sender, EventArgs e) =>
            await RunServiceAction("Starting...", () => PanelService.StartAsync(savedEndpoint));

        private async void btnStop_Click(object? sender, EventArgs e) =>
            await RunServiceAction("Stopping...", async () =>
            {
                await PanelService.StopAsync();
                return new ServiceResult(true, "The service is stopped.");
            });

        private async void btnRestart_Click(object? sender, EventArgs e)
        {
            if (dirty)
            {
                var answer = MessageBox.Show(this, "Save your changes before restarting? Choose No to restart with the saved settings.", Text,
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !Save()))
                    return;
            }

            await RunServiceAction("Restarting...", () => PanelService.RestartAsync(savedEndpoint));
        }

        private void btnClose_Click(object? sender, EventArgs e) => Close();

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (busy)
            {
                e.Cancel = true;
                return;
            }

            if (!dirty || settings == null)
                return;

            var answer = MessageBox.Show(this, "Save your changes?", Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !Save()))
                e.Cancel = true;
        }

        private sealed class WaitCursor : IDisposable
        {
            private readonly Cursor? previous = Cursor.Current;

            public WaitCursor() => Cursor.Current = Cursors.WaitCursor;

            public void Dispose() => Cursor.Current = previous;
        }
    }
}
