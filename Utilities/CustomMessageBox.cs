using System;
using System.Drawing;
using System.Windows.Forms;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    public class CustomMessageBox : Form
    {
        private Label messageLabel;
        private Button okButton;
        private Button yesButton;
        private Button noButton;
        private PictureBox iconPictureBox;
        private Font _labelFont;

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
            this.BackColor = Color.White;

            int formWidth = 420;
            int labelX = 65;
            int labelWidth = formWidth - labelX - 25;

            iconPictureBox = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(32, 32),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            SetIcon(icon);
            this.Controls.Add(iconPictureBox);

            // Measure the text to determine required label height
            _labelFont = new Font("AUMOVIO Screen", 9F);
            Size proposedSize = new Size(labelWidth, int.MaxValue);
            Size measuredSize = TextRenderer.MeasureText(message, _labelFont, proposedSize, TextFormatFlags.WordBreak);
            int labelHeight = Math.Max(40, measuredSize.Height + 5);

            messageLabel = new Label
            {
                Text = message,
                Location = new Point(labelX, 20),
                AutoSize = false,
                Size = new Size(labelWidth, labelHeight),
                Font = _labelFont
            };
            this.Controls.Add(messageLabel);

            int buttonY = messageLabel.Bottom + 15;
            int buttonWidth = 80;
            int buttonHeight = 28;
            int formHeight = buttonY + buttonHeight + 45;

            this.Size = new Size(formWidth, formHeight);

            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    okButton = new Button
                    {
                        Text = "OK",
                        Location = new Point((formWidth - buttonWidth) / 2, buttonY),
                        Size = new Size(buttonWidth, buttonHeight),
                        DialogResult = DialogResult.OK
                    };
                    okButton.Click += (s, e) => { Result = DialogResult.OK; this.Close(); };
                    this.Controls.Add(okButton);
                    this.AcceptButton = okButton;
                    break;

                case MessageBoxButtons.YesNo:
                    int totalButtonsWidth = buttonWidth * 2 + 10;
                    int startX = (formWidth - totalButtonsWidth) / 2;

                    yesButton = new Button
                    {
                        Text = "Yes",
                        Location = new Point(startX, buttonY),
                        Size = new Size(buttonWidth, buttonHeight),
                        DialogResult = DialogResult.Yes
                    };
                    yesButton.Click += (s, e) => { Result = DialogResult.Yes; this.Close(); };
                    this.Controls.Add(yesButton);

                    noButton = new Button
                    {
                        Text = "No",
                        Location = new Point(startX + buttonWidth + 10, buttonY),
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
            Icon systemIcon;
            switch (icon)
            {
                case MessageBoxIcon.Warning:
                    systemIcon = SystemIcons.Warning;
                    break;
                case MessageBoxIcon.Error:
                    systemIcon = SystemIcons.Error;
                    break;
                case MessageBoxIcon.Question:
                    systemIcon = SystemIcons.Question;
                    break;
                default:
                    systemIcon = SystemIcons.Information;
                    break;
            }
            // ToBitmap() creates a new Bitmap that the PictureBox will own.
            // It will be disposed when the PictureBox is disposed via the form.
            iconPictureBox.Image = systemIcon.ToBitmap();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose the bitmap created by ToBitmap() to prevent GDI handle leak
                if (iconPictureBox != null && iconPictureBox.Image != null)
                {
                    iconPictureBox.Image.Dispose();
                    iconPictureBox.Image = null;
                }
                if (_labelFont != null)
                {
                    _labelFont.Dispose();
                    _labelFont = null;
                }
            }
            base.Dispose(disposing);
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
                    if (y < screen.WorkingArea.Top) x = screen.WorkingArea.Top + 10;
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
