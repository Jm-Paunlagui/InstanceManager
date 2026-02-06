using System;
using System.IO;
using System.Text;

namespace InstanceManager.Utilities
{
    public static class SimpleLogger
    {
        private static readonly object _lockObject = new object();
        private static string _logDirectory;
        private static bool _isInitialized;

        static SimpleLogger()
        {
            try
            {
                _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
                _isInitialized = true;
            }
            catch
            {
                // If we can't create the log directory, logging will be silently disabled.
                // This prevents a TypeInitializationException from crashing the application.
                _isInitialized = false;
            }
        }

        public static void Log(string level, string location, string message)
        {
            if (!_isInitialized) return;

            try
            {
                lock (_lockObject)
                {
                    string logFile = Path.Combine(_logDirectory, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                    string machineInfo = GetMachineInfo();
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

                    string logEntry = $"[{machineInfo}][{timestamp}][{level}][PID:{processId}][{location}] - {message}";

                    using (StreamWriter writer = new StreamWriter(logFile, true, Encoding.UTF8))
                    {
                        writer.WriteLine(logEntry);
                    }
                }
            }
            catch
            {
                // Silent fail - logging should never crash the application
            }
        }

        public static void Info(string location, string message)
        {
            Log("INFO", location, message);
        }

        public static void Debug(string location, string message)
        {
            Log("DEBUG", location, message);
        }

        public static void Warn(string location, string message)
        {
            Log("WARN", location, message);
        }

        public static void Error(string location, string message)
        {
            Log("ERROR", location, message);
        }

        public static void Fatal(string location, string message)
        {
            Log("FATAL", location, message);
        }

        private static string GetMachineInfo()
        {
            try
            {
                string hostname = Environment.MachineName;
                string username = Environment.UserName;
                string ip = GetLocalIPAddress();
                return $"{hostname}/{username} {ip}";
            }
            catch
            {
                return "Unknown/Unknown 0.0.0.0";
            }
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ipAddress in host.AddressList)
                {
                    if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ipAddress.ToString();
                    }
                }
            }
            catch { }
            return "0.0.0.0";
        }
    }
}
