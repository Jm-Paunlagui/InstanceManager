using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IntelligentMutexExecutionEnvironment.Models;
using IntelligentMutexExecutionEnvironment.Utilities;

namespace IntelligentMutexExecutionEnvironment.Services
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
        private int _minSaveIntervalSeconds = 30;

        /// <summary>
        /// Updates the minimum save interval for throttled writes at runtime.
        /// </summary>
        public void SetSaveInterval(int seconds)
        {
            _minSaveIntervalSeconds = Math.Max(5, seconds);
        }

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
                    existingApp.StableRunPeriodSeconds != app.StableRunPeriodSeconds ||
                    existingApp.NotRespondingTimeoutSeconds != app.NotRespondingTimeoutSeconds ||
                    existingApp.MemoryLimitMB != app.MemoryLimitMB ||
                    existingApp.LastExitCode != app.LastExitCode ||
                    existingApp.DetectTitleChange != app.DetectTitleChange ||
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
                existingApp.StableRunPeriodSeconds = app.StableRunPeriodSeconds;
                existingApp.NotRespondingTimeoutSeconds = app.NotRespondingTimeoutSeconds;
                existingApp.MemoryLimitMB = app.MemoryLimitMB;
                existingApp.LastExitCode = app.LastExitCode;
                existingApp.DetectTitleChange = app.DetectTitleChange;

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
                else if (_appsDirty && (DateTime.Now - _lastAppSave).TotalSeconds >= _minSaveIntervalSeconds)
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
            DateTime? lastStop = null, int? crashCount = null, int? retryCount = null, int? lastExitCode = null)
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
            if (lastExitCode.HasValue)
            {
                if (existingApp.LastExitCode != lastExitCode.Value) persistentChanged = true;
                existingApp.LastExitCode = lastExitCode.Value;
            }

            if (persistentChanged)
            {
                _appsDirty = true;
            }

            // Throttled save for timer-tick updates
            if (_appsDirty && (DateTime.Now - _lastAppSave).TotalSeconds >= _minSaveIntervalSeconds)
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

        /// <summary>
        /// Finds all other application entries that share the same executable path
        /// but have a different Index. Used to detect cross-group duplicates.
        /// </summary>
        public List<ManagedApplication> FindOtherEntriesWithSamePath(string directory, int excludeIndex)
        {
            var result = new List<ManagedApplication>();
            if (string.IsNullOrEmpty(directory))
                return result;

            string normalizedPath = directory.ToLowerInvariant().Replace("/", "\\");

            foreach (var app in _applications)
            {
                if (app.Index == excludeIndex)
                    continue;
                if (string.IsNullOrEmpty(app.Directory))
                    continue;
                if (app.Directory.ToLowerInvariant().Replace("/", "\\") == normalizedPath)
                {
                    result.Add(app);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks whether any other application entry with the same executable path
        /// exists in the authorized set. Returns the first matching authorized sibling,
        /// or null if none found. This avoids allocating a List for the common case
        /// in the timer tick where we only need to know if a cross-group match exists.
        /// </summary>
        public ManagedApplication FindAuthorizedSibling(string directory, int excludeIndex, HashSet<int> authorizedApps)
        {
            if (string.IsNullOrEmpty(directory) || authorizedApps.Count == 0)
                return null;

            string normalizedPath = directory.ToLowerInvariant().Replace("/", "\\");

            foreach (var app in _applications)
            {
                if (app.Index == excludeIndex)
                    continue;
                if (!authorizedApps.Contains(app.Index))
                    continue;
                if (string.IsNullOrEmpty(app.Directory))
                    continue;
                if (app.Directory.ToLowerInvariant().Replace("/", "\\") == normalizedPath)
                {
                    return app;
                }
            }

            return null;
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
                sb.AppendLine($"    \"StartupDelaySeconds\": {app.StartupDelaySeconds},");
                sb.AppendLine($"    \"StableRunPeriodSeconds\": {app.StableRunPeriodSeconds},");
                sb.AppendLine($"    \"NotRespondingTimeoutSeconds\": {app.NotRespondingTimeoutSeconds},");
                sb.AppendLine($"    \"MemoryLimitMB\": {app.MemoryLimitMB},");
                sb.AppendLine($"    \"DetectTitleChange\": {app.DetectTitleChange.ToString().ToLower()},");
                sb.AppendLine($"    \"LastExitCode\": {app.LastExitCode}");
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
                catch (Exception ex)
                {
                    SimpleLogger.Error("DeserializeApplications", $"Error parsing application object: {ex.Message} | {objStr}");
                }
            }

            return apps;
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

                if (rawValue == "null") continue;

                if (rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
                {
                    rawValue = rawValue.Substring(1, rawValue.Length - 2);
                }

                string value = UnescapeJson(rawValue);

                switch (key)
                {
                    case "Index":
                        int index;
                        if (int.TryParse(value, out index))
                            app.Index = index;
                        break;
                    case "GroupId":
                        int groupId;
                        if (int.TryParse(value, out groupId))
                            app.GroupId = groupId;
                        break;
                    case "AppName":
                        app.AppName = value;
                        break;
                    case "Directory":
                        app.Directory = value;
                        break;
                    case "AddedDate":
                        DateTime addedDate;
                        if (DateTime.TryParse(value, out addedDate))
                            app.AddedDate = addedDate;
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
                        int crashCount;
                        if (int.TryParse(value, out crashCount))
                            app.CrashCount = crashCount;
                        break;
                    case "RetryCount":
                        int retryCount;
                        if (int.TryParse(value, out retryCount))
                            app.RetryCount = retryCount;
                        break;
                    case "MaxRetries":
                        int maxRetries;
                        if (int.TryParse(value, out maxRetries))
                            app.MaxRetries = maxRetries;
                        break;
                    case "StartDelaySeconds":
                        int startDelay;
                        if (int.TryParse(value, out startDelay))
                            app.StartDelaySeconds = startDelay;
                        break;
                    case "StartupDelaySeconds":
                        int startupDelay;
                        if (int.TryParse(value, out startupDelay))
                            app.StartupDelaySeconds = startupDelay;
                        break;
                    case "StableRunPeriodSeconds":
                        int stableRunPeriod;
                        if (int.TryParse(value, out stableRunPeriod))
                            app.StableRunPeriodSeconds = stableRunPeriod;
                        break;
                    case "NotRespondingTimeoutSeconds":
                        int notRespondingTimeout;
                        if (int.TryParse(value, out notRespondingTimeout))
                            app.NotRespondingTimeoutSeconds = notRespondingTimeout;
                        break;
                    case "MemoryLimitMB":
                        int memoryLimit;
                        if (int.TryParse(value, out memoryLimit))
                            app.MemoryLimitMB = memoryLimit;
                        break;
                    case "DetectTitleChange":
                        app.DetectTitleChange = value.ToLower() == "true";
                        break;
                    case "LastExitCode":
                        int lastExitCode;
                        if (int.TryParse(value, out lastExitCode))
                            app.LastExitCode = lastExitCode;
                        break;
                }
            }

            return app;
        }

        private string ReadFileWithFallback(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("ReadFileWithFallback", $"Error reading file {path}: {ex.Message}");
            }

            return null;
        }

        private void WriteFileAtomically(string path, string content)
        {
            try
            {
                string tempPath = path + ".tmp";
                File.WriteAllText(tempPath, content);
                File.Replace(tempPath, path, null);
            }
            catch (Exception ex)
            {
                SimpleLogger.Error("WriteFileAtomically", $"Error writing file {path}: {ex.Message}");
            }
        }

        private List<string> SplitJsonObjects(string json)
        {
            List<string> objects = new List<string>();

            int bracketDepth = 0;
            int startIndex = 0;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (c == '{') {
                    if (bracketDepth == 0) startIndex = i;
                    bracketDepth++;
                }
                else if (c == '}') {
                    bracketDepth--;
                    if (bracketDepth == 0) {
                        objects.Add(json.Substring(startIndex, i - startIndex + 1));
                    }
                }
            }

            return objects;
        }

        private List<string> SplitPropertyValues(string objStr)
        {
            List<string> properties = new List<string>();

            int braceDepth = 0;
            int startIndex = 0;

            for (int i = 0; i < objStr.Length; i++)
            {
                char c = objStr[i];

                if (c == '{') braceDepth++;
                else if (c == '}') braceDepth--;
                else if (c == ',' && braceDepth == 0)
                {
                    properties.Add(objStr.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                }
            }

            // Add the last property if not empty
            if (startIndex < objStr.Length)
            {
                properties.Add(objStr.Substring(startIndex));
            }

            return properties;
        }

        private string EscapeJson(string input)
        {
            return input.Replace("\"", "\\\"");
        }

        private string UnescapeJson(string input)
        {
            return input.Replace("\\\"", "\"");
        }
    }
}
