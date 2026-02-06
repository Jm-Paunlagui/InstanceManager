using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using InstanceManager.Models;
using InstanceManager.Utilities;

namespace InstanceManager.Services
{
    public class StorageService
    {
        private readonly string _storagePath;
        private List<ManagedApplication> _applications;

        public StorageService()
        {
            _storagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "applications.json");
            _applications = new List<ManagedApplication>();
            LoadApplications();
        }

        public List<ManagedApplication> GetAllApplications()
        {
            SimpleLogger.Debug("GetAllApplications @ StorageService.cs", "Retrieving all applications");
            return _applications.ToList();
        }

        public void AddApplication(ManagedApplication app)
        {
            app.Index = _applications.Count > 0 ? _applications.Max(a => a.Index) + 1 : 1;
            _applications.Add(app);
            SaveApplications();
            SimpleLogger.Info("AddApplication @ StorageService.cs", $"Added application: {app.AppName} (Index: {app.Index})");
        }

        public void RemoveApplication(int index)
        {
            var app = _applications.FirstOrDefault(a => a.Index == index);
            if (app != null)
            {
                _applications.Remove(app);
                SaveApplications();
                SimpleLogger.Info("RemoveApplication @ StorageService.cs", $"Removed application: {app.AppName} (Index: {index})");
            }
        }

        public void UpdateApplication(ManagedApplication app)
        {
            var existingApp = _applications.FirstOrDefault(a => a.Index == app.Index);
            if (existingApp != null)
            {
                existingApp.AppName = app.AppName;
                existingApp.Directory = app.Directory;
                existingApp.IsRunning = app.IsRunning;
                existingApp.LastStart = app.LastStart;
                existingApp.LastStop = app.LastStop;
                SaveApplications();
                SimpleLogger.Info("UpdateApplication @ StorageService.cs", $"Updated application: {app.AppName} (Index: {app.Index})");
            }
        }

        public bool ApplicationExists(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return false;

            // Normalize paths for comparison (case-insensitive on Windows)
            string normalizedPath = directory.ToLowerInvariant().Replace("/", "\\");
            
            bool exists = _applications.Any(app => 
                !string.IsNullOrEmpty(app.Directory) && 
                app.Directory.ToLowerInvariant().Replace("/", "\\") == normalizedPath);

            if (exists)
            {
                SimpleLogger.Debug("ApplicationExists @ StorageService.cs", $"Application already exists: {directory}");
            }

            return exists;
        }

        private void LoadApplications()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    string json = File.ReadAllText(_storagePath);
                    SimpleLogger.Debug("LoadApplications @ StorageService.cs", $"Loading JSON: {json.Substring(0, Math.Min(100, json.Length))}...");
                    _applications = DeserializeApplications(json);
                    SimpleLogger.Info("LoadApplications @ StorageService.cs", $"Loaded {_applications.Count} applications from storage");
                    
