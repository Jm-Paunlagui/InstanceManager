# Instance Manager

## Overview

A lightweight Windows Forms application for centralized management, monitoring, and control of multiple applications organized into groups. Designed to run on low-end PCs while monitoring 10+ applications per group with minimal CPU impact.

The core algorithm — an **Authorized Process Watchdog** — prevents duplicate launches, detects and terminates managed applications launched externally, monitors application health, and automatically restarts crashed applications when configured.

## Features

### Group Management
- **Create Groups**: Organize applications into logical groups (e.g., by production line, station)
- **Edit Groups**: Rename groups with duplicate name detection
- **Delete Groups**: Remove groups and all associated applications (running apps are stopped first)
- **Group Status Indicators**: Owner-drawn colored circles show aggregate group health at a glance (gray/green/orange/red with count suffixes)

### Application Management
- **Add Applications**: Browse and add executable files to a group (with duplicate detection per group)
- **Edit Applications**: Update paths, crash recovery, health monitoring, and reset counters
- **Delete Applications**: Remove applications from management (does not delete the actual file)
- **Start / Start All**: Launch applications with visual "Starting..." → "Running" transition
- **Stop / Stop All**: Gracefully or forcefully terminate applications with "Stopping..." → "Stopped" transition
- **Sequential Start All**: When apps have startup delays configured, they launch one-by-one with countdown timers
- **Cross-Group Detection**: Same executable in multiple groups shows "Running (Other Group)" status

### Authorized Process Watchdog
- **Startup Enforcement**: Applications already running when Instance Manager starts are terminated
- **Unauthorized Launch Detection**: Applications launched outside Instance Manager are killed within one poll interval (default: 10 seconds)
- **Authorization Tracking**: Only applications started through Instance Manager are allowed to run
- **Cross-Group Awareness**: Same executable started from another group is recognized, not treated as unauthorized
- **External Stop Detection**: When a running application is closed externally, the status updates automatically
- **Debounced Notifications**: User is warned only once per unauthorized launch attempt

### Crash Recovery (Keep Open)
- **Auto-Restart on Crash**: When Keep Open is enabled, crashed applications are automatically restarted
- **Configurable Restart Delay**: Per-application delay (1–300 seconds) before auto-restart
- **Max Retries**: Configurable maximum restart attempts (default: 3) before marking as "Failed"
- **Live Countdown**: Restart countdown updates every second in the status column
- **Startup Grace Period**: Configurable window after launch before watchdog monitoring begins (default: 10 seconds)
- **Stable Run Period**: Retry count resets only after the app runs stably for a configurable duration (default: 30 seconds)
- **Crash & Retry Tracking**: Counts are persisted and can be reset via the Edit dialog
- **Last Exit Code**: Recorded and displayed in the Edit dialog (non-zero shown as "abnormal")

### Health Monitoring
All health monitoring applies only to Keep Open applications and triggers the same auto-restart flow as a crash.

- **Not Responding Detection**: Detects frozen UI threads via `Process.Responding`. Configurable timeout per app (0 = default 2 poll cycles, or 1–600 seconds).
- **Error Dialog Detection**: Matches window titles against 27+ patterns including .NET exception type names (e.g., `NullReferenceException`, `DivideByZeroException`), Windows error dialogs, and generic error phrases. Requires 2 consecutive detections to confirm.
- **Window Title Change Detection**: Opt-in per-app feature that detects when a window title changes unexpectedly (e.g., a WinForms `ThreadExceptionDialog` whose title is just the app name). Requires 2 consecutive detections to confirm.
- **Memory Limit Detection**: Configurable per-app memory ceiling (0–65536 MB). Process is force-killed immediately when `WorkingSet64` exceeds the limit. Uses a lightweight kernel query with no overhead.
- **CPU-Hung Detection**: Computes CPU utilization from `TotalProcessorTime` delta across poll ticks. Force-kills after 3 consecutive ticks above 95% CPU. Always active for Keep Open apps.
- **Background Zombie Detection**: When an app's window disappears but its process lingers in the background, the zombie process is force-killed. Always active.

### Configurable Settings
- **Station Name**: Identifies the workstation in the title bar
- **Performance Tuning**: Status poll interval, grace period, GC interval, storage save interval
- **Logging Configuration**: Log flush interval, buffer size, and retention days
- **Reset Defaults**: Restore all performance and logging settings to defaults
- **Immediate Effect**: All changes apply instantly without restart
- **Grouped UI**: Settings organized into General, Performance, and Logging categories

