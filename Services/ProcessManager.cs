using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using InstanceManager.Models;
using InstanceManager.Utilities;

namespace InstanceManager.Services
{
    public class ProcessManager
    {
        /// <summary>
        /// Holds the result of a single GetProcessesByName snapshot to avoid
        /// calling it multiple times per app per timer tick.
        /// </summary>
        public struct ProcessSnapshot
        {
            public bool HasWindowedProcess;
            public bool HasBackgroundProcess;
            public int TotalCount;
        }

        /// <summary>
        /// Takes a single system-wide process snapshot and returns per-app results.
        /// This is dramatically more efficient than calling GetProcessesByName per app,
        /// because each GetProcessesByName call internally enumerates ALL system processes.
        /// With this approach, we enumerate once regardless of how many apps we monitor.
        /// </summary>
        public Dictionary<int, ProcessSnapshot> GetBatchProcessSnapshot(IList<ManagedApplication> apps)
        {
            var results = new Dictionary<int, ProcessSnapshot>();

            if (apps == null || apps.Count == 0)
                return results;

            // Build a lookup of process name -> list of app indices that use that name
            // (multiple apps could theoretically have the same exe name)
            var nameToApps = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < apps.Count; i++)
            {
                var app = apps[i];
                if (string.IsNullOrEmpty(app.Directory))
                    continue;

                string procName = Path.GetFileNameWithoutExtension(app.Directory);
                if (string.IsNullOrEmpty(procName))
                    continue;

                List<int> indices;
                if (!nameToApps.TryGetValue(procName, out indices))
                {
                    indices = new List<int>();
                    nameToApps[procName] = indices;
                }
                indices.Add(app.Index);

                // Initialize empty snapshot for each app
                results[app.Index] = new ProcessSnapshot();
            }

            if (nameToApps.Count == 0)
                return results;

            Process[] allProcesses = null;
            try
            {
                allProcesses = Process.GetProcesses();

                for (int i = 0; i < allProcesses.Length; i++)
                {
                    try
                    {
                        string pName = allProcesses[i].ProcessName;

                        List<int> appIndices;
                        if (!nameToApps.TryGetValue(pName, out appIndices))
                            continue;

                        bool hasWindow;
                        try
                        {
                            hasWindow = allProcesses[i].MainWindowHandle != IntPtr.Zero;
                        }
                        catch (InvalidOperationException)
                        {
                            continue; // Process exited
                        }

                        for (int j = 0; j < appIndices.Count; j++)
                        {
                            int idx = appIndices[j];
                            var snap = results[idx];
                            snap.TotalCount++;
                            if (hasWindow)
                                snap.HasWindowedProcess = true;
                            else
                                snap.HasBackgroundProcess = true;
                            results[idx] = snap;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process exited between enumeration and property access
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("GetBatchProcessSnapshot @ ProcessManager.cs",
                    $"Error during batch snapshot: {ex.Message}");
            }
            finally
            {
                if (allProcesses != null)
                {
                    for (int i = 0; i < allProcesses.Length; i++)
                    {
                        allProcesses[i].Dispose();
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Takes a single process snapshot for the given app, checking both windowed and background processes.
        /// Callers should use this instead of calling IsApplicationRunning + HasBackgroundProcess separately.
        /// </summary>
        public ProcessSnapshot GetProcessSnapshot(ManagedApplication app)
        {
            var snapshot = new ProcessSnapshot();
            try
            {
                if (app == null || string.IsNullOrEmpty(app.Directory))
                    return snapshot;

                string appName = Path.GetFileNameWithoutExtension(app.Directory);
                if (string.IsNullOrEmpty(appName))
                    return snapshot;

                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(appName);
                    snapshot.TotalCount = processes.Length;

                    for (int i = 0; i < processes.Length; i++)
                    {
                        try
                        {
                            if (processes[i].MainWindowHandle != IntPtr.Zero)
                            {
                                snapshot.HasWindowedProcess = true;
                            }
                            else
                            {
                                snapshot.HasBackgroundProcess = true;
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            // Process already exited
                        }
                    }
                }
                finally
                {
                    if (processes != null)
                    {
                        for (int i = 0; i < processes.Length; i++)
                        {
                            processes[i].Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("GetProcessSnapshot @ ProcessManager.cs",
                    $"Error getting snapshot for {(app != null ? app.AppName : "null")}: {ex.Message}");
            }
            return snapshot;
        }

        public bool IsApplicationRunning(ManagedApplication app)
        {
            var snapshot = GetProcessSnapshot(app);
            return snapshot.HasWindowedProcess;
        }

        /// <summary>
        /// Checks if any process with the app's name exists (including background/zombie processes without a window).
        /// </summary>
        public bool HasBackgroundProcess(ManagedApplication app)
        {
            var snapshot = GetProcessSnapshot(app);
            return snapshot.HasBackgroundProcess;
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

                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(appName);

                    for (int i = 0; i < processes.Length; i++)
                    {
                        try
                        {
                            if (processes[i].MainWindowHandle == IntPtr.Zero)
                            {
                                int pid = processes[i].Id;
                                processes[i].Kill();
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
                    }
                }
                finally
                {
                    if (processes != null)
                    {
                        for (int i = 0; i < processes.Length; i++)
                        {
                            processes[i].Dispose();
                        }
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

                Process process = null;
                try
                {
                    process = Process.Start(startInfo);

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
                        return true;
                    }
                }
                finally
                {
                    if (process != null)
                    {
                        process.Dispose();
                    }
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

                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(appName);

                    if (processes.Length == 0)
                    {
                        SimpleLogger.Warn("StopApplication @ ProcessManager.cs", $"Cannot stop {app.AppName}: Not running");
                        return false;
                    }

                    for (int i = 0; i < processes.Length; i++)
                    {
                        try
                        {
                            int pid = processes[i].Id;
                            bool hasWindow = processes[i].MainWindowHandle != IntPtr.Zero;

                            if (hasWindow)
                            {
                                processes[i].CloseMainWindow();

                                if (!processes[i].WaitForExit(3000))
                                {
                                    try
                                    {
                                        processes[i].Kill();
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
                                try
                                {
                                    processes[i].Kill();
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
                    }
                }
                finally
                {
                    if (processes != null)
                    {
                        for (int i = 0; i < processes.Length; i++)
                        {
                            processes[i].Dispose();
                        }
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

                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(appName);
                    return processes.Length;
                }
                finally
                {
                    if (processes != null)
                    {
                        for (int i = 0; i < processes.Length; i++)
                        {
                            processes[i].Dispose();
                        }
                    }
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}