                    // Validate loaded applications
                    foreach (var app in _applications)
                    {
                        if (string.IsNullOrEmpty(app.Directory))
                        {
                            SimpleLogger.Warn("LoadApplications @ StorageService.cs", $"Application {app.AppName} has empty directory");
                        }
                        else
                        {
                            SimpleLogger.Debug("LoadApplications @ StorageService.cs", $"Loaded: {app.AppName} -> {app.Directory}");
                        }
                    }
                }
                else
                {
                    _applications = new List<ManagedApplication>();
                    SimpleLogger.Info("LoadApplications @ StorageService.cs", "No existing storage found, initialized empty list");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("LoadApplications @ StorageService.cs", $"Error loading applications: {ex.Message} | StackTrace: {ex.StackTrace}");
                _applications = new List<ManagedApplication>();
            }
        }

        private void SaveApplications()
        {
            try
            {
                string json = SerializeApplications(_applications);
                File.WriteAllText(_storagePath, json);
                SimpleLogger.Debug("SaveApplications @ StorageService.cs", $"Saved {_applications.Count} applications to storage");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("SaveApplications @ StorageService.cs", $"Error saving applications: {ex.Message}");
            }
        }

        private string SerializeApplications(List<ManagedApplication> apps)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < apps.Count; i++)
            {
                var app = apps[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"Index\": {app.Index},");
                sb.AppendLine($"    \"AppName\": \"{EscapeJson(app.AppName)}\",");
                sb.AppendLine($"    \"Directory\": \"{EscapeJson(app.Directory)}\",");
                sb.AppendLine($"    \"AddedDate\": \"{app.AddedDate:yyyy-MM-ddTHH:mm:ss}\",");
                sb.AppendLine($"    \"IsRunning\": {app.IsRunning.ToString().ToLower()},");
                sb.AppendLine($"    \"LastStart\": {(app.LastStart.HasValue ? $"\"{app.LastStart.Value:yyyy-MM-ddTHH:mm:ss}\"" : "null")},");
                sb.AppendLine($"    \"LastStop\": {(app.LastStop.HasValue ? $"\"{app.LastStop.Value:yyyy-MM-ddTHH:mm:ss}\"" : "null")}");
                sb.Append("  }");
                if (i < apps.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        private List<ManagedApplication> DeserializeApplications(string json)
        {
            List<ManagedApplication> apps = new List<ManagedApplication>();
            
            json = json.Trim();
            if (!json.StartsWith("[") || !json.EndsWith("]")) return apps;

            string content = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(content)) return apps;

            var objects = SplitJsonObjects(content);
            foreach (var objStr in objects)
            {
                try
                {
                    var app = ParseApplicationObject(objStr);
                    if (app != null) apps.Add(app);
                }
                catch { }
            }

            return apps;
        }

        private List<string> SplitJsonObjects(string content)
        {
            List<string> objects = new List<string>();
            int braceCount = 0;
            int startIndex = 0;

            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '{')
                {
                    if (braceCount == 0) startIndex = i;
                    braceCount++;
                }
                else if (content[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        objects.Add(content.Substring(startIndex, i - startIndex + 1));
                    }
                }
            }

            return objects;
        }

        private ManagedApplication ParseApplicationObject(string objStr)
        {
            ManagedApplication app = new ManagedApplication();
            
            objStr = objStr.Trim().Trim('{', '}');
            
            // Split by comma, but be careful with commas inside quoted strings
            var properties = SplitPropertyValues(objStr);

            foreach (var prop in properties)
            {
                var colonIndex = prop.IndexOf(':');
                if (colonIndex == -1) continue;

                string key = prop.Substring(0, colonIndex).Trim().Trim('"');
                string rawValue = prop.Substring(colonIndex + 1).Trim();
                
                // Check for null value
                if (rawValue == "null")
                {
                    switch (key)
                    {
                        case "LastStart":
                            app.LastStart = null;
                            break;
                        case "LastStop":
                            app.LastStop = null;
                            break;
                    }
                    continue;
                }
                
                // Remove surrounding quotes if present
                if (rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
                {
                    rawValue = rawValue.Substring(1, rawValue.Length - 2);
                }
                
                // Unescape the value
                string value = UnescapeJson(rawValue);

                switch (key)
                {
                    case "Index":
                        int idx;
                        if (int.TryParse(value, out idx))
                            app.Index = idx;
                        break;
                    case "AppName":
                        app.AppName = value;
                        break;
                    case "Directory":
                        app.Directory = value;
                        break;
                    case "AddedDate":
                        DateTime dt;
                        if (DateTime.TryParse(value, out dt))
                            app.AddedDate = dt;
                        break;
                    case "IsRunning":
                        app.IsRunning = value.ToLower() == "true";
                        break;
                    case "LastStart":
                        DateTime lastStart;
                        if (DateTime.TryParse(value, out lastStart))
                            app.LastStart = lastStart;
                        break;
                    case "LastStop":
                        DateTime lastStop;
                        if (DateTime.TryParse(value, out lastStop))
                            app.LastStop = lastStop;
                        break;
                }
            }

            return app;
        }

        private List<string> SplitPropertyValues(string content)
        {
            List<string> properties = new List<string>();
            StringBuilder currentProp = new StringBuilder();
            bool inQuotes = false;
            
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                
                if (c == '"' && (i == 0 || content[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    currentProp.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    if (currentProp.Length > 0)
                    {
                        properties.Add(currentProp.ToString());
                        currentProp.Clear();
                    }
                }
                else
                {
                    currentProp.Append(c);
                }
            }
            
            if (currentProp.Length > 0)
            {
                properties.Add(currentProp.ToString());
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
            // Unescape in correct order: first handle escaped backslashes, then other escapes
            return value.Replace("\\\\", "\x00")  // Temporarily replace \\ with a placeholder
                        .Replace("\\\"", "\"")      // Replace escaped quotes
                        .Replace("\\n", "\n")       // Replace escaped newlines
                        .Replace("\\r", "\r")       // Replace escaped carriage returns
                        .Replace("\x00", "\\");     // Replace placeholder with single backslash
        }
    }
}
