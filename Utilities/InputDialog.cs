using System;
using System.Drawing;
using System.Windows.Forms;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    public class InputDialog : Form
    {
        private Label promptLabel;
        private TextBox inputTextBox;
        private Button okButton;
        private Button cancelButton;
        private Font _normalFont;
        private Font _buttonFont;
        private Font _cancelFont;

        public string InputValue { get { return inputTextBox.Text.Trim(); } }

        public InputDialog(string title, string prompt, string defaultValue)
        {
            _normalFont = new Font("AUMOVIO Screen", 9F);
            _buttonFont = new Font("AUMOVIO Screen", 8F, FontStyle.Bold);
            _cancelFont = new Font("AUMOVIO Screen", 8F);

            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;
            this.Size = new Size(380, 160);

            promptLabel = new Label
            {
                Text = prompt,
                Location = new Point(15, 15),
                AutoSize = true,
                Font = _normalFont
            };
            this.Controls.Add(promptLabel);

            inputTextBox = new TextBox
            {
                Location = new Point(15, 40),
                Size = new Size(335, 23),
                Font = _normalFont,
                Text = defaultValue ?? ""
            };
            this.Controls.Add(inputTextBox);

            okButton = new Button
            {
                Text = "OK",
                Location = new Point(190, 80),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(41, 128, 185),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = _buttonFont
            };
            this.Controls.Add(okButton);

            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(275, 80),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                Font = _cancelFont
            };
            this.Controls.Add(cancelButton);

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_normalFont != null) { _normalFont.Dispose(); _normalFont = null; }
                if (_buttonFont != null) { _buttonFont.Dispose(); _buttonFont = null; }
                if (_cancelFont != null) { _cancelFont.Dispose(); _cancelFont = null; }
            }
            base.Dispose(disposing);
        }

        public static DialogResult Show(IWin32Window owner, string title, string prompt, string defaultValue, out string result)
        {
            using (var dialog = new InputDialog(title, prompt, defaultValue))
            {
                DialogResult dialogResult = dialog.ShowDialog(owner);
                result = dialog.InputValue;
                return dialogResult;
            }
        }
    }
}
