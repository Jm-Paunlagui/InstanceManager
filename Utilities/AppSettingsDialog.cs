using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using IntelligentMutexExecutionEnvironment.Models;

namespace IntelligentMutexExecutionEnvironment
{
    public class EditAppDialog : Form
    {
        private ManagedApplication _app;
        private TextBox _directoryTextBox;
        private Button _browseButton;
        private Button _openPathButton;
        private CheckBox _keepOpenCheckBox;
        private NumericUpDown _startDelayNumeric;
        private NumericUpDown _maxRetriesNumeric;
        private NumericUpDown _startupDelayNumeric;
        private NumericUpDown _stableRunNumeric;
        private NumericUpDown _notRespondingTimeoutNumeric;
        private NumericUpDown _memoryLimitNumeric;
        private CheckBox _detectTitleChangeCheckBox;
        private Button _resetCrashButton;
        private Button _resetRetryButton;
        private Label _crashCountLabel;
        private Label _retryCountLabel;
        private Label _lastExitCodeLabel;
        private Button _okButton;
        private Button _cancelButton;

        // Shared font instances to avoid creating duplicate Font objects
        private Font _normalFont;
        private Font _boldFont;
        private Font _smallBoldFont;
        private Font _buttonFont;

        public string NewDirectory { get { return _directoryTextBox.Text.Trim(); } }

        public EditAppDialog(ManagedApplication app)
        {
            _app = app;

            // Create shared fonts once
            _normalFont = new Font("AUMOVIO Screen", 9F);
            _boldFont = new Font("AUMOVIO Screen", 9F, FontStyle.Bold);
            _smallBoldFont = new Font("AUMOVIO Screen", 7F, FontStyle.Bold);
            _buttonFont = new Font("AUMOVIO Screen", 8F, FontStyle.Bold);

            InitializeControls();
        }

        private void InitializeControls()
        {
            this.Text = $"Edit - {_app.AppName}";
            this.Size = new Size(500, 705);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = _normalFont;

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
                Font = _boldFont
            };
            this.Controls.Add(dirSectionLabel);

            y += 22;

