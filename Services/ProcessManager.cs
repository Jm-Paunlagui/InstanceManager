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
                string appName = Path.GetFileNameWithoutExtension(app.Directory);
                var processes = Process.GetProcessesByName(appName);
                bool isRunning = processes.Length > 0;

                foreach (var p in processes)
                {
                    p.Dispose();
                }

                SimpleLogger.Debug("IsApplicationRunning @ ProcessManager.cs", $"Checked {app.AppName}: {(isRunning ? "Running" : "Not Running")}");
                return isRunning;
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("IsApplicationRunning @ ProcessManager.cs", $"Error checking {app.AppName}: {ex.Message}");
                return false;
            }
        }

        public bool StartApplication(ManagedApplication app)
        {
            try
            {
                if (IsApplicationRunning(app))
                {
                    SimpleLogger.Warn("StartApplication @ ProcessManager.cs", $"Cannot start {app.AppName}: Already running");
                    return false;
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
                SimpleLogger.Error("StartApplication @ ProcessManager.cs", $"Error starting {app.AppName}: {ex.Message}");
                return false;
            }
        }

        public bool StopApplication(ManagedApplication app)
        {
            try
            {
                string appName = Path.GetFileNameWithoutExtension(app.Directory);
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
                        process.CloseMainWindow();
                        
                        if (!process.WaitForExit(3000))
                        {
                            process.Kill();
                            SimpleLogger.Warn("StopApplication @ ProcessManager.cs", $"Force killed {app.AppName} (PID: {pid})");
                        }
                        else
                        {
                            SimpleLogger.Info("StopApplication @ ProcessManager.cs", $"Gracefully stopped {app.AppName} (PID: {pid})");
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
                SimpleLogger.Error("StopApplication @ ProcessManager.cs", $"Error stopping {app.AppName}: {ex.Message}");
                return false;
            }
        }

        public int GetRunningInstanceCount(ManagedApplication app)
        {
            try
            {
                string appName = Path.GetFileNameWithoutExtension(app.Directory);
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
