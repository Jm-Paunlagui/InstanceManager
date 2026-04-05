using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using IntelligentMutexExecutionEnvironment.Services;
using IntelligentMutexExecutionEnvironment.Utilities;

namespace IntelligentMutexExecutionEnvironment
{
    internal static class Program
    {
        private static Mutex _mutex;

        /// <summary>
        /// Custom registered Windows message used to signal the first instance
        /// to restore its window when a second instance is launched.
        /// </summary>
        public static readonly int WM_SHOWFIRSTINSTANCE =
            NativeMethods.RegisterWindowMessage("WM_SHOWFIRSTINSTANCE_IMEE_{B5F0E7A2-4C3D-4F8E-9A1B-2D6E8F3C7A50}");

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
                // Another instance is already running — signal it to restore its window
                NativeMethods.PostMessage(
                    (IntPtr)NativeMethods.HWND_BROADCAST,
                    WM_SHOWFIRSTINSTANCE,
                    IntPtr.Zero,
                    IntPtr.Zero);
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

                // Initialize DPI awareness and read the user font size preference
                DpiScaler.Initialize();

                // Read font size setting before creating any forms
                var tempSettings = new SettingsService();
                DpiScaler.SetFontSizePercent(tempSettings.FontSizePercent);

                SimpleLogger.Info("Main @ Program.cs",
                    $"DPI={DpiScaler.SystemDpi}, FontSize={tempSettings.FontSizePercent}%, ScaleFactor={DpiScaler.ScaleFactor:F2}");

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

    /// <summary>
    /// Native Win32 methods for single-instance window activation.
    /// </summary>
    internal static class NativeMethods
    {
        public const int HWND_BROADCAST = 0xFFFF;
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);
    }
}
