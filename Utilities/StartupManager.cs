using System;
using IntelligentMutexExecutionEnvironment.Utilities;
using Microsoft.Win32;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    /// <summary>
    /// Manages the Windows Registry "Run" key to enable or disable running IMEE on system startup.
    /// Uses HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run so no admin rights are needed.
    /// </summary>
    public static class StartupManager
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "IntelligentMutexExecutionEnvironment";

        /// <summary>
        /// Enables or disables the application to run on Windows startup.
        /// </summary>
        public static void SetRunOnStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                    {
                        SimpleLogger.Error("SetRunOnStartup @ StartupManager.cs",
                            "Failed to open registry Run key");
                        return;
                    }

                    if (enable)
                    {
                        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue(AppRegistryName, "\"" + exePath + "\"");
                        SimpleLogger.Info("SetRunOnStartup @ StartupManager.cs",
                            $"Enabled run on startup: {exePath}");
                    }
                    else
                    {
                        key.DeleteValue(AppRegistryName, false);
                        SimpleLogger.Info("SetRunOnStartup @ StartupManager.cs",
                            "Disabled run on startup");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("SetRunOnStartup @ StartupManager.cs",
                    $"Error setting run on startup: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks whether the application is currently configured to run on Windows startup.
        /// </summary>
        public static bool IsRunOnStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                        return false;

                    object value = key.GetValue(AppRegistryName);
                    return value != null;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("IsRunOnStartupEnabled @ StartupManager.cs",
                    $"Error checking run on startup: {ex.Message}");
                return false;
            }
        }
    }
}
