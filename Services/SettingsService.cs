using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IntelligentMutexExecutionEnvironment.Utilities;

namespace IntelligentMutexExecutionEnvironment.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;

        // === General Settings ===
        private string _stationName;
        private bool _runOnStartup;

        public string StationName
        {
            get { return _stationName ?? ""; }
            set { _stationName = value ?? ""; Save(); }
        }

        public bool RunOnStartup
        {
            get { return _runOnStartup; }
            set { _runOnStartup = value; Save(); }
        }

        // === Performance Settings ===
        private int _statusPollIntervalMs;
        private int _startGracePeriodSeconds;
        private int _gcCollectIntervalMinutes;
        private int _storageSaveIntervalSeconds;

        /// <summary>
        /// How often the watchdog polls process statuses (milliseconds). Default: 10000.
        /// </summary>
        public int StatusPollIntervalMs
        {
            get { return _statusPollIntervalMs; }
            set { _statusPollIntervalMs = Clamp(value, 1000, 60000); Save(); }
        }

        /// <summary>
        /// Grace period after launching an app before watchdog monitoring begins (seconds). Default: 10.
        /// </summary>
        public int StartGracePeriodSeconds
        {
            get { return _startGracePeriodSeconds; }
            set { _startGracePeriodSeconds = Clamp(value, 1, 120); Save(); }
        }

        /// <summary>
        /// Interval between periodic GC collections to prevent memory growth (minutes). Default: 30.
        /// </summary>
        public int GcCollectIntervalMinutes
        {
            get { return _gcCollectIntervalMinutes; }
            set { _gcCollectIntervalMinutes = Clamp(value, 5, 1440); Save(); }
        }

        /// <summary>
        /// Minimum interval between throttled storage writes (seconds). Default: 30.
        /// User-initiated saves always write immediately.
        /// </summary>
        public int StorageSaveIntervalSeconds
        {
            get { return _storageSaveIntervalSeconds; }
            set { _storageSaveIntervalSeconds = Clamp(value, 5, 300); Save(); }
        }

        // === Logging Settings ===
        private int _logFlushIntervalSeconds;
        private int _logBufferSize;
        private int _logRetentionDays;

        /// <summary>
        /// How often buffered log entries are flushed to disk (seconds). Default: 10.
        /// </summary>
        public int LogFlushIntervalSeconds
        {
            get { return _logFlushIntervalSeconds; }
            set { _logFlushIntervalSeconds = Clamp(value, 1, 120); Save(); }
        }

        /// <summary>
        /// Maximum number of buffered log entries before a flush is forced. Default: 100.
        /// </summary>
        public int LogBufferSize
        {
            get { return _logBufferSize; }
            set { _logBufferSize = Clamp(value, 10, 1000); Save(); }
        }

        /// <summary>
        /// Number of days to retain log files before automatic cleanup. Default: 7.
        /// </summary>
        public int LogRetentionDays
        {
            get { return _logRetentionDays; }
            set { _logRetentionDays = Clamp(value, 1, 365); Save(); }
        }

        // === Defaults ===
        public const int DefaultStatusPollIntervalMs = 10000;
        public const int DefaultStartGracePeriodSeconds = 10;
        public const int DefaultGcCollectIntervalMinutes = 30;
        public const int DefaultStorageSaveIntervalSeconds = 30;
        public const int DefaultLogFlushIntervalSeconds = 10;
        public const int DefaultLogBufferSize = 100;
        public const int DefaultLogRetentionDays = 7;

        public SettingsService()
        {
            _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            SetDefaults();
            Load();
        }

        private void SetDefaults()
        {
            _stationName = "";
            _runOnStartup = false;
            _statusPollIntervalMs = DefaultStatusPollIntervalMs;
            _startGracePeriodSeconds = DefaultStartGracePeriodSeconds;
            _gcCollectIntervalMinutes = DefaultGcCollectIntervalMinutes;
            _storageSaveIntervalSeconds = DefaultStorageSaveIntervalSeconds;
            _logFlushIntervalSeconds = DefaultLogFlushIntervalSeconds;
            _logBufferSize = DefaultLogBufferSize;
            _logRetentionDays = DefaultLogRetentionDays;
        }

        /// <summary>
        /// Saves all settings without triggering individual property Save() calls.
        /// Use this when updating multiple settings at once from a dialog.
        /// Logs every individual setting change for traceability and accountability.
        /// </summary>
        public void SaveAll(string stationName, bool runOnStartup, int statusPollIntervalMs, int startGracePeriodSeconds,
            int gcCollectIntervalMinutes, int storageSaveIntervalSeconds,
            int logFlushIntervalSeconds, int logBufferSize, int logRetentionDays)
        {
            // Clamp new values first
            string newStationName = stationName ?? "";
            bool newRunOnStartup = runOnStartup;
            int newStatusPollIntervalMs = Clamp(statusPollIntervalMs, 1000, 60000);
            int newStartGracePeriodSeconds = Clamp(startGracePeriodSeconds, 1, 120);
            int newGcCollectIntervalMinutes = Clamp(gcCollectIntervalMinutes, 5, 1440);
            int newStorageSaveIntervalSeconds = Clamp(storageSaveIntervalSeconds, 5, 300);
            int newLogFlushIntervalSeconds = Clamp(logFlushIntervalSeconds, 1, 120);
            int newLogBufferSize = Clamp(logBufferSize, 10, 1000);
            int newLogRetentionDays = Clamp(logRetentionDays, 1, 365);

            // Log each individual change for traceability and accountability
            int changeCount = 0;
            if (_stationName != newStationName)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"StationName changed: \"{_stationName}\" to \"{newStationName}\"");
                changeCount++;
            }
            if (_runOnStartup != newRunOnStartup)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"RunOnStartup changed: {_runOnStartup} to {newRunOnStartup}");
                changeCount++;
            }
            if (_statusPollIntervalMs != newStatusPollIntervalMs)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"StatusPollIntervalMs changed: {_statusPollIntervalMs} to {newStatusPollIntervalMs}");
                changeCount++;
            }
            if (_startGracePeriodSeconds != newStartGracePeriodSeconds)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"StartGracePeriodSeconds changed: {_startGracePeriodSeconds} to {newStartGracePeriodSeconds}");
                changeCount++;
            }
            if (_gcCollectIntervalMinutes != newGcCollectIntervalMinutes)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"GcCollectIntervalMinutes changed: {_gcCollectIntervalMinutes} to {newGcCollectIntervalMinutes}");
                changeCount++;
            }
            if (_storageSaveIntervalSeconds != newStorageSaveIntervalSeconds)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"StorageSaveIntervalSeconds changed: {_storageSaveIntervalSeconds} to {newStorageSaveIntervalSeconds}");
                changeCount++;
            }
            if (_logFlushIntervalSeconds != newLogFlushIntervalSeconds)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"LogFlushIntervalSeconds changed: {_logFlushIntervalSeconds} to {newLogFlushIntervalSeconds}");
                changeCount++;
            }
            if (_logBufferSize != newLogBufferSize)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"LogBufferSize changed: {_logBufferSize} to {newLogBufferSize}");
                changeCount++;
            }
            if (_logRetentionDays != newLogRetentionDays)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"LogRetentionDays changed: {_logRetentionDays} to {newLogRetentionDays}");
                changeCount++;
            }

            if (changeCount == 0)
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs", "Settings dialog closed with OK — no changes detected");
            }
            else
            {
                SimpleLogger.Info("SettingsChanged @ SettingsService.cs",
                    $"Total settings changed: {changeCount}");
            }

            // Apply the new values
            _stationName = newStationName;
            _runOnStartup = newRunOnStartup;
            _statusPollIntervalMs = newStatusPollIntervalMs;
            _startGracePeriodSeconds = newStartGracePeriodSeconds;
            _gcCollectIntervalMinutes = newGcCollectIntervalMinutes;
            _storageSaveIntervalSeconds = newStorageSaveIntervalSeconds;
            _logFlushIntervalSeconds = newLogFlushIntervalSeconds;
            _logBufferSize = newLogBufferSize;
            _logRetentionDays = newLogRetentionDays;
            Save();
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void Load()
        {
            try
            {
                string json = null;

                if (File.Exists(_settingsPath))
                {
                    json = File.ReadAllText(_settingsPath);
                }

                // Fall back to temp file if primary is missing/empty (interrupted save)
                if (string.IsNullOrEmpty(json) || json.Trim().Length < 2)
                {
                    string tempPath = _settingsPath + ".tmp";
                    if (File.Exists(tempPath))
                    {
                        json = File.ReadAllText(tempPath);
                        if (!string.IsNullOrEmpty(json) && json.Trim().Length >= 2)
                        {
                            SimpleLogger.Warn("Load @ SettingsService.cs", "Recovered settings from temp file");
                            try { File.Copy(tempPath, _settingsPath, true); }
                            catch { /* non-critical */ }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(json))
                {
                    ParseSettings(json);
                    SimpleLogger.Info("Load @ SettingsService.cs", $"Loaded settings (Station: {_stationName})");
                }
                else
                {
                    SimpleLogger.Info("Load @ SettingsService.cs", "No settings file found, using defaults");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("Load @ SettingsService.cs", $"Error loading settings: {ex.Message}");
            }
        }

        private void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"StationName\": \"{EscapeJson(_stationName)}\",");
                sb.AppendLine($"  \"RunOnStartup\": {(_runOnStartup ? "true" : "false")},");
                sb.AppendLine($"  \"StatusPollIntervalMs\": {_statusPollIntervalMs},");
                sb.AppendLine($"  \"StartGracePeriodSeconds\": {_startGracePeriodSeconds},");
                sb.AppendLine($"  \"GcCollectIntervalMinutes\": {_gcCollectIntervalMinutes},");
                sb.AppendLine($"  \"StorageSaveIntervalSeconds\": {_storageSaveIntervalSeconds},");
                sb.AppendLine($"  \"LogFlushIntervalSeconds\": {_logFlushIntervalSeconds},");
                sb.AppendLine($"  \"LogBufferSize\": {_logBufferSize},");
                sb.AppendLine($"  \"LogRetentionDays\": {_logRetentionDays}");
                sb.AppendLine("}");

                // Write to temp file first, then atomically replace to avoid corruption on crash
                string tempPath = _settingsPath + ".tmp";
                File.WriteAllText(tempPath, sb.ToString());

                if (File.Exists(_settingsPath))
                {
                    File.Replace(tempPath, _settingsPath, null);
                }
                else
                {
                    File.Move(tempPath, _settingsPath);
                }

                SimpleLogger.Debug("Save @ SettingsService.cs", "Settings saved");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("Save @ SettingsService.cs", $"Error saving settings: {ex.Message}");
            }
        }

        private void ParseSettings(string json)
        {
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return;

            string content = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(content))
                return;

            // Split by comma, respecting quoted strings
            var properties = SplitProperties(content);

            foreach (var prop in properties)
            {
                int colonIndex = prop.IndexOf(':');
                if (colonIndex == -1) continue;

                string key = prop.Substring(0, colonIndex).Trim().Trim('"');
                string rawValue = prop.Substring(colonIndex + 1).Trim();

                if (rawValue == "null") continue;

                // Strip quotes for string values
                string value = rawValue;
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = UnescapeJson(value.Substring(1, value.Length - 2));
                }

                switch (key)
                {
                    case "StationName":
                        _stationName = value;
                        break;
                    case "RunOnStartup":
                        _runOnStartup = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "StatusPollIntervalMs":
                        int spim;
                        if (int.TryParse(value, out spim))
                            _statusPollIntervalMs = Clamp(spim, 1000, 60000);
                        break;
                    case "StartGracePeriodSeconds":
                        int sgps;
                        if (int.TryParse(value, out sgps))
                            _startGracePeriodSeconds = Clamp(sgps, 1, 120);
                        break;
                    case "GcCollectIntervalMinutes":
                        int gcim;
                        if (int.TryParse(value, out gcim))
                            _gcCollectIntervalMinutes = Clamp(gcim, 5, 1440);
                        break;
                    case "StorageSaveIntervalSeconds":
                        int ssis;
                        if (int.TryParse(value, out ssis))
                            _storageSaveIntervalSeconds = Clamp(ssis, 5, 300);
                        break;
                    case "LogFlushIntervalSeconds":
                        int lfis;
                        if (int.TryParse(value, out lfis))
                            _logFlushIntervalSeconds = Clamp(lfis, 1, 120);
                        break;
                    case "LogBufferSize":
                        int lbs;
                        if (int.TryParse(value, out lbs))
                            _logBufferSize = Clamp(lbs, 10, 1000);
                        break;
                    case "LogRetentionDays":
                        int lrd;
                        if (int.TryParse(value, out lrd))
                            _logRetentionDays = Clamp(lrd, 1, 365);
                        break;
                }
            }
        }

        private List<string> SplitProperties(string content)
        {
            var properties = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (c == '"' && (i == 0 || content[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        properties.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                properties.Add(current.ToString());
            }

            return properties;
        }

        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private string UnescapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    char next = value[i + 1];
                    switch (next)
                    {
                        case '\\':
                            sb.Append('\\');
                            i++;
                            break;
                        case '"':
                            sb.Append('"');
                            i++;
                            break;
                        case 'n':
                            sb.Append('\n');
                            i++;
                            break;
                        case 'r':
                            sb.Append('\r');
                            i++;
                            break;
                        default:
                            sb.Append('\\');
                            break;
                    }
                }
                else
                {
                    sb.Append(value[i]);
                }
            }
            return sb.ToString();
        }
    }
}
