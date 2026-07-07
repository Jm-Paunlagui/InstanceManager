using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using IntelligentMutexExecutionEnvironment.Models;
using IntelligentMutexExecutionEnvironment.Utilities;

namespace IntelligentMutexExecutionEnvironment.Services
{
    public class ProcessManager
    {
        // Win32 API imports for detecting hidden/tray windows
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // Win32 API imports for cross-bitness process path retrieval (Patch 0)
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(
            IntPtr hProcess, uint flags, StringBuilder exeName, ref uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr h);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        private readonly SettingsService _settingsService;

        public ProcessManager(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>Full image path of a PID, or null if unavailable. Never throws.</summary>
        public static string TryGetProcessPath(int pid)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (h == IntPtr.Zero) return null;
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
            }
            catch { return null; }
            finally { if (h != IntPtr.Zero) CloseHandle(h); }
        }

        /// <summary>True only when the PID's image path provably matches expectedPath.
        /// Unknown path => NOT a match (fail-safe: never kill what you can't identify).</summary>
        public static bool ProcessPathMatches(int pid, string expectedPath)
        {
            string actual = TryGetProcessPath(pid);
            if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(expectedPath))
                return false;
            return string.Equals(
                Path.GetFullPath(actual).TrimEnd('\\'),
                Path.GetFullPath(expectedPath).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }

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
            /// <summary>
            /// The PID of the first windowed (or tray) process found for this app.
            /// 0 if no windowed process exists. Used for logging the actual application PID,
            /// especially when a launcher script was used to start the app.
            /// </summary>
            public int FirstWindowedPid;
        }

        /// <summary>
        /// Tracks a launched process PID so we can retrieve its exit code after termination.
        /// </summary>
        private readonly Dictionary<int, int> _appPidMap = new Dictionary<int, int>();

        /// <summary>
        /// Keeps the Process handle alive so we can read ExitCode after the process terminates.
        /// On .NET Framework 4.0, the exit code is only available while the Process handle is open.
        /// Key: app Index, Value: the Process object from Process.Start().
        /// </summary>
        private readonly Dictionary<int, Process> _appProcessHandles = new Dictionary<int, Process>();

        /// <summary>
        /// Reusable collections to avoid per-tick allocations in GetBatchProcessSnapshot
        /// </summary>
        private readonly Dictionary<string, List<int>> _nameToAppsCache = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, ProcessSnapshot> _snapshotResultsCache = new Dictionary<int, ProcessSnapshot>();
        /// <summary>
        /// Reusable lookup from app Index to app.Directory for path-based filtering in batch snapshot (Patch 2).
        /// </summary>
        private readonly Dictionary<int, string> _appIndexToDirectoryCache = new Dictionary<int, string>();

        /// <summary>
        /// Reusable HashSet for batched EnumWindows ~ stores PIDs that own at least one top-level window.
        /// Avoids per-tick allocation in GetBatchProcessSnapshot.
        /// </summary>
        private readonly HashSet<uint> _pidsWithWindowsCache = new HashSet<uint>();

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
        /// Records the Process handle for a launched application so we can read its ExitCode later.
        /// The Process object is kept alive (not disposed) until explicitly untracked or replaced.
        /// </summary>
        public void TrackLaunchedProcess(int appIndex, Process process)
        {
            // Dispose any previously tracked handle for this app
            Process oldProc;
            if (_appProcessHandles.TryGetValue(appIndex, out oldProc))
            {
                try { oldProc.Dispose(); } catch { }
            }
            _appProcessHandles[appIndex] = process;
        }

        /// <summary>
        /// Removes PID tracking for an application (e.g. after intentional stop).
        /// Also disposes the kept-alive Process handle.
        /// </summary>
        public void UntrackPid(int appIndex)
        {
            _appPidMap.Remove(appIndex);

            Process proc;
            if (_appProcessHandles.TryGetValue(appIndex, out proc))
            {
                _appProcessHandles.Remove(appIndex);
                try { proc.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Attempts to retrieve the exit code of the last tracked process for the given app.
        /// Returns null if the PID was not tracked, the process is still running, or the exit code
        /// cannot be retrieved (access denied, already cleaned up, etc.).
        /// </summary>
        public int? GetTrackedExitCode(int appIndex)
        {
            // First try the kept-alive Process handle (reliable on .NET 4.0)
            Process proc;
            if (_appProcessHandles.TryGetValue(appIndex, out proc))
            {
                try
                {
                    if (proc.HasExited)
                    {
                        return proc.ExitCode;
                    }

                    // Process may have just exited ~ wait briefly for it to register
                    try
                    {
                        if (proc.WaitForExit(500))
                        {
                            return proc.ExitCode;
                        }
                    }
                    catch { }

                    // Still running ~ no exit code yet
                    return null;
                }
                catch (InvalidOperationException)
                {
                    // Handle was invalidated
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Access denied
                }
                catch (Exception)
                {
                    // Unexpected error
                }
            }

            // Fallback to PID-based lookup
            int pid;
            if (!_appPidMap.TryGetValue(appIndex, out pid))
                return null;

            // Try to open the process by PID ~ if it still exists, no exit code yet.
            // If it no longer exists, we can't retrieve the exit code without the original handle.
            try
            {
                Process procById = null;
                try
                {
                    procById = Process.GetProcessById(pid);
                    // Process is still alive ~ no exit code yet
                    return null;
                }
                catch (ArgumentException)
                {
                    // Process has exited ~ cannot retrieve exit code without the original handle
                    return null;
                }
                finally
                {
                    if (procById != null)
                        procById.Dispose();
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
        /// Returns true if a Process handle is currently tracked for the given app index.
        /// This indicates we kept the Process object alive (used for ExitCode retrieval).
        /// </summary>
        public bool HasTrackedHandle(int appIndex)
        {
            return _appProcessHandles.ContainsKey(appIndex);
        }

        /// <summary>
        /// Checks whether a process owns any top-level window (visible or hidden).
        /// Apps minimized to the system tray close their main window but still own
        /// hidden top-level windows. Process.MainWindowHandle returns IntPtr.Zero
        /// in that case, but EnumWindows will find the hidden windows.
        /// </summary>
        private static bool HasAnyTopLevelWindow(int processId)
        {
            bool found = false;
            uint targetPid = (uint)processId;

            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                uint windowPid;
                GetWindowThreadProcessId(hWnd, out windowPid);
                if (windowPid == targetPid)
                {
                    found = true;
                    return false; // stop enumerating
                }
                return true; // continue
            }, IntPtr.Zero);

            return found;
        }

        /// <summary>
        /// Performs a single EnumWindows call to build a set of all PIDs that own
        /// at least one top-level window. This replaces N individual HasAnyTopLevelWindow
        /// calls (one per windowless process) with a single system-wide enumeration.
        /// </summary>
        private void BuildPidsWithWindows(HashSet<uint> result)
        {
            result.Clear();
            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                uint windowPid;
                GetWindowThreadProcessId(hWnd, out windowPid);
                if (windowPid != 0)
                {
                    result.Add(windowPid);
                }
                return true; // continue enumerating all windows
            }, IntPtr.Zero);
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

            // Build index -> directory lookup for path-based filtering (Patch 2)
            var indexToDir = _appIndexToDirectoryCache;
            indexToDir.Clear();

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
                indexToDir[app.Index] = app.Directory;

                // Initialize empty snapshot for each app
                results[app.Index] = new ProcessSnapshot();
            }

            if (nameToApps.Count == 0)
                return results;

            // Single EnumWindows call to build a set of all PIDs that own top-level windows.
            // This replaces N individual HasAnyTopLevelWindow calls for windowless processes.
            var pidsWithWindows = _pidsWithWindowsCache;
            BuildPidsWithWindows(pidsWithWindows);

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

                    // If no visible main window, check the pre-built PID set for hidden top-level
                    // windows (system tray apps). This is an O(1) HashSet lookup instead of a
                    // per-process EnumWindows call.
                    bool isTrayApp = false;
                    if (!hasWindow)
                    {
                        try
                        {
                            isTrayApp = pidsWithWindows.Contains((uint)allProcesses[i].Id);
                        }
                        catch (InvalidOperationException)
                        {
                            continue; // Process exited
                        }
                    }

                    bool responding = true;
                    TimeSpan cpuTime = TimeSpan.Zero;
                    long workingSet = 0;
                    string windowTitle = null;
                    bool isErrorDialog = false;

                    if (hasWindow || isTrayApp)
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

                    // PATH FILTER (Patch 2): resolve the process path once per process,
                    // then check it per-app in the inner loop. Detection is permissive:
                    // unknown path (null) counts as a match so IMEE doesn't go blind to
                    // its own app when access is denied. Only the KILL side is strict (Patch 1).
                    int processPid;
                    try { processPid = allProcesses[i].Id; }
                    catch (InvalidOperationException) { continue; }
                    string processPath = TryGetProcessPath(processPid);

                    for (int j = 0; j < appIndices.Count; j++)
                    {
                        int idx = appIndices[j];

                        // Path filter: if we know the process path AND it doesn't match this app's
                        // directory, skip it — this process belongs to a different install/line.
                        string appDir;
                        if (processPath != null && indexToDir.TryGetValue(idx, out appDir))
                        {
                            if (!string.Equals(
                                Path.GetFullPath(processPath).TrimEnd('\\'),
                                Path.GetFullPath(appDir).TrimEnd('\\'),
                                StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }

                        var snap = results[idx];
                        snap.TotalCount++;
                        if (hasWindow || isTrayApp)
                        {
                            snap.HasWindowedProcess = true;
                            if (snap.FirstWindowedPid == 0)
                            {
                                snap.FirstWindowedPid = processPid;
                            }
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
                            int pid;
                            try { pid = processes[i].Id; } catch (InvalidOperationException) { continue; }

                            // PATH FILTER (Patch 2): a same-name process from a different install path
                            // is NOT this app. Detection is permissive: unknown path counts as a match.
                            string path = TryGetProcessPath(pid);
                            if (path != null && !ProcessPathMatches(pid, app.Directory))
                                continue;

                            bool hasVisibleWindow = processes[i].MainWindowHandle != IntPtr.Zero;

                            // If no visible main window, check for hidden top-level windows (system tray apps).
                            bool isTrayApp = false;
                            if (!hasVisibleWindow)
                            {
                                try
                                {
                                    isTrayApp = HasAnyTopLevelWindow(pid);
                                }
                                catch (InvalidOperationException)
                                {
                                    continue; // Process exited
                                }
                            }

                            if (hasVisibleWindow || isTrayApp)
                            {
                                snapshot.HasWindowedProcess = true;
                                if (snapshot.FirstWindowedPid == 0)
                                {
                                    snapshot.FirstWindowedPid = pid;
                                }

                                if (hasVisibleWindow)
                                {
                                    try
                                    {
                                        if (!processes[i].Responding)
                                            snapshot.HasNotRespondingProcess = true;
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        // Process already exited
                                    }
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

                                // Error dialog title check (only meaningful if there is a visible window)
                                if (hasVisibleWindow)
                                {
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
        /// Processes that have hidden top-level windows (e.g. system tray apps) are not killed.
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
                                // Check for hidden top-level windows before killing.
                                // Apps minimized to the system tray have no MainWindowHandle
                                // but still own hidden top-level windows ~ these are not zombies.
                                int pid = processes[i].Id;
                                if (HasAnyTopLevelWindow(pid))
                                {
                                    SimpleLogger.Info("KillBackgroundProcesses @ ProcessManager.cs",
                                        $"Skipping tray/hidden-window process for {app.AppName} (PID: {pid})");
                                    continue;
                                }

                                // PATH GATE (Patch 1): never kill a name-match whose image path isn't this app's exe.
                                if (!ProcessPathMatches(pid, app.Directory))
                                {
                                    SimpleLogger.Warn("KillBackgroundProcesses @ ProcessManager.cs",
                                        $"Skipping same-name process (PID {pid}) — image path does not match '{app.Directory}'");
                                    continue;
                                }

                                // ENFORCEMENT GATE (Patch 5): in LogOnly mode, record and skip.
                                if (!_settingsService.EnforcementEnabled)
                                {
                                    SimpleLogger.Warn("KillBackgroundProcesses @ ProcessManager.cs",
                                        $"[DRY-RUN] Would kill background process for {app.AppName} (PID {pid})");
                                    continue;
                                }

                                processes[i].Kill();
                                killed++;
                                SimpleLogger.Warn("KillBackgroundProcesses @ ProcessManager.cs",
                                    $"Killed background process for {app.AppName} (PID: {pid}), image path: {TryGetProcessPath(pid) ?? "(unknown)"}");
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

                // Only clean up lingering processes for apps NOT flagged as service-style (Patch 1/3).
                int bgKilled = app.TreatAsService ? 0 : KillBackgroundProcesses(app);
                if (bgKilled > 0)
                {
                    SimpleLogger.Info("StartApplication @ ProcessManager.cs",
                        $"Cleaned up {bgKilled} background process(es) for {app.AppName} before starting");
                }

                // Determine what to launch: launcher script/exe (if configured) or the primary exe
                string launchPath;
                bool useLauncher = !string.IsNullOrEmpty(app.LauncherPath);

                if (useLauncher)
                {
                    launchPath = app.LauncherPath;
                    if (!File.Exists(launchPath))
                    {
                        SimpleLogger.Error("StartApplication @ ProcessManager.cs",
                            $"Cannot start {app.AppName}: Launcher not found at {launchPath}");
                        return false;
                    }
                }
                else
                {
                    launchPath = app.Directory;
                    if (!File.Exists(launchPath))
                    {
                        SimpleLogger.Error("StartApplication @ ProcessManager.cs",
                            $"Cannot start {app.AppName}: File not found at {launchPath}");
                        return false;
                    }
                }

                ProcessStartInfo startInfo = BuildStartInfo(launchPath);

                Process process = Process.Start(startInfo);

                if (process != null)
                {
                    try
                    {
                        int pid = process.Id;

                        if (useLauncher)
                        {
                            // The PID here belongs to the launcher process (e.g. cmd.exe, powershell.exe, cscript.exe),
                            // not the actual application. Track the launcher PID for identification but
                            // we cannot track the launcher handle for exit code ~ it will exit with 0
                            // after spawning the app. The actual app's Process handle will be tracked
                            // later when the watchdog detects it running (via TrackActualAppProcess).
                            TrackLaunchedPid(app.Index, pid);
                            // Dispose the launcher Process handle ~ we don't need it for exit code
                            process.Dispose();

                            string monitoredProcessName = Path.GetFileNameWithoutExtension(app.Directory);
                            SimpleLogger.Info("StartApplication @ ProcessManager.cs",
                                $"Successfully started {app.AppName} via launcher '{Path.GetFileName(app.LauncherPath)}' (Launcher PID: {pid}), monitoring process: {monitoredProcessName}");

                            // Attempt to find and track the actual application process
                            try
                            {
                                Process[] appProcesses = null;
                                try
                                {
                                    appProcesses = Process.GetProcessesByName(monitoredProcessName);
                                    if (appProcesses.Length > 0)
                                    {
                                        // Log all found PIDs for the monitored process
                                        string[] pids = new string[appProcesses.Length];
                                        for (int i = 0; i < appProcesses.Length; i++)
                                        {
                                            try { pids[i] = appProcesses[i].Id.ToString(); }
                                            catch (InvalidOperationException) { pids[i] = "?"; }
                                        }
                                        SimpleLogger.Info("StartApplication @ ProcessManager.cs",
                                            $"Found {appProcesses.Length} '{monitoredProcessName}' process(es) ~ App PID(s): {string.Join(", ", pids)}");

                                        // Get the PID of the first process, then dispose all
                                        // GetProcessesByName handles (they have limited access rights)
                                        int targetPid = -1;
                                        try { targetPid = appProcesses[0].Id; }
                                        catch (InvalidOperationException) { }

                                        for (int i = 0; i < appProcesses.Length; i++)
                                        {
                                            appProcesses[i].Dispose();
                                        }
                                        appProcesses = null; // prevent double-dispose in finally

                                        // Re-open via GetProcessById and enable EnableRaisingEvents.
                                        // This forces the runtime to open a wait handle with SYNCHRONIZE
                                        // access, required to read ExitCode after exit on .NET Framework 4.0.
                                        if (targetPid > 0)
                                        {
                                            try
                                            {
                                                Process tracked = Process.GetProcessById(targetPid);
                                                try { tracked.EnableRaisingEvents = true; }
                                                catch (System.ComponentModel.Win32Exception) { }
                                                catch (InvalidOperationException) { }

                                                TrackLaunchedProcess(app.Index, tracked);
                                                TrackLaunchedPid(app.Index, targetPid);
                                            }
                                            catch (ArgumentException)
                                            {
                                                // Process already exited
                                            }
                                        }
                                    }
                                    else
                                    {
                                        SimpleLogger.Info("StartApplication @ ProcessManager.cs",
                                            $"No '{monitoredProcessName}' process found yet ~ it may still be starting via the launcher. Exit code tracking will be attempted when the process appears.");
                                    }
                                }
                                finally
                                {
                                    if (appProcesses != null)
                                    {
                                        for (int i = 0; i < appProcesses.Length; i++)
                                        {
                                            appProcesses[i].Dispose();
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                SimpleLogger.Debug("StartApplication @ ProcessManager.cs",
                                    $"Could not enumerate '{monitoredProcessName}' processes: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Direct launch ~ track both PID and Process handle for exit code retrieval
                            TrackLaunchedPid(app.Index, pid);
                            TrackLaunchedProcess(app.Index, process);
                            SimpleLogger.Info("StartApplication @ ProcessManager.cs",
                                $"Successfully started {app.AppName} (PID: {pid})");
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        if (!useLauncher)
                        {
                            // PID unavailable but process started ~ still keep the handle
                            TrackLaunchedProcess(app.Index, process);
                        }
                        SimpleLogger.Info("StartApplication @ ProcessManager.cs",
                            $"Successfully started {app.AppName} (PID unavailable - process may have exited quickly)");
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

        /// <summary>
        /// Builds the appropriate ProcessStartInfo for the given file path.
        /// Script files (.ps1, .bat, .cmd, .vbs) are launched via their interpreter
        /// so they execute rather than opening in a text editor.
        /// Executable files (.exe and everything else) use shell execute directly.
        /// </summary>
        private static ProcessStartInfo BuildStartInfo(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string workingDir = Path.GetDirectoryName(filePath);

            switch (ext)
            {
                case ".ps1":
                    return new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-ExecutionPolicy Bypass -NoProfile -File \"" + filePath + "\"",
                        UseShellExecute = false,
                        WorkingDirectory = workingDir
                    };

                case ".vbs":
                    return new ProcessStartInfo
                    {
                        FileName = "cscript.exe",
                        Arguments = "//NoLogo \"" + filePath + "\"",
                        UseShellExecute = false,
                        WorkingDirectory = workingDir
                    };

                case ".bat":
                case ".cmd":
                    return new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C \"" + filePath + "\"",
                        UseShellExecute = false,
                        WorkingDirectory = workingDir
                    };

                default:
                    // .exe or any other file type ~ use CreateProcess (UseShellExecute = false)
                    // so the returned Process handle properly supports ExitCode retrieval.
                    // UseShellExecute = true uses ShellExecuteEx which does not always provide
                    // a process handle that supports GetExitCodeProcess on .NET Framework 4.0.
                    return new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = false,
                        WorkingDirectory = workingDir
                    };
            }
        }

        /// <summary>
        /// Stops the specified application by closing its main window, if any.
        /// If the application does not respond to a graceful close, it will be
        /// forcefully terminated.
        /// The exit code is captured from the actual application process after it exits.
        /// </summary>
        public bool StopApplication(ManagedApplication app)
        {
            return StopApplication(app, out _);
        }

        /// <summary>
        /// Stops the specified application and captures the exit code.
        /// </summary>
        public bool StopApplication(ManagedApplication app, out int? exitCode)
        {
            exitCode = null;
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
                        // Process already exited ~ try to read exit code from tracked handle before cleanup
                        exitCode = GetTrackedExitCode(app.Index);
                        UntrackPid(app.Index);
                        SimpleLogger.Warn("StopApplication @ ProcessManager.cs", $"Cannot stop {app.AppName}: Not running");
                        return false;
                    }

                    for (int i = 0; i < processes.Length; i++)
                    {
                        try
                        {
                            int pid = processes[i].Id;
                            // Patch 4: capture image path for kill attribution logging
                            string imagePath = TryGetProcessPath(pid) ?? "(unknown)";

                            // PATH GATE: only kill processes whose image path matches this app's exe.
                            // If path is unknown (access denied), still proceed — fail-open for stops
                            // since the user/watchdog explicitly requested this app be stopped.
                            // But if path IS known and DOESN'T match, skip it — it belongs to another install.
                            if (imagePath != "(unknown)" && !ProcessPathMatches(pid, app.Directory))
                            {
                                SimpleLogger.Warn("StopApplication @ ProcessManager.cs",
                                    $"Skipping same-name process (PID {pid}, Path: {imagePath}) — does not match '{app.Directory}'");
                                continue;
                            }

                            bool hasWindow = processes[i].MainWindowHandle != IntPtr.Zero;

                            if (hasWindow)
                            {
                                processes[i].CloseMainWindow();

                                if (!processes[i].WaitForExit(3000))
                                {
                                    try
                                    {
                                        processes[i].Kill();
                                        processes[i].WaitForExit(2000);
                                        SimpleLogger.Warn("StopApplication @ ProcessManager.cs", $"Force killed {app.AppName} (PID: {pid}, Path: {imagePath}) - graceful shutdown timed out");
                                    }
                                    catch (System.ComponentModel.Win32Exception killEx)
                                    {
                                        SimpleLogger.Error("StopApplication @ ProcessManager.cs", $"Access denied killing {app.AppName} (PID: {pid}, Path: {imagePath}): {killEx.Message}");
                                    }
                                }
                                else
                                {
                                    SimpleLogger.Info("StopApplication @ ProcessManager.cs", $"Gracefully stopped {app.AppName} (PID: {pid}, Path: {imagePath})");
                                }
                            }
                            else
                            {
                                try
                                {
                                    processes[i].Kill();
                                    processes[i].WaitForExit(2000);
                                    SimpleLogger.Warn("StopApplication @ ProcessManager.cs", $"Force killed background process {app.AppName} (PID: {pid}, Path: {imagePath}) - no main window");
                                }
                                catch (System.ComponentModel.Win32Exception killEx)
                                {
                                    SimpleLogger.Error("StopApplication @ ProcessManager.cs", $"Access denied killing background {app.AppName} (PID: {pid}, Path: {imagePath}): {killEx.Message}");
                                }
                            }

                            // Capture exit code from the process we just stopped
                            if (exitCode == null)
                            {
                                try
                                {
                                    if (processes[i].HasExited)
                                    {
                                        exitCode = processes[i].ExitCode;
                                    }
                                }
                                catch (InvalidOperationException) { }
                                catch (System.ComponentModel.Win32Exception) { }
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

                // If we didn't get an exit code from the stopped processes,
                // try the tracked handle as a fallback
                if (exitCode == null)
                {
                    // Give the tracked handle a moment to register the exit
                    Process trackedProc;
                    if (_appProcessHandles.TryGetValue(app.Index, out trackedProc))
                    {
                        try
                        {
                            if (trackedProc.WaitForExit(1000))
                            {
                                exitCode = trackedProc.ExitCode;
                            }
                        }
                        catch (InvalidOperationException) { }
                        catch (System.ComponentModel.Win32Exception) { }
                        catch (Exception) { }
                    }
                }

                // Now safe to clean up the tracked handle
                UntrackPid(app.Index);

                return true;
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("StopApplication @ ProcessManager.cs", $"Error stopping {(app != null ? app.AppName : "null")}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to track the actual application process for exit code retrieval.
        /// This is used when a launcher was used to start the app and the actual process
        /// wasn't found at launch time. Call this when the watchdog first detects the app running.
        /// Returns true if a process was successfully tracked.
        /// </summary>
        public bool TryTrackActualAppProcess(ManagedApplication app)
        {
            // If we already have a tracked process handle, check if it's usable
            Process existing;
            if (_appProcessHandles.TryGetValue(app.Index, out existing))
            {
                try
                {
                    // Check if the existing handle is still for a live process
                    if (!existing.HasExited)
                        return true;
                }
                catch { }
                // Handle is stale ~ fall through to find a new one
            }

            try
            {
                string processName = Path.GetFileNameWithoutExtension(app.Directory);
                if (string.IsNullOrEmpty(processName))
                    return false;

                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        // PATH FILTER: find the first process whose path matches app.Directory.
                        // Without this, IMEE could track the wrong PID when a same-named process
                        // from a different install path exists.
                        int targetPid = -1;
                        for (int i = 0; i < processes.Length; i++)
                        {
                            try
                            {
                                int candidatePid = processes[i].Id;
                                string candidatePath = TryGetProcessPath(candidatePid);
                                // Accept if path is unknown (permissive) or matches
                                if (candidatePath == null || ProcessPathMatches(candidatePid, app.Directory))
                                {
                                    targetPid = candidatePid;
                                    break;
                                }
                            }
                            catch (InvalidOperationException) { continue; }
                        }
                        if (targetPid == -1)
                        {
                            return false; // No matching process found
                        }

                        // Dispose the GetProcessesByName handles ~ they have limited access rights
                        // on .NET Framework 4.0 and cannot reliably read ExitCode after the process exits.
                        for (int i = 0; i < processes.Length; i++)
                        {
                            processes[i].Dispose();
                        }
                        processes = null; // prevent double-dispose in finally

                        // Re-open via GetProcessById and enable EnableRaisingEvents.
                        // EnableRaisingEvents forces the runtime to open a wait handle with
                        // SYNCHRONIZE access, which is required to read ExitCode after exit.
                        Process tracked = null;
                        try
                        {
                            tracked = Process.GetProcessById(targetPid);
                            tracked.EnableRaisingEvents = true;
                        }
                        catch (ArgumentException)
                        {
                            // Process already exited between GetProcessesByName and GetProcessById
                            if (tracked != null) tracked.Dispose();
                            return false;
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            // Access denied ~ still track it, ExitCode may work via fallback
                            // (tracked handle is usable, just EnableRaisingEvents failed)
                        }
                        catch (InvalidOperationException)
                        {
                            if (tracked != null) tracked.Dispose();
                            return false;
                        }

                        TrackLaunchedProcess(app.Index, tracked);
                        TrackLaunchedPid(app.Index, targetPid);

                        SimpleLogger.Debug("TryTrackActualAppProcess @ ProcessManager.cs",
                            $"Tracked '{app.AppName}' process (PID: {targetPid}) for exit code retrieval");
                        return true;
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
                SimpleLogger.Debug("TryTrackActualAppProcess @ ProcessManager.cs",
                    $"Error tracking process for '{app.AppName}': {ex.Message}");
            }
            return false;
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
                    // Still running ~ no exit code available
                    return null;
                }
                catch (ArgumentException)
                {
                    // Process does not exist (already exited) ~ we cannot retrieve exit code
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
                    // PATH FILTER: only count processes whose path matches this app's exe.
                    int count = 0;
                    for (int i = 0; i < processes.Length; i++)
                    {
                        try
                        {
                            int pid = processes[i].Id;
                            string path = TryGetProcessPath(pid);
                            // Permissive: unknown path counts as a match
                            if (path == null || ProcessPathMatches(pid, app.Directory))
                                count++;
                        }
                        catch (InvalidOperationException) { }
                    }
                    return count;
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

        /// <summary>
        /// Disposes all tracked Process handles. Call on application shutdown.
        /// </summary>
        public void DisposeAllTrackedHandles()
        {
            foreach (var kvp in _appProcessHandles)
            {
                try { kvp.Value.Dispose(); } catch { }
            }
            _appProcessHandles.Clear();
            _appPidMap.Clear();
        }
    }
}
