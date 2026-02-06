# Intelligent Mutex Execution Environment

## Overview

This application prevents simultaneous opening of applications through intelligent instance management. It provides a centralized interface to manage, monitor, and control multiple applications across LRA reflow sorter lines.

The core algorithm — an **Authorized Process Watchdog** — not only prevents duplicate launches through the UI, but also detects and terminates managed applications launched externally (e.g., double-clicking the .exe, shortcuts, Start menu).

## Features

### Core Functionality
- **Add Applications**: Browse and add executable files to the manager (with duplicate detection)
- **Edit Applications**: Update application paths (with duplicate detection on new path)
- **Delete Applications**: Remove applications from management (doesn't delete the actual file)
- **Start Applications**: Launch applications with instance checking, records Last Start timestamp
- **Stop Applications**: Gracefully or forcefully terminate running applications, records Last Stop timestamp
- **Refresh**: Reload all applications from JSON storage
- **Real-time Monitoring**: Automatic status updates every 2 seconds

### Authorized Process Watchdog
- **Unauthorized Launch Detection**: If a managed application is launched outside of Instance Manager (double-click, shortcut, etc.), it is automatically killed within 2 seconds and the user is notified
- **Authorization Tracking**: Only applications started through the Start button are authorized to run
- **Graceful Onboarding**: Applications already running when Instance Manager starts are marked as authorized (not killed)
- **External Stop Detection**: When a running application is closed externally, the status updates automatically and Last Stop is recorded
- **Debounced Notifications**: User is warned only once per unauthorized launch attempt

### Data Storage
- Applications are stored in JSON format (`applications.json`)
- No database required — lightweight file-based storage
- Stores: Index, AppName, Directory, AddedDate, IsRunning, LastStart, LastStop
- Nullable DateTime support (`null` for never-started/never-stopped)
- Custom JSON serializer with proper Windows path escaping (`\\`)
- Backward compatible — loads old JSON files missing new fields

### Process Management
- Checks if application is already running before starting
- Prevents multiple instances (mutex enforcement)
- Detects and kills unauthorized external launches
- Graceful shutdown via `CloseMainWindow()` with 3-second timeout
- Force kill fallback via `Process.Kill()`
- Real-time process monitoring with minimal CPU impact

### Logging (Custom SimpleLogger)
All actions are logged with a special format:

```
[MACHINE_IDENTIFIER][TIMESTAMP][LEVEL][PID:processId][FUNCTION @ FILE:LINE] - MESSAGE
```

**Examples:**
```
[JmPaunlagui/user 192.168.1.100][2025-12-03 14:30:00][INFO][PID:23632][StartApplication @ ProcessManager.cs] - Successfully started DryCabinet (PID: 9876)
[JmPaunlagui/user 192.168.1.100][2025-12-03 14:30:05][WARN][PID:23632][HandleUnauthorizedLaunch @ Form1.cs] - Unauthorized launch detected for 'EXCEL' - killing process
```

**Logged Events:**
- Application startup/shutdown
- Add/Edit/Delete operations
- Start/Stop application attempts
- Unauthorized external launch detection and termination
- External stop detection
- Duplicate application warnings
- Process status checks
- All errors and warnings

**Log Location:** `logs/YYYY-MM-DD.log` (auto-generated)

**Log Levels:** INFO, DEBUG, WARN, ERROR, FATAL

## Architecture

### Files Structure
```
InstanceManager/
??? Form1.cs                          Main UI logic, Authorized Process Watchdog
??? Form1.Designer.cs                 UI control definitions (6-column ListView)
??? Form1.resx                        Form resources
??? Program.cs                        Application entry point with logging
??? Models/
?   ??? ManagedApplication.cs         Data model (Index, AppName, Directory, LastStart, LastStop)
??? Services/
?   ??? StorageService.cs             JSON storage with custom serialization + duplicate checking
?   ??? ProcessManager.cs             Process monitoring, start, stop, instance counting
??? Utilities/
?   ??? SimpleLogger.cs               Thread-safe custom logging (zero dependencies)
?   ??? CustomMessageBox.cs           Custom dialog positioned relative to owner form
?   ??? MessageBoxHelper.cs           Convenience wrappers (ShowSuccess, ShowError, etc.)
??? Properties/
?   ??? AssemblyInfo.cs               Assembly metadata
?   ??? Resources.Designer.cs         Resource accessors (Logo)
?   ??? Settings.Designer.cs          Application settings
??? packages.config                   Package references (empty — no external dependencies)
??? NLog.config                       Configuration reference (not actively used)
??? applications.json                 Auto-generated application data storage
??? logs/                             Auto-generated daily log files
```

### Key Classes

**ManagedApplication**
- Properties: Index, AppName, Directory, AddedDate, IsRunning, LastStart, LastStop
- Helper methods: `GetLastStartDisplay()`, `GetLastStopDisplay()` (returns "Never" or formatted timestamp)

**StorageService**
- Custom JSON serialization/deserialization (.NET 4.0 compatible)
- Proper path escaping/unescaping for Windows paths (`\\` ? `\`)
- CRUD operations for applications
- Duplicate detection via `ApplicationExists()` (case-insensitive, path-normalized)

**ProcessManager**
- Check if application is running via `Process.GetProcessesByName()`
- Start application with instance validation
- Stop application (graceful with 3-second timeout, fallback to force kill)
- Get running instance count

**SimpleLogger**
- Thread-safe logging with `lock` mechanism
- Automatic `logs/` directory creation
- Daily log file rotation (YYYY-MM-DD.log)
- Custom log format with machine identifier, timestamp, PID, and location
- Silent failure — never crashes the application

**CustomMessageBox**
- Custom dialog form positioned relative to owner form
- Screen boundary protection
- Supports OK and Yes/No buttons with system icons
- Keyboard support (Enter/Escape)

**MessageBoxHelper**
- Static convenience methods: `ShowSuccess`, `ShowError`, `ShowWarning`, `ShowInfo`, `ShowQuestion`
- Routes to `CustomMessageBox` when owner form is available

## Authorized Process Watchdog — Algorithm

The core security mechanism that enforces the mutex:

```
Every 2 seconds (timer tick):
  For each managed application:

    Was Stopped ? Now Running?
      ??? In _authorizedApps? ? ? Authorized, update status
      ??? NOT in _authorizedApps? ? ? UNAUTHORIZED
            ??? Kill process immediately
            ??? Log warning
            ??? Notify user (once per attempt)

    Was Running ? Now Stopped?
      ??? Remove from _authorizedApps
      ??? Record LastStop timestamp
      ??? Update UI and storage

    No change? ? Refresh display
```

### Authorization Rules
| Action | Effect |
|--------|--------|
| Instance Manager starts, app already running | Marked as authorized |
| User clicks Start | Marked as authorized |
| User clicks Stop | Authorization removed |
| User clicks Delete | Authorization removed |
| App stopped externally | Authorization removed |
| App launched externally | **Killed + user warned** |

## Performance Considerations

### Minimal CPU Impact
- Timer-based status updates (2-second interval)
- Efficient process name checking via `Process.GetProcessesByName()`
- Updates only changed ListView items (no full refresh on tick)
- Lightweight JSON storage (no database overhead)
- Thread-safe logging with minimal lock contention
- `BeginInvoke` for non-blocking unauthorized launch notifications

### Memory Efficiency
- Proper disposal of Process objects in `finally` blocks
- Timer cleanup on form close
- Minimal memory footprint (< 50 MB)
- No external dependencies

## Usage

### Adding an Application
1. Click "Add" button
2. Browse to the executable (.exe) file
3. Application is automatically added with index and name
4. **Note:** If the application path already exists, you'll see a warning and it won't be added

### Starting an Application
1. Select an application from the list
2. Click "Start" button
3. System checks if already running
4. If not running, launches the application
5. Status updates to "Running" (green), Last Start is recorded
6. Application is marked as authorized in the watchdog

### Stopping an Application
1. Select a running application
2. Click "Stop" button
3. Confirmation dialog appears
4. System attempts graceful shutdown (3-second timeout)
5. Force kills if graceful shutdown fails
6. Last Stop is recorded, authorization is removed

### Editing an Application
1. Select an application
2. Click "Edit" button
3. Browse to new executable path
4. Application details are updated
5. **Note:** Cannot edit to a path that already exists in the list

### Deleting an Application
1. Select an application
2. Click "Delete" button
3. Confirm deletion
4. Application removed from management (file not deleted)
5. Authorization tracking is cleaned up

### Unauthorized Launch Behavior
1. User adds `DryCabinet.exe` to Instance Manager
2. User double-clicks `DryCabinet.exe` directly (bypassing Instance Manager)
3. Within 2 seconds, Instance Manager detects the unauthorized launch
4. Process is killed automatically
5. User sees warning: "DryCabinet was launched outside of Instance Manager and has been terminated."

## Technical Details

### .NET Framework
- Target: .NET Framework 4.0
- C# 7.3
- Windows Forms application
- Compatible with Windows XP SP3 and above
- **Zero** external dependencies

### Custom JSON Serialization
- Built from scratch for .NET 4.0 compatibility
- Placeholder-based unescaping for Windows paths (`\\` ? `\`)
- Quote-aware property splitting (handles commas inside strings)
- Nullable DateTime support (`null` / `"2026-02-05T17:16:25"`)
- Backward compatible with older JSON files

### Logging Format Components
1. **MACHINE_IDENTIFIER**: `[hostname/username ip]`
2. **TIMESTAMP**: `[YYYY-MM-DD HH:MM:SS]`
3. **LEVEL**: `[INFO|ERROR|WARN|DEBUG|FATAL]`
4. **PID**: `[PID:12345]`
5. **LOCATION**: `[functionName @ file.cs]`
6. **MESSAGE**: The actual log message

## Error Handling

### Comprehensive Logging
- All exceptions are caught and logged with context
- User-friendly error messages via CustomMessageBox
- Detailed technical logs for debugging
- Silent log failures to prevent application crashes

### Foolproof Design
- File existence checks before launching applications
- Process validation before start/stop
- Duplicate detection on Add and Edit
- Path normalization for case-insensitive comparison
- Graceful degradation on failures
- Thread-safe operations
- Unauthorized launch enforcement

## Build and Deployment

### Requirements
- Visual Studio 2010 or later
- .NET Framework 4.0 or higher
- Windows XP SP3 or later

### Building
1. Open the solution in Visual Studio
2. Build the project (F6 or Ctrl+Shift+B)
3. Executable will be in `bin\Debug` or `bin\Release`

### Deployment
- Copy the executable to target machine
- No installation required — fully portable
- `applications.json` and `logs/` are created automatically in the same directory

## Future Enhancements
- CSV export/import functionality
- System tray minimization
- Auto-start with Windows
- Application categories/groups
- Command-line interface
- Remote monitoring capabilities
- Application crash detection and auto-restart
- Performance metrics and reporting
- Configurable watchdog interval
- Whitelist/blacklist mode toggle

## License
Internal LRA Reflow Sorter Tool

## Author
Developed for LRA Application Instance Management

## Repository
https://github.com/Jm-Paunlagui/InstanceManager
