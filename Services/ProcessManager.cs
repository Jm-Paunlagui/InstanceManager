using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using InstanceManager.Models;
using InstanceManager.Utilities;

namespace InstanceManager.Services
{
    public class ProcessManager
    {
        public bool IsApplicationRunning(ManagedApplication app)
        {
            try
            {
                if (app == null || string.IsNullOrEmpty(app.Directory))
                    return false;

                string appName = Path.GetFileNameWithoutExtension(app.Directory);
                if (string.IsNullOrEmpty(appName))
                    return false;

                var processes = Process.GetProcessesByName(appName);
                bool hasWindowedProcess = false;

                foreach (var p in processes)
                {
                    try
                    {
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            hasWindowedProcess = true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process already exited
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }

                SimpleLogger.Debug("IsApplicationRunning @ ProcessManager.cs",
                    $"Checked {app.AppName}: ProcessCount={processes.Length}, HasWindow={hasWindowedProcess}");
                return hasWindowedProcess;
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("IsApplicationRunning @ ProcessManager.cs", $"Error checking {(app != null ? app.AppName : "null")}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if any process with the app's name exists (including background/zombie processes without a window).
        /// </summary>
        public bool HasBackgroundProcess(ManagedApplication app)
        {
            try
            {
                if (app == null || string.IsNullOrEmpty(app.Directory))
                    return false;

                string appName = Path.GetFileNameWithoutExtension(app.Directory);
                if (string.IsNullOrEmpty(appName))
                    return false;

                var processes = Process.GetProcessesByName(appName);
                bool hasBackground = false;

                foreach (var p in processes)
                {
                    try
                    {
                        if (p.MainWindowHandle == IntPtr.Zero)
                        {
                            hasBackground = true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process already exited
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }

                return hasBackground;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kills any background (windowless) processes matching the app's name.
        /// Returns the number of processes killed.
        /// </summary>
        public int KillBackgroundProcesses(ManagedApplication app)
        {
            int killed = 0;
            try
            {
                if (app == null || string.IsNullOrEmpty(app.Directory))
                    return 0;

                string appName = Path.GetFileNameWithoutExtension(app.Directory);
                if (string.IsNullOrEmpty(appName))
                    return 0;

                var processes = Process.GetProcessesByName(appName);

                foreach (var process in processes)
                {
                    try
                    {
                        if (process.MainWindowHandle == IntPtr.Zero)
                        {
                            int pid = process.Id;
                            process.Kill();
                            killed++;
                            SimpleLogger.Warn("KillBackgroundProcesses @ ProcessManager.cs",
                                $"Killed background process for {app.AppName} (PID: {pid})");
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process already exited
                    }
                    catch (System.ComponentModel.Win32Exception killEx)
                    {
                        SimpleLogger.Error("KillBackgroundProcesses @ ProcessManager.cs",
                            $"Access denied killing background process for {app.AppName}: {killEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Error("KillBackgroundProcesses @ ProcessManager.cs",
                            $"Error killing background process for {app.AppName}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("KillBackgroundProcesses @ ProcessManager.cs",
                    $"Error killing background processes for {(app != null ? app.AppName : "null")}: {ex.Message}");
            }
            return killed;
        }

        public bool StartApplication(ManagedApplication app)
        {
            try
            {
                if (app == null || string.IsNullOrEmpty(app.Directory))
                {
                    SimpleLogger.Error("StartApplication @ ProcessManager.cs", "Cannot start: app or directory is null");
                    return false;
                }

                if (IsApplicationRunning(app))
                {
                    SimpleLogger.Warn("StartApplication @ ProcessManager.cs", $"Cannot start {app.AppName}: Already running");
                    return false;
                }

                // Kill any lingering background processes before starting fresh
                int bgKilled = KillBackgroundProcesses(app);
                if (bgKilled > 0)
                {
                    SimpleLogger.Info("StartApplication @ ProcessManager.cs",
                        $"Cleaned up {bgKilled} background process(es) for {app.AppName} before starting");
                }

                if (!File.Exists(app.Directory))
                {
                    SimpleLogger.Error("StartApplication @ ProcessManager.cs", $"Cannot start {app.AppName}: File not found at {app.Directory}");
                    return false;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = app.Directory,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(app.Directory)
                };

                Process process = Process.Start(startInfo);
                
                if (process != null)
                {
                    try
                    {
                        int pid = process.Id;
                        SimpleLogger.Info("StartApplication @ ProcessManager.cs", $"Successfully started {app.AppName} (PID: {pid})");
                    }
                    catch (InvalidOperationException)
                    {
                        SimpleLogger.Info("StartApplication @ ProcessManager.cs", $"Successfully started {app.AppName} (PID unavailable - process may have exited quickly)");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                    return true;
                }

                SimpleLogger.Error("StartApplication @ ProcessManager.cs", $"Failed to start {app.AppName}");
                return false;
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StartApplication @ ProcessManager.cs", $"Error starting {(app != null ? app.AppName : "null")}: {ex.Message}");
                return false;
            }
        }

        public bool StopApplication(ManagedApplication app)
        {
            try
            {
                if (app == null || string.IsNullOrEmpty(app.Directory))
                {
                    SimpleLogger.Warn("StopApplication @ ProcessManager.cs", "Cannot stop: app or directory is null");
                    return false;
                }

                string appName = Path.GetFileNameWithoutExtension(app.Directory);
                if (string.IsNullOrEmpty(appName))
                {
                    SimpleLogger.Warn("StopApplication @ ProcessManager.cs", $"Cannot stop {app.AppName}: Unable to determine process name");
                    return false;
                }

                var processes = Process.GetProcessesByName(appName);

                if (processes.Length == 0)
                {
                    SimpleLogger.Warn("StopApplication @ ProcessManager.cs", $"Cannot stop {app.AppName}: Not running");
                    return false;
                }

                foreach (var process in processes)
                {
                    try
                    {
                        int pid = process.Id;
                        bool hasWindow = process.MainWindowHandle != IntPtr.Zero;

                        if (hasWindow)
                        {
                            // Try graceful shutdown first
                            process.CloseMainWindow();

                            if (!process.WaitForExit(3000))
                            {
                                try
                                {
                                    process.Kill();
                                    SimpleLogger.Warn("StopApplication @ ProcessManager.cs", $"Force killed {app.AppName} (PID: {pid}) - graceful shutdown timed out");
                                }
                                catch (System.ComponentModel.Win32Exception killEx)
                                {
                                    SimpleLogger.Error("StopApplication @ ProcessManager.cs", $"Access denied killing {app.AppName} (PID: {pid}): {killEx.Message}");
                                }
                            }
                            else
                            {
                                SimpleLogger.Info("StopApplication @ ProcessManager.cs", $"Gracefully stopped {app.AppName} (PID: {pid})");
                            }
                        }
                        else
                        {
                            // No main window — background/zombie process, force kill immediately
                            try
                            {
                                process.Kill();
                                SimpleLogger.Warn("StopApplication @ ProcessManager.cs", $"Force killed background process {app.AppName} (PID: {pid}) - no main window");
                            }
                            catch (System.ComponentModel.Win32Exception killEx)
                            {
                                SimpleLogger.Error("StopApplication @ ProcessManager.cs", $"Access denied killing background {app.AppName} (PID: {pid}): {killEx.Message}");
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        SimpleLogger.Info("StopApplication @ ProcessManager.cs", $"Process for {app.AppName} already exited during stop");
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Error("StopApplication @ ProcessManager.cs", $"Error stopping process: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StopApplication @ ProcessManager.cs", $"Error stopping {(app != null ? app.AppName : "null")}: {ex.Message}");
                return false;
            }
        }

        public int GetRunningInstanceCount(ManagedApplication app)
        {
            try
            {
                if (app == null || string.IsNullOrEmpty(app.Directory))
                    return 0;

                string appName = Path.GetFileNameWithoutExtension(app.Directory);
                if (string.IsNullOrEmpty(appName))
                    return 0;

                var processes = Process.GetProcessesByName(appName);
                int count = processes.Length;

                foreach (var p in processes)
                {
                    p.Dispose();
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }
    }
}
