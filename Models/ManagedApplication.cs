using System;

namespace InstanceManager.Models
{
    [Serializable]
    public class ManagedApplication
    {
        public int Index { get; set; }
        public int GroupId { get; set; }
        public string AppName { get; set; }
        public string Directory { get; set; }
        public DateTime AddedDate { get; set; }
        public bool IsRunning { get; set; }
        public DateTime? LastStart { get; set; }
        public DateTime? LastStop { get; set; }
        public bool KeepOpen { get; set; }
        public int CrashCount { get; set; }
        public int RetryCount { get; set; }
        public int StartDelaySeconds { get; set; }

        public ManagedApplication()
        {
            AddedDate = DateTime.Now;
            IsRunning = false;
            LastStart = null;
            LastStop = null;
            KeepOpen = false;
            CrashCount = 0;
            RetryCount = 0;
            StartDelaySeconds = 5;
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