### Status Transitions
| Status | Color | Meaning |
|--------|-------|---------|
| **Stopped** | Black | Application is not running |
| **Starting...** | Orange | Application was just launched, waiting for window to appear |
| **Running** | Green | Application window detected and process is active |
| **Running (Other Group)** | Dark Cyan | Application is running, started from a different group |
| **Stopping...** | Orange | Stop command sent, waiting for process to exit |
| **Restarting (Ns)** | Orange | Crashed, auto-restart countdown in progress |
| **Starting...** | Orange | Restart countdown finished, launch in progress |
| **Waiting (Ns)** | Orange | Sequential Start All delay countdown |
| **Failed** | Red | Max retries exhausted or restart failed |

### Group Status Indicators
| Indicator | Color | Meaning | Count Suffix |
|-----------|-------|---------|--------------|
| ● | Gray | Empty group or all stopped | No |
| ● | Green | All or some apps running | "N / T" if partial |
| ● | Orange | Apps in transitional state | No |
| ● | Red | Apps failed (max retries exhausted) | "N / T" if partial |

**Priority**: Red > Orange > Green > Gray

### Performance Optimizations
- **Batch Process Snapshot**: Single `Process.GetProcesses()` call for all apps instead of N individual `GetProcessesByName()` calls
- **O(1) ListView Lookup**: Dictionary-indexed item lookup instead of O(n) linear scan per app
- **Configurable Poll Interval**: Default 10-second watchdog poll (adjustable 1–60 seconds via Settings)
- **1-Second Countdown Timer**: Dedicated timer for smooth countdown display (only runs when needed)
- **Throttled Storage Writes**: Batched file I/O with configurable minimum save intervals
- **Atomic File Writes**: Write-to-temp-then-rename pattern with .bak fallback recovery prevents data corruption
- **Periodic GC Collection**: Configurable interval to prevent memory growth during 24/7 operation
- **Reusable Collections**: Dictionary and list instances are reused across timer ticks to minimize GC pressure
- **Cached Group Indicators**: Owner-drawn rendering only repaints when status actually changes

### Data Storage
- **Applications**: `applications.json` — per-app settings, health monitoring config, timestamps, and runtime state
- **Groups**: `groups.json` — group definitions
- **Settings**: `settings.json` — station name, performance, and logging configuration
- **Logs**: `logs/YYYY-MM-DD.log` — daily rotating log files
- No database required — fully file-based with atomic writes and fallback recovery

### Logging
All actions are logged in the format:
```
[HOSTNAME/USER IP][TIMESTAMP][LEVEL][PID:id][LOCATION] - MESSAGE
```

**Log Features:**
- Buffered writes (configurable flush interval, default: 10 seconds / 100 entries)
- Immediate flush on ERROR/FATAL
- Automatic cleanup of logs older than configured retention period (default: 7 days)
- Machine info cached at startup (no repeated DNS lookups)
- All logging parameters configurable via Settings dialog

## Architecture

### File Structure
```
InstanceManager/
├── Form1.cs                          Main UI, watchdog, crash recovery, health monitoring, group management
├── Form1.Designer.cs                 UI control definitions (9-column ListView, owner-drawn group list)
├── Form1.resx                        Form resources
├── Program.cs                        Application entry point
├── Models/
│   ├── ManagedApplication.cs         App model (19 properties including health monitoring)
│   └── ApplicationGroup.cs           Group model (GroupId, GroupName, CreatedDate)
├── Services/
│   ├── StorageService.cs             JSON CRUD with throttled saves, atomic writes, fallback recovery
│   ├── ProcessManager.cs             Batch snapshots, start/stop, zombie cleanup, PID tracking, error dialog detection
│   └── SettingsService.cs            Station name, performance, and logging settings
├── Utilities/
│   ├── SimpleLogger.cs               Buffered thread-safe logging with runtime configuration
│   ├── CustomMessageBox.cs           Owner-relative positioned dialogs
│   ├── MessageBoxHelper.cs           Convenience wrappers
│   ├── InputDialog.cs                Text input dialog (group names)
│   ├── SettingsDialog.cs             Grouped settings UI (General, Performance, Logging)
│   └── AppSettingsDialog.cs          Per-app settings (path, startup, crash recovery, health monitoring, statistics)
├── Properties/
│   ├── AssemblyInfo.cs               Assembly metadata
│   ├── Resources.Designer.cs         Resource accessors
│   └── Settings.Designer.cs          Application settings
├── applications.json                 Auto-generated app data
├── groups.json                       Auto-generated group data
├── settings.json                     Auto-generated settings
└── logs/                             Auto-generated daily log files
```

