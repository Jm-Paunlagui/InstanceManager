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

        public ManagedApplication()
        {
            AddedDate = DateTime.Now;
            IsRunning = false;
            LastStart = null;
            LastStop = null;
        }

        public string GetLastStartDisplay()
        {
            return LastStart.HasValue ? LastStart.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Never";
        }

        public string GetLastStopDisplay()
        {
            return LastStop.HasValue ? LastStop.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Never";
        }

        public override string ToString()
        {
            return AppName;
        }
    }
}
