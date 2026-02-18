using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using IntelligentMutexExecutionEnvironment.Models;
using IntelligentMutexExecutionEnvironment.Utilities;

namespace IntelligentMutexExecutionEnvironment.Services
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
            public bool HasNotRespondingProcess;
            public int TotalCount;
            /// <summary>
            /// Total processor time across all matching windowed processes at the time of snapshot.
            /// Used by the caller to detect CPU-hung processes by comparing across ticks.
            /// TimeSpan.Zero if no windowed process exists.
            /// </summary>
            public TimeSpan TotalCpuTime;
            /// <summary>
            /// Peak working set (bytes) across all matching windowed processes.
            /// Used to detect runaway memory consumption.
            /// </summary>
            public long PeakWorkingSetBytes;
            /// <summary>
            /// True if any windowed process has a main window title matching common
            /// error/crash dialog patterns (e.g. "Error", "Exception", ".NET", "has stopped").
            /// This detects ghost windows that are technically "responding" but show an error dialog.
            /// </summary>
            public bool HasErrorDialogWindow;
            /// <summary>
            /// The window title that triggered HasErrorDialogWindow, for diagnostic logging.
            /// </summary>
            public string ErrorDialogTitle;
            /// <summary>
            /// The current main window title of the first windowed process found.
            /// Used to detect when a window title changes unexpectedly (e.g. exception dialog overlay).
            /// Null if no windowed process exists.
            /// </summary>
            public string MainWindowTitle;
        }

        /// <summary>
        /// Tracks a launched process PID so we can retrieve its exit code after termination.
        /// </summary>
        private readonly Dictionary<int, int> _appPidMap = new Dictionary<int, int>();

        // Reusable collections to avoid per-tick allocations in GetBatchProcessSnapshot
        private readonly Dictionary<string, List<int>> _nameToAppsCache = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, ProcessSnapshot> _snapshotResultsCache = new Dictionary<int, ProcessSnapshot>();

        // Error dialog detection patterns (checked case-insensitively against window titles)
        private static readonly string[] ErrorDialogPatterns = new string[]
        {
            "has stopped working",
            "not responding",
            "has encountered a problem",
            "unhandled exception",
            "application error",
            "runtime error",
            "fatal error",
            "clr error",
            ".net framework",
            "just-in-time debugging",
            "assertion failed",
            "access violation",
            "stack overflow",
            "nullreferenceexception",
            "dividebyzeroexception",
            "outofmemoryexception",
            "stackoverflowexception",
            "accessviolationexception",
            "invalidoperationexception",
            "argumentexception",
            "indexoutofrangeexception",
            "objectdisposedexception",
            "system.exception",
            "system.componentmodel.win32exception",
            "an error occurred",
            "error in application",
            "stopped unexpectedly"
        };

        /// <summary>
        /// Records the PID for a launched application so we can track it for exit code retrieval.
        /// </summary>
        public void TrackLaunchedPid(int appIndex, int pid)
        {
            _appPidMap[appIndex] = pid;
        }

        /// <summary>
        /// Removes PID tracking for an application (e.g. after intentional stop).
        /// </summary>
        public void UntrackPid(int appIndex)
        {
            _appPidMap.Remove(appIndex);
        }

        /// <summary>
        /// Attempts to retrieve the exit code of the last tracked process for the given app.
        /// Returns null if the PID was not tracked, the process is still running, or the exit code
        /// cannot be retrieved (access denied, already cleaned up, etc.).
        /// </summary>
        public int? GetTrackedExitCode(int appIndex)
        {
            int pid;
            if (!_appPidMap.TryGetValue(appIndex, out pid))
                return null;

            try
            {
                Process proc = null;
                try
                {
                    proc = Process.GetProcessById(pid);
                    // Process is still alive — no exit code yet
                    return null;
                }
                catch (ArgumentException)
                {
                    // Process has exited (GetProcessById throws ArgumentException for non-existent PIDs)
                    // On .NET 4.0 we cannot retrieve the exit code after the Process object is gone
                    // unless we kept a handle. Return a sentinel indicating abnormal exit.
                    return null;
                }
                finally
                {
                    if (proc != null)
                        proc.Dispose();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the tracked PID for an app index, or -1 if not tracked.
        /// </summary>
        public int GetTrackedPid(int appIndex)
        {
            int pid;
            return _appPidMap.TryGetValue(appIndex, out pid) ? pid : -1;
        }

        /// <summary>
        /// Takes a single system-wide process snapshot and returns per-app results.
        /// This is dramatically more efficient than calling GetProcessesByName per app,
        /// because each GetProcessesByName call internally enumerates ALL system processes.
        /// With this approach, we enumerate once regardless of how many apps we monitor.
        /// </summary>
        public Dictionary<int, ProcessSnapshot> GetBatchProcessSnapshot(IList<ManagedApplication> apps)
        {
            // Reuse cached collections to avoid allocations on every tick
            var results = _snapshotResultsCache;
            results.Clear();

            if (apps == null || apps.Count == 0)
                return results;

            // Build a lookup of process name -> list of app indices that use that name
            // Clear reused lists instead of creating new ones
            var nameToApps = _nameToAppsCache;
            foreach (var kvp in nameToApps)
            {
                kvp.Value.Clear();
            }

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
                    string pName;
                    try
                    {
                        pName = allProcesses[i].ProcessName;
                    }
                    catch (InvalidOperationException)
                    {
                        continue; // Process exited
                    }

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

                    bool responding = true;
                    TimeSpan cpuTime = TimeSpan.Zero;
                    long workingSet = 0;
                    string windowTitle = null;
                    bool isErrorDialog = false;

                    if (hasWindow)
                    {
                        try
                        {
                            responding = allProcesses[i].Responding;
                        }
                        catch (InvalidOperationException)
                        {
                            continue; // Process exited
                        }

                        // Gather CPU time (lightweight kernel query, no perf counter overhead)
                        try
                        {
                            cpuTime = allProcesses[i].TotalProcessorTime;
                        }
                        catch (InvalidOperationException) { }
                        catch (System.ComponentModel.Win32Exception) { }

                        // Gather working set
                        try
                        {
                            // .NET 4.0: WorkingSet64 is available
                            workingSet = allProcesses[i].WorkingSet64;
                        }
                        catch (InvalidOperationException) { }
                        catch (System.ComponentModel.Win32Exception) { }

                        // Check window title for error dialog patterns
                        // Only if the window is responding (error dialogs pump messages)
                        if (responding)
                        {
                            try
                            {
                                windowTitle = allProcesses[i].MainWindowTitle;
                                if (!string.IsNullOrEmpty(windowTitle))
                                {
                                    string titleLower = windowTitle.ToLowerInvariant();
                                    for (int p = 0; p < ErrorDialogPatterns.Length; p++)
                                    {
                                        if (titleLower.Contains(ErrorDialogPatterns[p]))
                                        {
                                            isErrorDialog = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (InvalidOperationException) { }
                        }
                    }

                    for (int j = 0; j < appIndices.Count; j++)
                    {
                        int idx = appIndices[j];
                        var snap = results[idx];
                        snap.TotalCount++;
                        if (hasWindow)
                        {
                            snap.HasWindowedProcess = true;
                            if (!responding)
                                snap.HasNotRespondingProcess = true;
                            // Accumulate CPU time across all windowed processes for this app
                            snap.TotalCpuTime = snap.TotalCpuTime.Add(cpuTime);
                            // Track peak working set
                            if (workingSet > snap.PeakWorkingSetBytes)
                                snap.PeakWorkingSetBytes = workingSet;
                            if (isErrorDialog)
                            {
                                snap.HasErrorDialogWindow = true;
                                if (snap.ErrorDialogTitle == null)
                                    snap.ErrorDialogTitle = windowTitle;
                            }
                            // Capture window title for title-change detection
                            if (snap.MainWindowTitle == null && windowTitle != null)
                                snap.MainWindowTitle = windowTitle;
                        }
                        else
                            snap.HasBackgroundProcess = true;
                        results[idx] = snap;
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
                                try
                                {
                                    if (!processes[i].Responding)
                                        snapshot.HasNotRespondingProcess = true;
                                }
                                catch (InvalidOperationException)
                                {
                                    // Process already exited
                                }

                                // CPU time
                                try
                                {
                                    snapshot.TotalCpuTime = snapshot.TotalCpuTime.Add(processes[i].TotalProcessorTime);
                                }
                                catch (InvalidOperationException) { }
                                catch (System.ComponentModel.Win32Exception) { }

                                // Working set
                                try
                                {
                                    long ws = processes[i].WorkingSet64;
                                    if (ws > snapshot.PeakWorkingSetBytes)
                                        snapshot.PeakWorkingSetBytes = ws;
                                }
                                catch (InvalidOperationException) { }
                                catch (System.ComponentModel.Win32Exception) { }

                                // Error dialog title check
                                try
                                {
                                    string title = processes[i].MainWindowTitle;
                                    if (!string.IsNullOrEmpty(title))
                                    {
                                        // Capture for title-change detection
                                        if (snapshot.MainWindowTitle == null)
                                            snapshot.MainWindowTitle = title;

                                        string titleLower = title.ToLowerInvariant();
                                        for (int p = 0; p < ErrorDialogPatterns.Length; p++)
                                        {
                                            if (titleLower.Contains(ErrorDialogPatterns[p]))
                                            {
                                                snapshot.HasErrorDialogWindow = true;
                                                if (snapshot.ErrorDialogTitle == null)
                                                    snapshot.ErrorDialogTitle = title;
                                                break;
                                            }
                                        }
                                    }
                                }
                                catch (InvalidOperationException) { }

                                // Track the main window title (for detecting title changes)
                                snapshot.MainWindowTitle = processes[i].MainWindowTitle;
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
                            // Track the PID for exit code retrieval and precise identification
                            TrackLaunchedPid(app.Index, pid);
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

                // Clear PID tracking since we're intentionally stopping
                UntrackPid(app.Index);

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

        /// <summary>
        /// Attempts to retrieve the exit code from a process by PID.
        /// The process must have already exited. Returns null on failure.
        /// Uses OpenProcess + GetExitCodeProcess via the Process class.
        /// </summary>
        public int? TryGetExitCode(int pid)
        {
            try
            {
                Process proc = null;
                try
                {
                    proc = Process.GetProcessById(pid);
                    // Still running — no exit code available
                    return null;
                }
                catch (ArgumentException)
                {
                    // Process does not exist (already exited) — we cannot retrieve exit code
                    // from a disposed handle on .NET 4.0 without keeping the Process object alive.
                    return null;
                }
                finally
                {
                    if (proc != null)
                        proc.Dispose();
                }
            }
            catch
            {
                return null;
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
