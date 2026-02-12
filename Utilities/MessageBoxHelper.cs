using System;
using System.Drawing;
using System.Windows.Forms;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    public static class MessageBoxHelper
    {
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Form ownerForm = owner as Form;
            if (ownerForm != null)
            {
                return CustomMessageBox.Show(ownerForm, text, caption, buttons, icon);
            }
            return MessageBox.Show(owner, text, caption, buttons, icon);
        }

        public static DialogResult ShowSuccess(IWin32Window owner, string message)
        {
            Form ownerForm = owner as Form;
            if (ownerForm != null)
            {
                return CustomMessageBox.Show(ownerForm, message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return MessageBox.Show(owner, message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static DialogResult ShowError(IWin32Window owner, string message)
        {
            Form ownerForm = owner as Form;
            if (ownerForm != null)
            {
                return CustomMessageBox.Show(ownerForm, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return MessageBox.Show(owner, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static DialogResult ShowWarning(IWin32Window owner, string message)
        {
            Form ownerForm = owner as Form;
            if (ownerForm != null)
            {
                return CustomMessageBox.Show(ownerForm, message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return MessageBox.Show(owner, message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static DialogResult ShowInfo(IWin32Window owner, string message)
        {
            Form ownerForm = owner as Form;
            if (ownerForm != null)
            {
                return CustomMessageBox.Show(ownerForm, message, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return MessageBox.Show(owner, message, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static DialogResult ShowQuestion(IWin32Window owner, string message, string caption)
        {
            Form ownerForm = owner as Form;
            if (ownerForm != null)
            {
                return CustomMessageBox.Show(ownerForm, message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            }
            return MessageBox.Show(owner, message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }
    }
}
