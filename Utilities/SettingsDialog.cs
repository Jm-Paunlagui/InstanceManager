using System;
using System.Drawing;
using System.Windows.Forms;
using IntelligentMutexExecutionEnvironment.Services;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    public class SettingsDialog : Form
    {
        private TextBox _stationNameTextBox;
        private CheckBox _runOnStartupCheckBox;
        private NumericUpDown _fontSizePercentNumeric;
        private Label _fontSizePreviewLabel;

        private NumericUpDown _statusPollIntervalNumeric;
        private NumericUpDown _startGracePeriodNumeric;
        private NumericUpDown _gcCollectIntervalNumeric;
        private NumericUpDown _storageSaveIntervalNumeric;

        private NumericUpDown _logFlushIntervalNumeric;
        private NumericUpDown _logBufferSizeNumeric;
        private NumericUpDown _logRetentionDaysNumeric;

        private Button _okButton;
        private Button _cancelButton;
        private Button _resetDefaultsButton;

        private Font _normalFont;
        private Font _boldFont;
        private Font _smallBoldFont;
        private Font _buttonFont;

        // Public properties for retrieving values after OK
        public string StationName { get { return _stationNameTextBox.Text.Trim(); } }
        public bool RunOnStartup { get { return _runOnStartupCheckBox.Checked; } }
        public int FontSizePercent { get { return (int)_fontSizePercentNumeric.Value; } }
        public int StatusPollIntervalMs { get { return (int)_statusPollIntervalNumeric.Value; } }
        public int StartGracePeriodSeconds { get { return (int)_startGracePeriodNumeric.Value; } }
        public int GcCollectIntervalMinutes { get { return (int)_gcCollectIntervalNumeric.Value; } }
        public int StorageSaveIntervalSeconds { get { return (int)_storageSaveIntervalNumeric.Value; } }
        public int LogFlushIntervalSeconds { get { return (int)_logFlushIntervalNumeric.Value; } }
        public int LogBufferSize { get { return (int)_logBufferSizeNumeric.Value; } }
        public int LogRetentionDays { get { return (int)_logRetentionDaysNumeric.Value; } }

        public SettingsDialog(SettingsService settings)
        {
            _normalFont = DpiScaler.CreateFont("AUMOVIO Screen", 9F);
            _boldFont = DpiScaler.CreateFont("AUMOVIO Screen", 9F, FontStyle.Bold);
            _smallBoldFont = DpiScaler.CreateFont("AUMOVIO Screen", 7F, FontStyle.Bold);
            _buttonFont = DpiScaler.CreateFont("AUMOVIO Screen", 8F, FontStyle.Bold);

            InitializeControls(settings);
        }

        private void InitializeControls(SettingsService settings)
        {
            this.Text = "Settings";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = _normalFont;
            this.BackColor = Color.White;
            this.AutoScaleMode = AutoScaleMode.None;

            int labelX = DpiScaler.Scale(20);
            int controlX = DpiScaler.Scale(270);
            int y = DpiScaler.Scale(15);
            int rowHeight = DpiScaler.Scale(30);

            // ===== General Settings =====
            var generalLabel = new Label
            {
                Text = "General",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(generalLabel);
            y += DpiScaler.Scale(22);

            // Station Name
            var stationLabel = new Label
            {
                Text = "Station Name:",
                Location = new Point(labelX, y + DpiScaler.Scale(3)),
                AutoSize = true
            };
            _stationNameTextBox = new TextBox
            {
                Text = settings.StationName,
                Location = new Point(controlX, y),
                Size = new Size(DpiScaler.Scale(210), DpiScaler.Scale(23)),
                Font = _normalFont
            };
            this.Controls.Add(stationLabel);
            this.Controls.Add(_stationNameTextBox);
            y += rowHeight;

            // Run on Startup
            _runOnStartupCheckBox = new CheckBox
            {
                Text = "Run on Windows Startup",
                Checked = settings.RunOnStartup,
                Location = new Point(labelX, y + DpiScaler.Scale(3)),
                AutoSize = true,
                Font = _normalFont
            };
            this.Controls.Add(_runOnStartupCheckBox);
            y += rowHeight - DpiScaler.Scale(5);

            // Description label for Run on Startup
            var startupDescLabel = new Label
            {
                Text = "Automatically launch IMEE when Windows starts so managed applications are monitored at all times.",
                Location = new Point(labelX + DpiScaler.Scale(17), y),
                Size = new Size(DpiScaler.Scale(460), DpiScaler.Scale(28)),
                ForeColor = Color.Gray,
                Font = DpiScaler.CreateFont("AUMOVIO Screen", 7.5F)
            };
            this.Controls.Add(startupDescLabel);
            y += DpiScaler.Scale(30) + DpiScaler.Scale(12);

            // ===== Display Settings =====
            var displayLabel = new Label
            {
                Text = "Display",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(displayLabel);
            y += DpiScaler.Scale(22);

            // Font Size
            var fontSizeLabel = new Label
            {
                Text = "Font Size (%):",
                Location = new Point(labelX, y + DpiScaler.Scale(3)),
                AutoSize = true
            };
            _fontSizePercentNumeric = new NumericUpDown
            {
                Minimum = 75,
                Maximum = 200,
                Increment = 5,
                Value = Math.Max(75, Math.Min(200, settings.FontSizePercent)),
                Location = new Point(controlX, y),
                Size = new Size(DpiScaler.Scale(80), DpiScaler.Scale(23))
            };
            var fontSizeHintLabel = new Label
            {
                Text = "75 - 200",
                Location = new Point(controlX + DpiScaler.Scale(85), y + DpiScaler.Scale(3)),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(fontSizeLabel);
            this.Controls.Add(_fontSizePercentNumeric);
            this.Controls.Add(fontSizeHintLabel);
            y += rowHeight;

            // Font Size Preview
            _fontSizePreviewLabel = new Label
            {
                Text = "Preview: The quick brown fox jumps over the lazy dog",
                Location = new Point(labelX + DpiScaler.Scale(17), y),
                Size = new Size(DpiScaler.Scale(460), DpiScaler.Scale(22)),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            UpdateFontSizePreview();
            this.Controls.Add(_fontSizePreviewLabel);

            _fontSizePercentNumeric.ValueChanged += (s, e) => { UpdateFontSizePreview(); };
            y += DpiScaler.Scale(24);

            // Font size restart note
            var fontSizeNoteLabel = new Label
            {
                Text = "Changes to font size take effect after restarting IMEE.",
                Location = new Point(labelX + DpiScaler.Scale(17), y),
                Size = new Size(DpiScaler.Scale(460), DpiScaler.Scale(18)),
                ForeColor = Color.Gray,
                Font = DpiScaler.CreateFont("AUMOVIO Screen", 7.5F)
            };
            this.Controls.Add(fontSizeNoteLabel);
            y += DpiScaler.Scale(24) + DpiScaler.Scale(8);

            // ===== Performance Settings =====
            var perfLabel = new Label
            {
                Text = "Performance",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(perfLabel);
            y += DpiScaler.Scale(22);

            // Status Poll Interval
            AddNumericRow(ref y, labelX, controlX, "Status Poll Interval (ms):",
                1000, 60000, settings.StatusPollIntervalMs, out _statusPollIntervalNumeric,
                "1000 - 60000");

            // Start Grace Period
            AddNumericRow(ref y, labelX, controlX, "Start Grace Period (seconds):",
                1, 120, settings.StartGracePeriodSeconds, out _startGracePeriodNumeric,
                "1 - 120");

            // GC Collect Interval
            AddNumericRow(ref y, labelX, controlX, "GC Collect Interval (minutes):",
                5, 1440, settings.GcCollectIntervalMinutes, out _gcCollectIntervalNumeric,
                "5 - 1440");

            // Storage Save Interval
            AddNumericRow(ref y, labelX, controlX, "Storage Save Interval (seconds):",
                5, 300, settings.StorageSaveIntervalSeconds, out _storageSaveIntervalNumeric,
                "5 - 300");

            y += DpiScaler.Scale(12);

            // ===== Logging Settings =====
            var logLabel = new Label
            {
                Text = "Logging",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(logLabel);
            y += DpiScaler.Scale(22);

            // Log Flush Interval
            AddNumericRow(ref y, labelX, controlX, "Log Flush Interval (seconds):",
                1, 120, settings.LogFlushIntervalSeconds, out _logFlushIntervalNumeric,
                "1 - 120");

            // Log Buffer Size
            AddNumericRow(ref y, labelX, controlX, "Log Buffer Size (entries):",
                10, 1000, settings.LogBufferSize, out _logBufferSizeNumeric,
                "10 - 1000");

            // Log Retention Days
            AddNumericRow(ref y, labelX, controlX, "Log Retention (days):",
                1, 365, settings.LogRetentionDays, out _logRetentionDaysNumeric,
                "1 - 365");

            y += DpiScaler.Scale(20);

            // ===== Buttons =====
            _resetDefaultsButton = new Button
            {
                Text = "Reset Defaults",
                Location = new Point(labelX, y),
                Size = new Size(DpiScaler.Scale(110), DpiScaler.Scale(30)),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                Font = _smallBoldFont
            };
            _resetDefaultsButton.Click += ResetDefaultsButton_Click;
            this.Controls.Add(_resetDefaultsButton);

            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(DpiScaler.Scale(310), y),
                Size = new Size(DpiScaler.Scale(80), DpiScaler.Scale(30)),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Font = _buttonFont
            };
            _okButton.Click += OkButton_Click;
            this.Controls.Add(_okButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(DpiScaler.Scale(400), y),
                Size = new Size(DpiScaler.Scale(80), DpiScaler.Scale(30)),
                FlatStyle = FlatStyle.Flat,
                Font = DpiScaler.CreateFont("AUMOVIO Screen", 8F)
            };
            this.Controls.Add(_cancelButton);

            // Adjust form size to fit all controls
            this.Size = new Size(DpiScaler.Scale(520), y + DpiScaler.Scale(75));

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        private void UpdateFontSizePreview()
        {
            if (_fontSizePreviewLabel == null || _fontSizePercentNumeric == null) return;
            float previewSize = 9F * ((float)_fontSizePercentNumeric.Value / 100f);
            if (previewSize < 1F) previewSize = 1F;
            try
            {
                var oldFont = _fontSizePreviewLabel.Font;
                Font newFont;
                try
                {
                    newFont = new Font("AUMOVIO Screen", previewSize);
                }
                catch
                {
                    // Font family not available — fall back to system font
                    newFont = new Font(FontFamily.GenericSansSerif, previewSize);
                }
                _fontSizePreviewLabel.Font = newFont;
                if (oldFont != null && oldFont != _normalFont) oldFont.Dispose();
            }
            catch
            {
                // Font creation may fail for extreme sizes - ignore
            }
        }

        private void AddNumericRow(ref int y, int labelX, int controlX, string labelText,
            int min, int max, int value, out NumericUpDown numeric, string hint)
        {
            var label = new Label
            {
                Text = labelText,
                Location = new Point(labelX, y + DpiScaler.Scale(3)),
                AutoSize = true
            };
            numeric = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, value)),
                Location = new Point(controlX, y),
                Size = new Size(DpiScaler.Scale(80), DpiScaler.Scale(23))
            };
            var hintLabel = new Label
            {
                Text = hint,
                Location = new Point(controlX + DpiScaler.Scale(85), y + DpiScaler.Scale(3)),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = _normalFont
            };
            this.Controls.Add(label);
            this.Controls.Add(numeric);
            this.Controls.Add(hintLabel);
            y += DpiScaler.Scale(30);
        }

        private void ResetDefaultsButton_Click(object sender, EventArgs e)
        {
            _fontSizePercentNumeric.Value = SettingsService.DefaultFontSizePercent;
            _statusPollIntervalNumeric.Value = SettingsService.DefaultStatusPollIntervalMs;
            _startGracePeriodNumeric.Value = SettingsService.DefaultStartGracePeriodSeconds;
            _gcCollectIntervalNumeric.Value = SettingsService.DefaultGcCollectIntervalMinutes;
            _storageSaveIntervalNumeric.Value = SettingsService.DefaultStorageSaveIntervalSeconds;
            _logFlushIntervalNumeric.Value = SettingsService.DefaultLogFlushIntervalSeconds;
            _logBufferSizeNumeric.Value = SettingsService.DefaultLogBufferSize;
            _logRetentionDaysNumeric.Value = SettingsService.DefaultLogRetentionDays;
            // Station name is intentionally NOT reset
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
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

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsDialog));
            this.SuspendLayout();
            // 
            // SettingsDialog
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "SettingsDialog";
            this.ResumeLayout(false);

        }
    }
}
