using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using IntelligentMutexExecutionEnvironment.Models;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    public class ServerConfigDialog : Form
    {
        private CheckBox _enableServerModeCheckBox;
        private RadioButton _allGroupsRadio;
        private RadioButton _selectedGroupsRadio;
        private CheckedListBox _groupsCheckedListBox;
        private Label _groupsLabel;
        private Button _okButton;
        private Button _cancelButton;

        private Font _normalFont;
        private Font _boldFont;
        private Font _buttonFont;

        private readonly List<ApplicationGroup> _groups;

        public bool IsServerMode { get { return _enableServerModeCheckBox.Checked; } }
        public int ServerAutoRunMode
        {
            get
            {
                if (!_enableServerModeCheckBox.Checked) return 0;
                return _selectedGroupsRadio.Checked ? 2 : 1;
            }
        }
        public string ServerAutoRunGroupIds
        {
            get
            {
                if (!_enableServerModeCheckBox.Checked || !_selectedGroupsRadio.Checked)
                    return "";
                var ids = new List<string>();
                foreach (int i in _groupsCheckedListBox.CheckedIndices)
                {
                    var group = _groupsCheckedListBox.Items[i] as ApplicationGroup;
                    if (group != null) ids.Add(group.GroupId.ToString());
                }
                return string.Join(",", ids);
            }
        }

        public ServerConfigDialog(List<ApplicationGroup> groups, bool isServerMode, int serverAutoRunMode, List<int> selectedGroupIds)
        {
            _groups = groups ?? new List<ApplicationGroup>();

            _normalFont = new Font("AUMOVIO Screen", 9F);
            _boldFont = new Font("AUMOVIO Screen", 9F, FontStyle.Bold);
            _buttonFont = new Font("AUMOVIO Screen", 8F, FontStyle.Bold);

            InitializeControls(isServerMode, serverAutoRunMode, selectedGroupIds);
        }

        private void InitializeControls(bool isServerMode, int serverAutoRunMode, List<int> selectedGroupIds)
        {
            this.Text = "Server Startup Configuration";
            this.Size = new Size(420, 380);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = _normalFont;
            this.BackColor = Color.White;

            int labelX = 20;
            int y = 15;

            // Section header
            var headerLabel = new Label
            {
                Text = "Server Startup",
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(headerLabel);
            y += 25;

            // Enable server mode checkbox
            _enableServerModeCheckBox = new CheckBox
            {
                Text = "Enable Server Mode",
                Checked = isServerMode,
                Location = new Point(labelX, y),
                AutoSize = true,
                Font = _boldFont
            };
            this.Controls.Add(_enableServerModeCheckBox);
            y += 25;

            // Description
            var descLabel = new Label
            {
                Text = "When enabled, IMEE automatically starts the configured\ngroups when Windows starts (requires Run on Startup).",
                Location = new Point(labelX + 17, y),
                Size = new Size(360, 36),
                ForeColor = Color.Gray,
                Font = new Font(_normalFont.FontFamily, 7.5F)
            };
            this.Controls.Add(descLabel);
            y += 48;

            // Radio buttons
            _allGroupsRadio = new RadioButton
            {
                Text = "Auto-run all groups",
                Location = new Point(labelX + 17, y),
                AutoSize = true,
                Checked = (serverAutoRunMode != 2)
            };
            this.Controls.Add(_allGroupsRadio);
            y += 26;

            _selectedGroupsRadio = new RadioButton
            {
                Text = "Auto-run selected groups only",
                Location = new Point(labelX + 17, y),
                AutoSize = true,
                Checked = (serverAutoRunMode == 2)
            };
            this.Controls.Add(_selectedGroupsRadio);
            y += 30;

            // Groups list label
            _groupsLabel = new Label
            {
                Text = "Groups to auto-run:",
                Location = new Point(labelX + 34, y),
                AutoSize = true,
                ForeColor = Color.DimGray
            };
            this.Controls.Add(_groupsLabel);
            y += 20;

            // Groups CheckedListBox
            _groupsCheckedListBox = new CheckedListBox
            {
                Location = new Point(labelX + 34, y),
                Size = new Size(330, 100),
                Font = _normalFont,
                CheckOnClick = true
            };
            foreach (var group in _groups)
            {
                bool isChecked = selectedGroupIds != null && selectedGroupIds.Contains(group.GroupId);
                _groupsCheckedListBox.Items.Add(group, isChecked);
            }
            this.Controls.Add(_groupsCheckedListBox);
            y += 110;

            // Buttons
            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(210, y),
                Size = new Size(80, 30),
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
                Location = new Point(300, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("AUMOVIO Screen", 8F)
            };
            this.Controls.Add(_cancelButton);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;

            // Wire enable toggle
            _enableServerModeCheckBox.CheckedChanged += OnEnableChanged;
            _selectedGroupsRadio.CheckedChanged += OnRunModeChanged;

            UpdateControlStates();
        }

        private void OnEnableChanged(object sender, EventArgs e)
        {
            UpdateControlStates();
        }

        private void OnRunModeChanged(object sender, EventArgs e)
        {
            UpdateControlStates();
        }

        private void UpdateControlStates()
        {
            bool enabled = _enableServerModeCheckBox.Checked;
            _allGroupsRadio.Enabled = enabled;
            _selectedGroupsRadio.Enabled = enabled;

            bool showGroups = enabled && _selectedGroupsRadio.Checked;
            _groupsLabel.Enabled = showGroups;
            _groupsCheckedListBox.Enabled = showGroups;
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
                if (_buttonFont != null) { _buttonFont.Dispose(); _buttonFont = null; }
            }
            base.Dispose(disposing);
        }
    }
}