### Key Classes

| Class | Responsibility |
|-------|---------------|
| `Main` (Form1) | UI, timers, watchdog logic, health monitoring, group/app management |
| `ManagedApplication` | Data model with 19 properties including health monitoring config |
| `ApplicationGroup` | Group data model |
| `StorageService` | JSON persistence with throttling, atomic writes, and fallback recovery |
| `ProcessManager` | Batch process snapshots (CPU, memory, title, error dialog), start/stop/kill, PID tracking |
| `SettingsService` | Station name, performance, and logging settings |
| `SimpleLogger` | Buffered, thread-safe daily log files with runtime configuration |
| `SettingsDialog` | Grouped settings UI (General, Performance, Logging) |
| `EditAppDialog` | Per-app settings (path, startup, crash recovery, health monitoring, statistics) |

### Watchdog Algorithm

```
Every poll interval (default: 10 seconds, configurable via Settings):
  1. Get batch process snapshot (single Process.GetProcesses() call)
     — Collects: window state, Responding, TotalProcessorTime, WorkingSet64, MainWindowTitle
  2. Build ListView index dictionary for O(1) lookup
  3. For each managed application across ALL groups:

     Pending Restart?
       → Timer expired? → AttemptAutoRestart
       → Otherwise: skip (countdown handled by 1-second timer)

     Pending Stop?
       → Process exited? → Clean up (kill zombies), show "Stopped"
       → Still running? → Show "Stopping..."

     Failed (max retries)?
       → Show "Failed" in red

     In Grace Period?
       → Window appeared? → Show "Running"
       → Still waiting? → Show "Starting..."

     Stable Run Check?
       → Period elapsed? → Reset retry count to 0

     Not Responding? (KeepOpen)
       → First detection? → Start tracking
       → Timeout elapsed? → Force-kill

     Error Dialog? (KeepOpen)
       → Title matches pattern? → Confirm on next tick → Force-kill

     Title Changed? (KeepOpen + DetectTitleChange)
       → Title differs from baseline? → Confirm on next tick → Force-kill

     Memory Limit? (KeepOpen)
       → WorkingSet64 > limit? → Force-kill immediately

     CPU-Hung? (KeepOpen)
       → >95% CPU for 3 ticks? → Force-kill

     Background Zombie?
       → Window gone but process alive? → Kill zombies

     Unauthorized Launch? (cross-group aware)
       → Authorized sibling in another group? → "Running (Other Group)"
       → No authorized sibling? → Kill + notify user

     Was Running → Now Stopped? (crash or external close)
       → KeepOpen? → Schedule restart with countdown
       → No KeepOpen? → Record stop time

     State changed? → Update storage
     Update ListView display

  4. FlushPendingChanges()
  5. UpdateGroupIndicators()
```

## Technical Details

| Detail | Value |
|--------|-------|
| .NET Target | .NET Framework 4.0 |
| C# Version | 7.3 |
| UI Framework | Windows Forms |
| External Dependencies | **0** (zero) |
| Status Poll Interval | 10 seconds (configurable: 1–60s) |
| Countdown Timer Interval | 1 second |
| Graceful Shutdown Timeout | 3 seconds |
| Startup Grace Period | 10 seconds (configurable: 1–120s) |
| Stable Run Period | 30 seconds (configurable: 5–600s per app) |
| Not Responding Timeout | 0 = 2 poll cycles (configurable: 0–600s per app) |
| Memory Limit | 0 = disabled (configurable: 0–65536 MB per app) |
| CPU-Hung Threshold | 95% for 3 consecutive ticks |
| Error Dialog Patterns | 27+ patterns (always active) |
| GC Collect Interval | 30 minutes (configurable: 5–1440 min) |
| Storage Save Interval | 30 seconds (configurable: 5–300s) |
| Default Max Retries | 3 |
| Default Restart Delay | 5 seconds |
| Log Buffer Size | 100 entries (configurable: 10–1000) |
| Log Flush Interval | 10 seconds (configurable: 1–120s) |
| Log Retention | 7 days (configurable: 1–365 days) |

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

