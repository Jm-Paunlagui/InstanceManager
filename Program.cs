using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using IntelligentMutexExecutionEnvironment.Utilities;

namespace IntelligentMutexExecutionEnvironment
{
    internal static class Program
    {
        private static Mutex _mutex;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Ensure only one instance of IMEE is running
            bool createdNew;
            _mutex = new Mutex(true, "Global\\IntelligentMutexExecutionEnvironment_SingleInstance_Mutex", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Intelligent Mutex Execution Environment is already running.",
                    "Intelligent Mutex Execution Environment",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Catch unhandled UI thread exceptions
                Application.ThreadException += Application_ThreadException;
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                // Catch unhandled non-UI thread exceptions
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                SimpleLogger.Info("Main @ Program.cs", "IntelligentMutexExecutionEnvironment application starting");

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                SimpleLogger.Info("Main @ Program.cs", "Running main form");
                Application.Run(new Main());
                
                SimpleLogger.Info("Main @ Program.cs", "IntelligentMutexExecutionEnvironment application terminated");
            }
            catch (Exception ex)
            {
                SimpleLogger.Fatal("Main @ Program.cs", $"Critical error: {ex.Message}");
                MessageBox.Show($"A critical error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SimpleLogger.Flush();
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            SimpleLogger.Fatal("Application_ThreadException @ Program.cs",
                $"Unhandled UI thread exception: {e.Exception.Message}\nStackTrace: {e.Exception.StackTrace}");
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will attempt to continue.",
                "Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            string message = ex != null ? ex.Message : "Unknown error";
            string stackTrace = ex != null ? ex.StackTrace : "";

            SimpleLogger.Fatal("CurrentDomain_UnhandledException @ Program.cs",
                $"Unhandled domain exception (IsTerminating={e.IsTerminating}): {message}\nStackTrace: {stackTrace}");

            MessageBox.Show(
                $"A fatal error occurred:\n\n{message}\n\nThe application will close.",
                "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
