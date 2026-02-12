# Instance Manager

## Overview

A lightweight Windows Forms application for centralized management, monitoring, and control of multiple applications organized into groups. Designed to run on low-end PCs while monitoring 10+ applications per group with minimal CPU impact.

The core algorithm — an **Authorized Process Watchdog** — prevents duplicate launches, detects and terminates managed applications launched externally, and automatically restarts crashed applications when configured.

## Features

### Group Management
- **Create Groups**: Organize applications into logical groups (e.g., by production line, station)
- **Edit Groups**: Rename groups with duplicate name detection
- **Delete Groups**: Remove groups and all associated applications (running apps are stopped first)
- **Station Name**: Configure a station identifier displayed in the title bar

### Application Management
- **Add Applications**: Browse and add executable files to a group (with duplicate detection per group)
- **Edit Applications**: Update application paths and settings (Keep Open, delays, counter resets)
- **Delete Applications**: Remove applications from management (does not delete the actual file)
- **Start / Start All**: Launch applications with visual "Starting..." ? "Running" transition
- **Stop / Stop All**: Gracefully or forcefully terminate applications with "Stopping..." ? "Stopped" transition
- **Sequential Start All**: When apps have startup delays configured, they launch one-by-one with countdown timers

### Authorized Process Watchdog
- **Startup Enforcement**: Applications already running when Instance Manager starts are terminated
- **Unauthorized Launch Detection**: Applications launched outside Instance Manager are killed within 5 seconds
- **Authorization Tracking**: Only applications started through Instance Manager are allowed to run
- **External Stop Detection**: When a running application is closed externally, the status updates automatically
- **Debounced Notifications**: User is warned only once per unauthorized launch attempt

### Crash Recovery (Keep Open)
- **Auto-Restart on Crash**: When Keep Open is enabled, crashed applications are automatically restarted
- **Configurable Restart Delay**: Per-application delay (1–300 seconds) before auto-restart
- **Max Retries**: Configurable maximum restart attempts (default: 3) before marking as "Failed"
- **Live Countdown**: Restart countdown updates every second in the status column
- **Startup Grace Period**: 10-second window after launch before watchdog monitoring begins
- **Crash & Retry Tracking**: Counts are persisted and can be reset via the Edit dialog

### Status Transitions
| Status | Color | Meaning |
|--------|-------|---------|
| **Stopped** | Black | Application is not running |
| **Starting...** | Orange | Application was just launched, waiting for window to appear |
| **Running** | Green | Application window detected and process is active |
| **Stopping...** | Orange | Stop command sent, waiting for process to exit |
| **Restarting (Ns)** | Orange | Crashed, auto-restart countdown in progress |
| **Starting...** | Orange | Restart countdown finished, launch in progress |
| **Waiting (Ns)** | Orange | Sequential Start All delay countdown |
| **Failed** | Red | Max retries exhausted or restart failed |

### Performance Optimizations
- **Batch Process Snapshot**: Single `Process.GetProcesses()` call for all apps instead of N individual `GetProcessesByName()` calls
- **O(1) ListView Lookup**: Dictionary-indexed item lookup instead of O(n) linear scan per app
- **5-Second Poll Interval**: Balanced between responsiveness and CPU efficiency
- **1-Second Countdown Timer**: Dedicated timer for smooth countdown display (only runs when needed)
- **Throttled Storage Writes**: Batched file I/O with minimum save intervals
- **Atomic File Writes**: Write-to-temp-then-rename pattern prevents data corruption

### Data Storage
- **Applications**: `applications.json` — per-app settings, timestamps, and runtime state
- **Groups**: `groups.json` — group definitions
- **Settings**: `settings.json` — station name configuration
- **Logs**: `logs/YYYY-MM-DD.log` — daily rotating log files
- No database required — fully file-based with atomic writes

### Logging
All actions are logged in the format:
```
[HOSTNAME/USER IP][TIMESTAMP][LEVEL][PID:id][LOCATION] - MESSAGE
```

**Log Features:**
- Buffered writes (flush every 5 seconds or 50 entries)
- Immediate flush on ERROR/FATAL
- Automatic cleanup of logs older than 30 days
- Machine info cached at startup (no repeated DNS lookups)

## Architecture

