using System;

namespace IntelligentMutexExecutionEnvironment.Models
{
    [Serializable]
    public class ManagedApplication
    {
        public int Index { get; set; }
        public int GroupId { get; set; }
        public string AppName { get; set; }
        public string Directory { get; set; }

        /// <summary>
        /// Optional path to a launcher script (.vbs, .bat, .ps1, .cmd) or launcher executable
        /// that is used to start the application instead of launching the primary exe directly.
        /// When set, IMEE will execute this launcher to start the app, but will still monitor
        /// the primary exe (Directory) for running status detection.
        /// Null or empty means the app is launched directly from Directory.
        /// </summary>
        public string LauncherPath { get; set; }

        public DateTime AddedDate { get; set; }
        public bool IsRunning { get; set; }
        public DateTime? LastStart { get; set; }
        public DateTime? LastStop { get; set; }
        public bool KeepOpen { get; set; }
        public int CrashCount { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public int StartDelaySeconds { get; set; }
        public int StartupDelaySeconds { get; set; }
        public int StableRunPeriodSeconds { get; set; }

        /// <summary>
        /// How long (in seconds) a process can remain in a "not responding" state
        /// before being force-killed for auto-restart. 0 = use default (2 poll cycles).
        /// </summary>
        public int NotRespondingTimeoutSeconds { get; set; }

        /// <summary>
        /// Optional memory limit in megabytes. If a process exceeds this working set size,
        /// it is considered unhealthy and will be force-killed for auto-restart (KeepOpen only).
        /// 0 = no limit (disabled).
        /// </summary>
        public int MemoryLimitMB { get; set; }

        /// <summary>
        /// When enabled, the watchdog tracks the application's initial window title
        /// and treats a sudden title change as a potential error dialog (e.g. WinForms
        /// ThreadExceptionDialog). Requires 2 consecutive detections to confirm.
        /// Default: false (disabled, since some apps legitimately change their title).
        /// </summary>
        public bool DetectTitleChange { get; set; }

        /// <summary>
        /// When true, health monitoring checks (not-responding, error dialogs, memory/CPU limits,
        /// zombie/background processes, etc.) will actively be enforced (processes may be killed
        /// and auto-restarted). If false, IMEE will only log and notify the user when issues are
        /// detected but will not take corrective action automatically.
        /// Default: false (disabled, opt-in is off).
        /// </summary>
        public bool HealthMonitoringEnabled { get; set; }

        /// <summary>
        /// Stores the exit code of the last process termination.
        /// Null if no exit has been recorded yet.
        /// </summary>
        public int? LastExitCode { get; set; }

        public ManagedApplication()
        {
            AddedDate = DateTime.Now;
            IsRunning = false;
            LastStart = null;
            LastStop = null;
            KeepOpen = false;
            CrashCount = 0;
            RetryCount = 0;
            MaxRetries = 3;
            StartDelaySeconds = 5;
            StartupDelaySeconds = 10;
            StableRunPeriodSeconds = 30;
            NotRespondingTimeoutSeconds = 0;
            MemoryLimitMB = 0;
            LastExitCode = null;
            DetectTitleChange = false;
            HealthMonitoringEnabled = false; // default: disabled (opt-in is off)
            LauncherPath = null;
        }

        public string GetLastStartDisplay()
        {
            return LastStart.HasValue ? LastStart.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Never";
        }

        public string GetLastStopDisplay()
        {
            return LastStop.HasValue ? LastStop.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Never";
        }

        public string GetKeepOpenDisplay()
        {
            return KeepOpen ? "Yes" : "No";
        }

        public override string ToString()
        {
            return AppName;
        }
    }
}
