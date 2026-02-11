using System;
using System.IO;
using System.Text;
using InstanceManager.Utilities;

namespace InstanceManager.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        private string _stationName;

        public string StationName
        {
            get { return _stationName ?? ""; }
            set
            {
                _stationName = value ?? "";
                Save();
            }
        }

        public SettingsService()
        {
            _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            _stationName = "";
            Load();
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
                    _stationName = ParseStationName(json);
                    SimpleLogger.Info("Load @ SettingsService.cs", $"Loaded station name: {_stationName}");
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
                sb.AppendLine($"  \"StationName\": \"{EscapeJson(_stationName)}\"");
                sb.AppendLine("}");

                // Write to temp file first, then replace to avoid corruption on crash
                string tempPath = _settingsPath + ".tmp";
                File.WriteAllText(tempPath, sb.ToString());

                if (File.Exists(_settingsPath))
                {
                    File.Delete(_settingsPath);
                }

                File.Move(tempPath, _settingsPath);

                SimpleLogger.Info("Save @ SettingsService.cs", $"Saved station name: {_stationName}");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("Save @ SettingsService.cs", $"Error saving settings: {ex.Message}");
            }
        }

        private string ParseStationName(string json)
        {
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return "";

            string content = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(content))
                return "";

            int colonIndex = content.IndexOf(':');
            if (colonIndex == -1)
                return "";

            string key = content.Substring(0, colonIndex).Trim().Trim('"');
            string rawValue = content.Substring(colonIndex + 1).Trim();

            if (key == "StationName")
            {
                if (rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
                {
                    rawValue = rawValue.Substring(1, rawValue.Length - 2);
                }
                return UnescapeJson(rawValue);
            }

            return "";
        }

        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private string UnescapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\\\", "\x00")
                        .Replace("\\\"", "\"")
                        .Replace("\\n", "\n")
                        .Replace("\\r", "\r")
                        .Replace("\x00", "\\");
        }
    }
}