            _directoryTextBox = new TextBox
            {
                Text = _app.Directory ?? "",
                Location = new Point(labelX, y),
                Size = new Size(280, 23),
                Font = _normalFont
            };
            _browseButton = new Button
            {
                Text = "Browse",
                Location = new Point(308, y - 1),
                Size = new Size(75, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _browseButton.Click += BrowseButton_Click;
            _openPathButton = new Button
            {
                Text = "Open",
                Location = new Point(388, y - 1),
                Size = new Size(75, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(155, 89, 182),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _openPathButton.Click += OpenPathButton_Click;
            this.Controls.Add(_directoryTextBox);
            this.Controls.Add(_browseButton);
            this.Controls.Add(_openPathButton);

            y += rowHeight + 10;

            // --- Startup Settings Section ---
            var startupSectionLabel = new Label
            {
                Text = "Startup Settings",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(startupSectionLabel);

            y += 22;

            // Startup Delay
            var startupDelayLabel = new Label
            {
                Text = "Startup Delay (sec):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _startupDelayNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 300,
                Value = Math.Max(0, _app.StartupDelaySeconds),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            var startupDelayHint = new Label
            {
                Text = "Wait time before launching in Start All",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(startupDelayLabel);
            this.Controls.Add(_startupDelayNumeric);
            this.Controls.Add(startupDelayHint);

            y += rowHeight + 10;

            // --- Crash Recovery Settings Section ---
            var settingsSectionLabel = new Label
            {
                Text = "Crash Recovery Settings",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
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
                Location = new Point(controlX + 8, y),
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

            // Max Retries
            var maxRetriesLabel = new Label
            {
                Text = "Max Retries:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _maxRetriesNumeric = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 100,
                Value = Math.Max(1, _app.MaxRetries),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            this.Controls.Add(maxRetriesLabel);
            this.Controls.Add(_maxRetriesNumeric);

            y += rowHeight;

            // Start Delay
            var startDelayLabel = new Label
            {
                Text = "Restart Delay (sec):",
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

            // Stable Run Period
            var stableRunLabel = new Label
            {
                Text = "Stable Run Period (sec):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _stableRunNumeric = new NumericUpDown
            {
                Minimum = 5,
                Maximum = 600,
                Value = Math.Max(5, _app.StableRunPeriodSeconds),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            var stableRunHint = new Label
            {
                Text = "Run time before retry count resets",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(stableRunLabel);
            this.Controls.Add(_stableRunNumeric);
            this.Controls.Add(stableRunHint);

            y += rowHeight + 10;

            // --- Health Monitoring Section ---
            var healthSectionLabel = new Label
            {
                Text = "Health Monitoring",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(healthSectionLabel);

            y += 22;

            // Not Responding Timeout
            var nrtLabel = new Label
            {
                Text = "Not Responding (sec):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _notRespondingTimeoutNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 600,
                Value = Math.Max(0, Math.Min(600, _app.NotRespondingTimeoutSeconds)),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            var nrtHint = new Label
            {
                Text = "0 = default (2 poll cycles)",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(nrtLabel);
            this.Controls.Add(_notRespondingTimeoutNumeric);
            this.Controls.Add(nrtHint);

            y += rowHeight;

            // Memory Limit
            var memLabel = new Label
            {
                Text = "Memory Limit (MB):",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _memoryLimitNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 65536,
                Value = Math.Max(0, Math.Min(65536, _app.MemoryLimitMB)),
                Location = new Point(controlX, y),
                Size = new Size(80, 23)
            };
            var memHint = new Label
            {
                Text = "0 = no limit (disabled)",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(memLabel);
            this.Controls.Add(_memoryLimitNumeric);
            this.Controls.Add(memHint);

            y += rowHeight;

            // Detect Title Change
            var dtcLabel = new Label
            {
                Text = "Detect Title Change:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            _detectTitleChangeCheckBox = new CheckBox
            {
                Checked = _app.DetectTitleChange,
                Location = new Point(controlX + 8, y),
                AutoSize = true,
                Text = _app.DetectTitleChange ? "Yes" : "No"
            };
            _detectTitleChangeCheckBox.CheckedChanged += (s, e) =>
            {
                _detectTitleChangeCheckBox.Text = _detectTitleChangeCheckBox.Checked ? "Yes" : "No";
            };
            var dtcHint = new Label
            {
                Text = "Kill if window title changes",
                Location = new Point(controlX + 85, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(dtcLabel);
            this.Controls.Add(_detectTitleChangeCheckBox);
            this.Controls.Add(dtcHint);

            y += rowHeight + 10;

            // --- Statistics Section ---
            var statsSectionLabel = new Label
            {
                Text = "Statistics",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(statsSectionLabel);

            y += 22;

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
                Font = _boldFont
            };
            _resetCrashButton = new Button
            {
                Text = "Reset",
                Location = new Point(controlX + 60, y),
                Size = new Size(60, 23),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = _smallBoldFont
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
                Text = $"{_app.RetryCount} / {_app.MaxRetries}",
                Location = new Point(controlX, y + 2),
                AutoSize = true,
                Font = _boldFont
            };
            _resetRetryButton = new Button
            {
                Text = "Reset",
                Location = new Point(controlX + 60, y),
                Size = new Size(60, 23),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _resetRetryButton.Click += (s, e) =>
            {
                _app.RetryCount = 0;
                _retryCountLabel.Text = $"0 / {(int)_maxRetriesNumeric.Value}";
            };
            this.Controls.Add(retryLabel);
            this.Controls.Add(_retryCountLabel);
            this.Controls.Add(_resetRetryButton);

            y += rowHeight;

            // Last Exit Code
            var exitCodeLabel = new Label
            {
                Text = "Last Exit Code:",
                Location = new Point(labelX, y + 2),
                AutoSize = true
            };
            string exitCodeText = _app.LastExitCode.HasValue ? _app.LastExitCode.Value.ToString() : "N/A";
            if (_app.LastExitCode.HasValue && _app.LastExitCode.Value != 0)
            {
                exitCodeText += " (abnormal)";
            }
            _lastExitCodeLabel = new Label
            {
                Text = exitCodeText,
                Location = new Point(controlX, y + 2),
                AutoSize = true,
                Font = _boldFont,
                ForeColor = (_app.LastExitCode.HasValue && _app.LastExitCode.Value != 0) ? Color.Red : Color.Black
            };
            this.Controls.Add(exitCodeLabel);
            this.Controls.Add(_lastExitCodeLabel);

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
                Font = _buttonFont
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
                Font = _buttonFont
            };

            this.Controls.Add(_okButton);
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_normalFont != null) { _normalFont.Dispose(); _normalFont = null; }
                if (_boldFont != null) { _boldFont.Dispose(); _boldFont = null; }
                if (_smallBoldFont != null) { _smallBoldFont.Dispose(); _smallBoldFont = null; }
                if (_buttonFont != null) { _buttonFont.Dispose(); _buttonFont = null; }
            }
            base.Dispose(disposing);
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

        private void OpenPathButton_Click(object sender, EventArgs e)
        {
            try
            {
                string path = _directoryTextBox.Text.Trim();
                if (string.IsNullOrEmpty(path))
                {
                    MessageBox.Show(this, "Application path is empty.", "Open Path",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", "/select, \"" + path + "\"");
                }
                else
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                    {
                        Process.Start("explorer.exe", "\"" + dir + "\"");
                    }
                    else
                    {
                        MessageBox.Show(this, "The specified path does not exist.", "Open Path",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening path: {ex.Message}", "Open Path",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            _app.MaxRetries = (int)_maxRetriesNumeric.Value;
            _app.StartDelaySeconds = (int)_startDelayNumeric.Value;
            _app.StartupDelaySeconds = (int)_startupDelayNumeric.Value;
            _app.StableRunPeriodSeconds = (int)_stableRunNumeric.Value;
            _app.NotRespondingTimeoutSeconds = (int)_notRespondingTimeoutNumeric.Value;
            _app.MemoryLimitMB = (int)_memoryLimitNumeric.Value;
            _app.DetectTitleChange = _detectTitleChangeCheckBox.Checked;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditAppDialog));
            this.SuspendLayout();
            // 
            // EditAppDialog
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "EditAppDialog";
            this.ResumeLayout(false);

        }
    }
}
