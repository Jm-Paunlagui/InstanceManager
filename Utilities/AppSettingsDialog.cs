using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using InstanceManager.Models;

namespace InstanceManager
{
    public class EditAppDialog : Form
    {
        private ManagedApplication _app;
        private TextBox _directoryTextBox;
        private Button _browseButton;
        private CheckBox _keepOpenCheckBox;
        private NumericUpDown _startDelayNumeric;
        private Button _resetCrashButton;
        private Button _resetRetryButton;
        private Label _crashCountLabel;
        private Label _retryCountLabel;
        private Button _okButton;
        private Button _cancelButton;

        public string NewDirectory { get { return _directoryTextBox.Text.Trim(); } }

        public EditAppDialog(ManagedApplication app)
        {
            _app = app;
            InitializeControls();
        }

        private void InitializeControls()
        {
            this.Text = $"Edit - {_app.AppName}";
            this.Size = new Size(500, 330);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F);

            int labelX = 20;
            int controlX = 170;
            int y = 20;
            int rowHeight = 32;

            // --- Directory Section ---
            var dirSectionLabel = new Label
            {
                Text = "Application Path",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            this.Controls.Add(dirSectionLabel);

            y += 22;

            _directoryTextBox = new TextBox
            {
                Text = _app.Directory ?? "",
                Location = new Point(labelX, y),
                Size = new Size(360, 23),
                Font = new Font("Segoe UI", 9F)
            };
            _browseButton = new Button
            {
                Text = "Browse",
                Location = new Point(388, y - 1),
                Size = new Size(75, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold)
            };
            _browseButton.Click += BrowseButton_Click;
            this.Controls.Add(_directoryTextBox);
            this.Controls.Add(_browseButton);

            y += rowHeight + 10;

            // --- Settings Section ---
            var settingsSectionLabel = new Label
            {
                Text = "Crash Recovery Settings",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            this.Controls.Add(settingsSectionLabel);

            y += 22;

            // Keep Open
            var keepOpenLabel = new Label
            {
                Text = "Keep Open (Auto-Restart):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _keepOpenCheckBox = new CheckBox
            {
                Checked = _app.KeepOpen,
                Location = new Point(controlX, y),
                AutoSize = true,
                Text = _app.KeepOpen ? "Yes" : "No"
            };
            _keepOpenCheckBox.CheckedChanged += (s, e) =>
            {
                _keepOpenCheckBox.Text = _keepOpenCheckBox.Checked ? "Yes" : "No";
            };
            this.Controls.Add(keepOpenLabel);
            this.Controls.Add(_keepOpenCheckBox);

            y += rowHeight;

            // Start Delay
            var startDelayLabel = new Label
            {
                Text = "Restart Delay (seconds):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _startDelayNumeric = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 300,
                Value = Math.Max(1, _app.StartDelaySeconds),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            this.Controls.Add(startDelayLabel);
            this.Controls.Add(_startDelayNumeric);

            y += rowHeight;

            // Crash Count
            var crashLabel = new Label
            {
                Text = "Crash Count:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _crashCountLabel = new Label
            {
                Text = _app.CrashCount.ToString(),
                Location = new Point(controlX, y + 2),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _resetCrashButton = new Button
            {
                Text = "Reset",
                Location = new Point(controlX + 60, y),
                Size = new Size(60, 23),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold)
            };
            _resetCrashButton.Click += (s, e) =>
            {
                _app.CrashCount = 0;
                _crashCountLabel.Text = "0";
            };
            this.Controls.Add(crashLabel);
            this.Controls.Add(_crashCountLabel);
            this.Controls.Add(_resetCrashButton);

            y += rowHeight;

            // Retry Count
            var retryLabel = new Label
            {
                Text = "Retry Count:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _retryCountLabel = new Label
            {
                Text = _app.RetryCount.ToString(),
                Location = new Point(controlX, y + 2),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _resetRetryButton = new Button
            {
                Text = "Reset",
                Location = new Point(controlX + 60, y),
                Size = new Size(60, 23),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold)
            };
            _resetRetryButton.Click += (s, e) =>
            {
                _app.RetryCount = 0;
                _retryCountLabel.Text = "0";
            };
            this.Controls.Add(retryLabel);
            this.Controls.Add(_retryCountLabel);
            this.Controls.Add(_resetRetryButton);

            y += rowHeight + 15;

            // OK / Cancel buttons
            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(290, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            _okButton.Click += OkButton_Click;

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(380, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };

            this.Controls.Add(_okButton);
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
                dialog.Title = "Select Application";
                if (!string.IsNullOrEmpty(_directoryTextBox.Text) && File.Exists(_directoryTextBox.Text))
                {
                    dialog.FileName = _directoryTextBox.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _directoryTextBox.Text = dialog.FileName;
                }
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_directoryTextBox.Text.Trim()))
            {
                MessageBox.Show(this, "Application path cannot be empty.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _app.KeepOpen = _keepOpenCheckBox.Checked;
            _app.StartDelaySeconds = (int)_startDelayNumeric.Value;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
