using System;
using System.Drawing;
using System.Windows.Forms;

namespace InstanceManager.Utilities
{
    public class CustomMessageBox : Form
    {
        private Label messageLabel;
        private Button okButton;
        private Button yesButton;
        private Button noButton;
        private PictureBox iconPictureBox;

        public DialogResult Result { get; private set; }

        public CustomMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            InitializeComponents(message, title, buttons, icon);
        }

        private void InitializeComponents(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Size = new Size(400, 160);
            this.BackColor = Color.White;

            iconPictureBox = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(32, 32),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            SetIcon(icon);
            this.Controls.Add(iconPictureBox);

            messageLabel = new Label
            {
                Text = message,
                Location = new Point(65, 20),
                AutoSize = false,
                Size = new Size(310, 60),
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(messageLabel);

            int buttonY = 90;
            int buttonWidth = 80;
            int buttonHeight = 28;

            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    okButton = new Button
                    {
                        Text = "OK",
                        Location = new Point(160, buttonY),
                        Size = new Size(buttonWidth, buttonHeight),
                        DialogResult = DialogResult.OK
                    };
                    okButton.Click += (s, e) => { Result = DialogResult.OK; this.Close(); };
                    this.Controls.Add(okButton);
                    this.AcceptButton = okButton;
                    break;

                case MessageBoxButtons.YesNo:
                    yesButton = new Button
                    {
                        Text = "Yes",
                        Location = new Point(120, buttonY),
                        Size = new Size(buttonWidth, buttonHeight),
                        DialogResult = DialogResult.Yes
                    };
                    yesButton.Click += (s, e) => { Result = DialogResult.Yes; this.Close(); };
                    this.Controls.Add(yesButton);

                    noButton = new Button
                    {
                        Text = "No",
                        Location = new Point(210, buttonY),
                        Size = new Size(buttonWidth, buttonHeight),
                        DialogResult = DialogResult.No
                    };
                    noButton.Click += (s, e) => { Result = DialogResult.No; this.Close(); };
                    this.Controls.Add(noButton);
                    this.AcceptButton = yesButton;
                    this.CancelButton = noButton;
                    break;
            }
        }

        private void SetIcon(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Information:
                    iconPictureBox.Image = SystemIcons.Information.ToBitmap();
                    break;
                case MessageBoxIcon.Warning:
                    iconPictureBox.Image = SystemIcons.Warning.ToBitmap();
                    break;
                case MessageBoxIcon.Error:
                    iconPictureBox.Image = SystemIcons.Error.ToBitmap();
                    break;
                case MessageBoxIcon.Question:
                    iconPictureBox.Image = SystemIcons.Question.ToBitmap();
                    break;
                default:
                    iconPictureBox.Image = SystemIcons.Information.ToBitmap();
                    break;
            }
        }

        public static DialogResult Show(Form owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (CustomMessageBox msgBox = new CustomMessageBox(message, title, buttons, icon))
            {
                if (owner != null)
                {
                    // Position the dialog relative to the owner form
                    int x = owner.Left + (owner.Width - msgBox.Width) / 2;
                    int y = owner.Top + (owner.Height - msgBox.Height) / 2;
                    
                    // Ensure it stays within screen bounds
                    Screen screen = Screen.FromControl(owner);
                    if (x < screen.WorkingArea.Left) x = screen.WorkingArea.Left + 10;
                    if (y < screen.WorkingArea.Top) y = screen.WorkingArea.Top + 10;
                    if (x + msgBox.Width > screen.WorkingArea.Right) 
                        x = screen.WorkingArea.Right - msgBox.Width - 10;
                    if (y + msgBox.Height > screen.WorkingArea.Bottom) 
                        y = screen.WorkingArea.Bottom - msgBox.Height - 10;
                    
                    msgBox.Location = new Point(x, y);
                    msgBox.ShowDialog(owner);
                }
                else
                {
                    msgBox.StartPosition = FormStartPosition.CenterScreen;
                    msgBox.ShowDialog();
                }
                
                return msgBox.Result;
            }
        }
    }
}
