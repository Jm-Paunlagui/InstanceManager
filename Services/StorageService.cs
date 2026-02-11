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
        private readonly string _groupStoragePath;
        private List<ManagedApplication> _applications;
        private List<ApplicationGroup> _groups;

        // Dirty tracking to avoid writing to disk when nothing changed
        private bool _appsDirty;
        private bool _groupsDirty;
        private DateTime _lastAppSave = DateTime.MinValue;
        private DateTime _lastGroupSave = DateTime.MinValue;
        private const int MinSaveIntervalSeconds = 10;

        public StorageService()
        {
            _storagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "applications.json");
            _groupStoragePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "groups.json");
            _applications = new List<ManagedApplication>();
            _groups = new List<ApplicationGroup>();
            LoadGroups();
            LoadApplications();
        }

        // ===== Group Operations =====

        public List<ApplicationGroup> GetAllGroups()
        {
            return _groups.ToList();
        }

        public ApplicationGroup GetGroup(int groupId)
        {
            return _groups.FirstOrDefault(g => g.GroupId == groupId);
        }

        public void AddGroup(ApplicationGroup group)
        {
            group.GroupId = _groups.Count > 0 ? _groups.Max(g => g.GroupId) + 1 : 1;
            _groups.Add(group);
            SaveGroups();
            SimpleLogger.Info("AddGroup @ StorageService.cs", $"Added group: {group.GroupName} (GroupId: {group.GroupId})");
        }

        public void UpdateGroup(ApplicationGroup group)
        {
            var existing = _groups.FirstOrDefault(g => g.GroupId == group.GroupId);
            if (existing != null)
            {
                existing.GroupName = group.GroupName;
                SaveGroups();
                SimpleLogger.Info("UpdateGroup @ StorageService.cs", $"Updated group: {group.GroupName} (GroupId: {group.GroupId})");
            }
        }

        public void RemoveGroup(int groupId)
        {
            var group = _groups.FirstOrDefault(g => g.GroupId == groupId);
            if (group != null)
            {
                // Remove all applications in this group
                var appsInGroup = _applications.Where(a => a.GroupId == groupId).ToList();
                foreach (var app in appsInGroup)
                {
                    _applications.Remove(app);
                }
                _groups.Remove(group);
                SaveGroups();
                SaveApplications();
                SimpleLogger.Info("RemoveGroup @ StorageService.cs",
                    $"Removed group: {group.GroupName} (GroupId: {groupId}) and {appsInGroup.Count} application(s)");
            }
        }

        public bool GroupNameExists(string name, int excludeGroupId = -1)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return _groups.Any(g => g.GroupId != excludeGroupId &&
                !string.IsNullOrEmpty(g.GroupName) &&
                g.GroupName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        // ===== Application Operations =====

        public List<ManagedApplication> GetAllApplications()
        {
            return _applications.ToList();
        }

        /// <summary>
        /// Returns a read-only reference to the internal applications list.
        /// Callers must NOT modify the returned list or its elements directly.
        /// Use this for read-heavy paths (like the timer tick) to avoid allocations.
        /// </summary>
        public IList<ManagedApplication> GetAllApplicationsReadOnly()
        {
            return _applications.AsReadOnly();
        }

        public List<ManagedApplication> GetApplicationsByGroup(int groupId)
        {
            return _applications.Where(a => a.GroupId == groupId).ToList();
        }

        public void AddApplication(ManagedApplication app)
        {
            app.Index = _applications.Count > 0 ? _applications.Max(a => a.Index) + 1 : 1;
            _applications.Add(app);
            SaveApplications();
            SimpleLogger.Info("AddApplication @ StorageService.cs", $"Added application: {app.AppName} (Index: {app.Index}, GroupId: {app.GroupId})");
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
            UpdateApplicationInternal(app, forceSave: true);
        }

        /// <summary>
        /// Updates only transient/runtime state (IsRunning, timestamps) without forcing an immediate disk save.
        /// Use this for frequent timer-tick updates to avoid disk thrashing.
        /// </summary>
        public void UpdateApplicationTransient(ManagedApplication app)
        {
            UpdateApplicationInternal(app, forceSave: false);
        }

        private void UpdateApplicationInternal(ManagedApplication app, bool forceSave)
        {
            var existingApp = _applications.FirstOrDefault(a => a.Index == app.Index);
            if (existingApp != null)
            {
                // Track whether any persistent (non-transient) field actually changed
                bool persistentChanged =
                    existingApp.AppName != app.AppName ||
                    existingApp.Directory != app.Directory ||
                    existingApp.GroupId != app.GroupId ||
                    existingApp.KeepOpen != app.KeepOpen ||
                    existingApp.CrashCount != app.CrashCount ||
                    existingApp.RetryCount != app.RetryCount ||
                    existingApp.MaxRetries != app.MaxRetries ||
                    existingApp.StartDelaySeconds != app.StartDelaySeconds ||
                    existingApp.StartupDelaySeconds != app.StartupDelaySeconds ||
                    existingApp.LastStart != app.LastStart ||
                    existingApp.LastStop != app.LastStop;

                existingApp.AppName = app.AppName;
                existingApp.Directory = app.Directory;
                existingApp.IsRunning = app.IsRunning;
                existingApp.LastStart = app.LastStart;
                existingApp.LastStop = app.LastStop;
                existingApp.GroupId = app.GroupId;
                existingApp.KeepOpen = app.KeepOpen;
                existingApp.CrashCount = app.CrashCount;
                existingApp.RetryCount = app.RetryCount;
                existingApp.MaxRetries = app.MaxRetries;
                existingApp.StartDelaySeconds = app.StartDelaySeconds;
                existingApp.StartupDelaySeconds = app.StartupDelaySeconds;

                if (persistentChanged)
                {
                    _appsDirty = true;
                }

                // forceSave: always write to disk immediately (user-initiated actions)
                // non-forceSave: throttled writes for timer-tick updates
                if (forceSave)
                {
                    SaveApplications();
                }
                else if (_appsDirty && (DateTime.Now - _lastAppSave).TotalSeconds >= MinSaveIntervalSeconds)
                {
                    SaveApplications();
                }

                if (forceSave)
                {
                    SimpleLogger.Debug("UpdateApplication @ StorageService.cs", $"Updated application: {app.AppName} (Index: {app.Index})");
                }
            }
        }

        /// <summary>
        /// Updates specific fields on an application by index without requiring the caller to
        /// hold a reference to the internal object. This avoids the bug where callers mutate
        /// the internal object and then UpdateApplicationTransient sees no changes.
        /// Only non-null parameters are applied.
        /// </summary>
        public void UpdateApplicationFields(int appIndex, bool? isRunning = null, DateTime? lastStart = null,
            DateTime? lastStop = null, int? crashCount = null, int? retryCount = null)
        {
            var existingApp = _applications.FirstOrDefault(a => a.Index == appIndex);
            if (existingApp == null) return;

            bool persistentChanged = false;

            if (isRunning.HasValue)
            {
                existingApp.IsRunning = isRunning.Value;
            }
            if (lastStart.HasValue)
            {
                if (existingApp.LastStart != lastStart.Value) persistentChanged = true;
                existingApp.LastStart = lastStart.Value;
            }
            if (lastStop.HasValue)
            {
                if (existingApp.LastStop != lastStop.Value) persistentChanged = true;
                existingApp.LastStop = lastStop.Value;
            }
            if (crashCount.HasValue)
            {
                if (existingApp.CrashCount != crashCount.Value) persistentChanged = true;
                existingApp.CrashCount = crashCount.Value;
            }
            if (retryCount.HasValue)
            {
                if (existingApp.RetryCount != retryCount.Value) persistentChanged = true;
                existingApp.RetryCount = retryCount.Value;
            }

            if (persistentChanged)
            {
                _appsDirty = true;
            }

            // Throttled save for timer-tick updates
            if (_appsDirty && (DateTime.Now - _lastAppSave).TotalSeconds >= MinSaveIntervalSeconds)
            {
                SaveApplications();
            }
        }

        /// <summary>
        /// Flushes any pending dirty data to disk. Should be called periodically
        /// and before application exit to ensure no data is lost.
        /// </summary>
        public void FlushPendingChanges()
        {
            if (_appsDirty)
            {
                SaveApplications();
            }
            if (_groupsDirty)
            {
                SaveGroups();
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

        public bool ApplicationExistsInGroup(string directory, int groupId)
        {
            if (string.IsNullOrEmpty(directory))
                return false;

            string normalizedPath = directory.ToLowerInvariant().Replace("/", "\\");

            return _applications.Any(app =>
                app.GroupId == groupId &&
                !string.IsNullOrEmpty(app.Directory) &&
                app.Directory.ToLowerInvariant().Replace("/", "\\") == normalizedPath);
        }

        // ===== Load/Save Groups =====

        private void LoadGroups()
        {
            try
            {
                string json = ReadFileWithFallback(_groupStoragePath);
                if (!string.IsNullOrEmpty(json))
                {
                    SimpleLogger.Debug("LoadGroups @ StorageService.cs", $"Loading groups JSON: {json.Substring(0, Math.Min(100, json.Length))}...");
                    _groups = DeserializeGroups(json);
                }
                else
                {
                    _groups = new List<ApplicationGroup>();
                }
                SimpleLogger.Info("LoadGroups @ StorageService.cs", $"Loaded {_groups.Count} groups from storage");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("LoadGroups @ StorageService.cs", $"Error loading groups: {ex.Message} | StackTrace: {ex.StackTrace}");
                _groups = new List<ApplicationGroup>();
            }
        }

        private void SaveGroups()
        {
            try
            {
                string json = SerializeGroups(_groups);
                WriteFileAtomically(_groupStoragePath, json);
                _groupsDirty = false;
                _lastGroupSave = DateTime.Now;
                SimpleLogger.Debug("SaveGroups @ StorageService.cs", $"Saved {_groups.Count} groups to storage");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("SaveGroups @ StorageService.cs", $"Error saving groups: {ex.Message}");
            }
        }

        private string SerializeGroups(List<ApplicationGroup> groups)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"GroupId\": {g.GroupId},");
                sb.AppendLine($"    \"GroupName\": \"{EscapeJson(g.GroupName)}\",");
                sb.AppendLine($"    \"CreatedDate\": \"{g.CreatedDate:yyyy-MM-ddTHH:mm:ss}\"");
                sb.Append("  }");
                if (i < groups.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        private List<ApplicationGroup> DeserializeGroups(string json)
        {
            List<ApplicationGroup> groups = new List<ApplicationGroup>();

            json = json.Trim();
            if (!json.StartsWith("[") || !json.EndsWith("]")) return groups;

            string content = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(content)) return groups;

            var objects = SplitJsonObjects(content);
            foreach (var objStr in objects)
            {
                try
                {
                    var group = ParseGroupObject(objStr);
                    if (group != null) groups.Add(group);
                }
                catch { }
            }

            return groups;
        }

        private ApplicationGroup ParseGroupObject(string objStr)
        {
            ApplicationGroup group = new ApplicationGroup();

            objStr = objStr.Trim().Trim('{', '}');
            var properties = SplitPropertyValues(objStr);

            foreach (var prop in properties)
            {
                var colonIndex = prop.IndexOf(':');
                if (colonIndex == -1) continue;

                string key = prop.Substring(0, colonIndex).Trim().Trim('"');
                string rawValue = prop.Substring(colonIndex + 1).Trim();

                if (rawValue == "null") continue;

                if (rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
                {
                    rawValue = rawValue.Substring(1, rawValue.Length - 2);
                }

                string value = UnescapeJson(rawValue);

                switch (key)
                {
                    case "GroupId":
                        int gid;
                        if (int.TryParse(value, out gid))
                            group.GroupId = gid;
                        break;
                    case "GroupName":
                        group.GroupName = value;
                        break;
                    case "CreatedDate":
                        DateTime dt;
                        if (DateTime.TryParse(value, out dt))
                            group.CreatedDate = dt;
                        break;
                }
            }

            return group;
        }

        // ===== Load/Save Applications =====

        private void LoadApplications()
        {
            try
            {
                string json = ReadFileWithFallback(_storagePath);
                if (!string.IsNullOrEmpty(json))
                {
                    SimpleLogger.Debug("LoadApplications @ StorageService.cs", $"Loading JSON: {json.Substring(0, Math.Min(100, json.Length))}...");
                    _applications = DeserializeApplications(json);
                }
                else
                {
                    _applications = new List<ManagedApplication>();
                }
                SimpleLogger.Info("LoadApplications @ StorageService.cs", $"Loaded {_applications.Count} applications from storage");

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
                WriteFileAtomically(_storagePath, json);
                _appsDirty = false;
                _lastAppSave = DateTime.Now;
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
                sb.AppendLine($"    \"GroupId\": {app.GroupId},");
                sb.AppendLine($"    \"AppName\": \"{EscapeJson(app.AppName)}\",");
                sb.AppendLine($"    \"Directory\": \"{EscapeJson(app.Directory)}\",");
                sb.AppendLine($"    \"AddedDate\": \"{app.AddedDate:yyyy-MM-ddTHH:mm:ss}\",");
                sb.AppendLine($"    \"IsRunning\": {app.IsRunning.ToString().ToLower()},");
                sb.AppendLine($"    \"LastStart\": {(app.LastStart.HasValue ? $"\"{app.LastStart.Value:yyyy-MM-ddTHH:mm:ss}\"" : "null")},");
                sb.AppendLine($"    \"LastStop\": {(app.LastStop.HasValue ? $"\"{app.LastStop.Value:yyyy-MM-ddTHH:mm:ss}\"" : "null")},");
                sb.AppendLine($"    \"KeepOpen\": {app.KeepOpen.ToString().ToLower()},");
                sb.AppendLine($"    \"CrashCount\": {app.CrashCount},");
                sb.AppendLine($"    \"RetryCount\": {app.RetryCount},");
                sb.AppendLine($"    \"MaxRetries\": {app.MaxRetries},");
                sb.AppendLine($"    \"StartDelaySeconds\": {app.StartDelaySeconds},");
                sb.AppendLine($"    \"StartupDelaySeconds\": {app.StartupDelaySeconds}");
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

            var properties = SplitPropertyValues(objStr);

            foreach (var prop in properties)
            {
                var colonIndex = prop.IndexOf(':');
                if (colonIndex == -1) continue;

                string key = prop.Substring(0, colonIndex).Trim().Trim('"');
                string rawValue = prop.Substring(colonIndex + 1).Trim();

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

                if (rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
                {
                    rawValue = rawValue.Substring(1, rawValue.Length - 2);
                }

                string value = UnescapeJson(rawValue);

                switch (key)
                {
                    case "Index":
                        int idx;
                        if (int.TryParse(value, out idx))
                            app.Index = idx;
                        break;
                    case "GroupId":
                        int gid;
                        if (int.TryParse(value, out gid))
                            app.GroupId = gid;
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
                    case "KeepOpen":
                        app.KeepOpen = value.ToLower() == "true";
                        break;
                    case "CrashCount":
                        int cc;
                        if (int.TryParse(value, out cc))
                            app.CrashCount = cc;
                        break;
                    case "RetryCount":
                        int rc;
                        if (int.TryParse(value, out rc))
                            app.RetryCount = rc;
                        break;
                    case "MaxRetries":
                        int mr;
                        if (int.TryParse(value, out mr))
                            app.MaxRetries = mr;
                        break;
                    case "StartDelaySeconds":
                        int sd;
                        if (int.TryParse(value, out sd))
                            app.StartDelaySeconds = sd;
                        break;
                    case "StartupDelaySeconds":
                        int sud;
                        if (int.TryParse(value, out sud))
                            app.StartupDelaySeconds = sud;
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
            return value.Replace("\\\\", "\x00")
                        .Replace("\\\"", "\"")
                        .Replace("\\n", "\n")
                        .Replace("\\r", "\r")
                        .Replace("\x00", "\\");
        }

        /// <summary>
        /// Writes content to a file atomically by writing to a temp file first,
        /// then replacing the target. This prevents data loss on power failure or crash.
        /// </summary>
        private void WriteFileAtomically(string targetPath, string content)
        {
            string tempPath = targetPath + ".tmp";
            string backupPath = targetPath + ".bak";

            // Write new content to temp file
            File.WriteAllText(tempPath, content, Encoding.UTF8);

            if (File.Exists(targetPath))
            {
                // Create backup of current file
                if (File.Exists(backupPath))
                {
                    try { File.Delete(backupPath); }
                    catch { /* non-critical */ }
                }

                try
                {
                    File.Copy(targetPath, backupPath, true);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Warn("WriteFileAtomically @ StorageService.cs",
                        $"Could not create backup for {targetPath}: {ex.Message}");
                }

                // Delete the original and move temp into place
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);

            // Clean up backup on success
            if (File.Exists(backupPath))
            {
                try { File.Delete(backupPath); }
                catch { /* non-critical */ }
            }
        }

        /// <summary>
        /// Reads a file, falling back to .bak if the primary file is missing or corrupted (empty/invalid).
        /// </summary>
        private string ReadFileWithFallback(string path)
        {
            // Try primary file first
            if (File.Exists(path))
            {
                string content = File.ReadAllText(path);
                if (!string.IsNullOrEmpty(content) && content.Trim().Length > 2)
                {
                    return content;
                }
                SimpleLogger.Warn("ReadFileWithFallback @ StorageService.cs",
                    $"Primary file is empty or corrupted: {path}");
            }

            // Try backup file
            string backupPath = path + ".bak";
            if (File.Exists(backupPath))
            {
                string content = File.ReadAllText(backupPath);
                if (!string.IsNullOrEmpty(content) && content.Trim().Length > 2)
                {
                    SimpleLogger.Warn("ReadFileWithFallback @ StorageService.cs",
                        $"Recovered from backup file: {backupPath}");
                    // Restore backup as primary
                    try { File.Copy(backupPath, path, true); }
                    catch { /* non-critical */ }
                    return content;
                }
            }

            // Try temp file (write was interrupted before move)
            string tempPath = path + ".tmp";
            if (File.Exists(tempPath))
            {
                string content = File.ReadAllText(tempPath);
                if (!string.IsNullOrEmpty(content) && content.Trim().Length > 2)
                {
                    SimpleLogger.Warn("ReadFileWithFallback @ StorageService.cs",
                        $"Recovered from temp file: {tempPath}");
                    try { File.Copy(tempPath, path, true); }
                    catch { /* non-critical */ }
                    return content;
                }
            }

            return null;
        }
    }
}
