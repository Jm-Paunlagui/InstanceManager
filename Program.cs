using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using InstanceManager.Utilities;

namespace InstanceManager
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                SimpleLogger.Info("Main @ Program.cs", "InstanceManager application starting");

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                SimpleLogger.Info("Main @ Program.cs", "Running main form");
                Application.Run(new Main());
                
                SimpleLogger.Info("Main @ Program.cs", "InstanceManager application terminated");
            }
            catch (Exception ex)
            {
                SimpleLogger.Fatal("Main @ Program.cs", $"Critical error: {ex.Message}");
                MessageBox.Show($"A critical error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
