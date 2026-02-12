using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    public static class SimpleLogger
    {
        private static readonly object _lockObject = new object();
        private static string _logDirectory;
        private static bool _isInitialized;
        private static string _cachedMachineInfo;
        private static int _cachedProcessId;
        private static List<string> _logBuffer = new List<string>();
        private static string _currentLogFile;
        private static DateTime _lastFlush = DateTime.MinValue;
        private static DateTime _lastCleanup = DateTime.MinValue;
        private static int _flushIntervalSeconds = 10;
        private static int _maxBufferSize = 100;
        private static int _maxLogAgeDays = 7;

        static SimpleLogger()
        {
            try
            {
                _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                // Cache machine info and PID once at startup instead of every log call
                _cachedMachineInfo = BuildMachineInfo();
                using (var proc = System.Diagnostics.Process.GetCurrentProcess())
                {
                    _cachedProcessId = proc.Id;
                }

                _isInitialized = true;
            }
            catch
            {
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Updates the logger configuration at runtime. Thread-safe.
        /// </summary>
        public static void Configure(int flushIntervalSeconds, int maxBufferSize, int maxLogAgeDays)
        {
            lock (_lockObject)
            {
                _flushIntervalSeconds = Math.Max(1, flushIntervalSeconds);
                _maxBufferSize = Math.Max(10, maxBufferSize);
                _maxLogAgeDays = Math.Max(1, maxLogAgeDays);
            }
        }

        public static void Log(string level, string location, string message)
        {
            if (!_isInitialized) return;

            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logEntry = string.Concat("[", _cachedMachineInfo, "][", timestamp, "][", level, "][PID:", _cachedProcessId.ToString(), "][", location, "] - ", message);
                string logFile = Path.Combine(_logDirectory, DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                lock (_lockObject)
                {
                    // If the log file changed (day rolled over), flush the old buffer first
                    if (_currentLogFile != null && _currentLogFile != logFile && _logBuffer.Count > 0)
                    {
                        FlushBufferUnsafe(_currentLogFile);
                    }
                    _currentLogFile = logFile;

                    _logBuffer.Add(logEntry);

                    bool shouldFlush = _logBuffer.Count >= _maxBufferSize
                        || level == "FATAL"
                        || level == "ERROR"
                        || (DateTime.Now - _lastFlush).TotalSeconds >= _flushIntervalSeconds;

                    if (shouldFlush)
                    {
                        FlushBufferUnsafe(logFile);
                    }

                    // Periodic cleanup of old log files (check once per day)
                    if ((DateTime.Now - _lastCleanup).TotalHours >= 24)
                    {
                        _lastCleanup = DateTime.Now;
                        CleanupOldLogsUnsafe();
                    }
                }
            }
            catch
            {
                // Silent fail - logging should never crash the application
            }
        }

        /// <summary>
        /// Flushes any buffered log entries to disk. Call this before application exit.
        /// </summary>
        public static void Flush()
        {
            if (!_isInitialized) return;

            try
            {
                lock (_lockObject)
                {
                    if (_logBuffer.Count > 0 && _currentLogFile != null)
                    {
                        FlushBufferUnsafe(_currentLogFile);
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }

        /// <summary>
        /// Must be called while holding _lockObject.
        /// </summary>
        private static void FlushBufferUnsafe(string logFile)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(logFile, true, Encoding.UTF8))
                {
                    for (int i = 0; i < _logBuffer.Count; i++)
                    {
                        writer.WriteLine(_logBuffer[i]);
                    }
                }
                _logBuffer.Clear();
                _lastFlush = DateTime.Now;
            }
            catch
            {
                // If we can't write, discard buffer to prevent unbounded memory growth
                if (_logBuffer.Count > _maxBufferSize * 2)
                {
                    _logBuffer.Clear();
                }
            }
        }

        /// <summary>
        /// Deletes log files older than _maxLogAgeDays. Must be called while holding _lockObject.
        /// </summary>
        private static void CleanupOldLogsUnsafe()
        {
            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-_maxLogAgeDays);
                string[] files = Directory.GetFiles(_logDirectory, "*.log");
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        if (File.GetLastWriteTime(files[i]) < cutoff)
                        {
                            File.Delete(files[i]);
                        }
                    }
                    catch
                    {
                        // Ignore individual file deletion failures
                    }
                }
            }
            catch
            {
                // Ignore cleanup failures
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

        private static string BuildMachineInfo()
        {
            try
            {
                string hostname = Environment.MachineName;
                string username = Environment.UserName;
                string ip = GetLocalIPAddress();
                return hostname + "/" + username + " " + ip;
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
