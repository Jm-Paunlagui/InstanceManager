using System;
using System.Drawing;
using System.Windows.Forms;

namespace InstanceManager.Utilities
{
    public class InputDialog : Form
    {
        private Label promptLabel;
        private TextBox inputTextBox;
        private Button okButton;
        private Button cancelButton;

        public string InputValue { get { return inputTextBox.Text.Trim(); } }

        public InputDialog(string title, string prompt, string defaultValue)
        {
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
                Font = new Font("AUMOVIO Screen", 9F)
            };
            this.Controls.Add(promptLabel);

            inputTextBox = new TextBox
            {
                Location = new Point(15, 40),
                Size = new Size(335, 23),
                Font = new Font("AUMOVIO Screen", 9F),
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
                Font = new Font("AUMOVIO Screen", 8F, FontStyle.Bold)
            };
            this.Controls.Add(okButton);

            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(275, 80),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("AUMOVIO Screen", 8F)
            };
            this.Controls.Add(cancelButton);

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
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
