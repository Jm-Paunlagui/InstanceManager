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
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    if (!string.IsNullOrEmpty(json))
                    {
                        _stationName = ParseStationName(json);
                        SimpleLogger.Info("Load @ SettingsService.cs", $"Loaded station name: {_stationName}");
                    }
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
                File.WriteAllText(_settingsPath, sb.ToString());
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