### File Structure
```
InstanceManager/
??? Form1.cs                          Main UI, watchdog, crash recovery, group management
??? Form1.Designer.cs                 UI control definitions (9-column ListView)
??? Form1.resx                        Form resources
??? Program.cs                        Application entry point
??? Models/
?   ??? ManagedApplication.cs         App model (Index, GroupId, KeepOpen, delays, counters)
?   ??? ApplicationGroup.cs           Group model (GroupId, GroupName, CreatedDate)
??? Services/
?   ??? StorageService.cs             JSON CRUD with throttled saves and atomic writes
?   ??? ProcessManager.cs             Batch snapshots, start, stop, background process cleanup
?   ??? SettingsService.cs            Station name persistence
??? Utilities/
?   ??? SimpleLogger.cs               Buffered thread-safe logging with auto-cleanup
?   ??? CustomMessageBox.cs           Owner-relative positioned dialogs
?   ??? MessageBoxHelper.cs           Convenience wrappers
?   ??? InputDialog.cs                Text input dialog (group names, station name)
?   ??? AppSettingsDialog.cs          Per-app settings (Keep Open, delays, counter reset)
??? Properties/
?   ??? AssemblyInfo.cs               Assembly metadata
?   ??? Resources.Designer.cs         Resource accessors
?   ??? Settings.Designer.cs          Application settings
??? applications.json                 Auto-generated app data
??? groups.json                       Auto-generated group data
??? settings.json                     Auto-generated settings
??? logs/                             Auto-generated daily log files
```

### Key Classes

| Class | Responsibility |
|-------|---------------|
| `Main` (Form1) | UI, timers, watchdog logic, group/app management |
| `ManagedApplication` | Data model with display helpers |
| `ApplicationGroup` | Group data model |
| `StorageService` | JSON persistence with throttling and atomic writes |
| `ProcessManager` | Batch process snapshots, start/stop/kill operations |
| `SettingsService` | Station name load/save |
| `SimpleLogger` | Buffered, thread-safe daily log files |

### Watchdog Algorithm

```
Every 5 seconds (StatusUpdateTimer_Tick):
  1. Get batch process snapshot (single Process.GetProcesses() call)
  2. Build ListView index dictionary for O(1) lookups
  3. For each managed application across ALL groups:

     Pending Restart?
       ? Timer expired? ? AttemptAutoRestart
       ? Otherwise: skip (countdown handled by 1-second timer)

     Pending Stop?
       ? Process exited? ? Clean up, show "Stopped"
       ? Still running? ? Show "Stopping..."

     Failed (max retries)?
       ? Show "Failed" in red

     In Grace Period?
       ? Window appeared? ? Show "Running"
       ? Still waiting? ? Show "Starting..."

     Was Running ? Now Stopped? (crash or external close)
       ? KeepOpen? ? Schedule restart with countdown
       ? No KeepOpen? ? Record stop time

     Was Stopped ? Now Running? (unauthorized launch)
       ? Not in authorized set? ? Kill + notify user

     State changed? ? Update storage
     Update ListView display
```

## Technical Details

| Detail | Value |
|--------|-------|
| .NET Target | .NET Framework 4.0 |
| C# Version | 7.3 |
| UI Framework | Windows Forms |
| External Dependencies | **0** (zero) |
| Status Poll Interval | 5 seconds |
| Countdown Timer Interval | 1 second |
| Graceful Shutdown Timeout | 3 seconds |
| Startup Grace Period | 10 seconds |
| Default Max Retries | 3 |
| Default Restart Delay | 5 seconds |
| Log Buffer Size | 50 entries |
| Log Flush Interval | 5 seconds |
| Log Retention | 30 days |

## Build and Deployment

### Requirements
- Visual Studio 2010 or later
- .NET Framework 4.0 or higher
- Windows XP SP3 or later

### Building
1. Open `InstanceManager.sln` in Visual Studio
2. Build (F6 or Ctrl+Shift+B)
3. Output: `bin\Debug\` or `bin\Release\`

### Deployment
- Copy the executable to the target machine
- No installation required — fully portable
- `applications.json`, `groups.json`, `settings.json`, and `logs/` are created automatically

## Repository
https://github.com/Jm-Paunlagui/InstanceManager
