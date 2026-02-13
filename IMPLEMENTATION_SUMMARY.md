# Instance Manager — Implementation Summary

## Project Status: ? Complete

---

## Implemented Features

### 1. Group-Based Application Management
? Create, edit, and delete application groups  
? Applications are organized per group with duplicate detection within each group  
? Deleting a group stops all running apps in that group first  
? Watchdog monitors all groups simultaneously, not just the visible one  

### 2. Application CRUD
? Add applications via file browser (duplicate detection per group)  
? Edit application path and settings (Keep Open, delays, counter resets)  
? Delete applications with confirmation (cleans up all tracking state)  
? Applications persist across restarts via JSON storage  

### 3. Start / Stop with Visual Feedback
? Start: "Starting..." (orange) ? "Running" (green) once window detected  
? Stop: "Stopping..." (orange) ? "Stopped" (black) once process exits  
? Start All: Immediate batch launch or sequential with per-app startup delays  
? Stop All: Stops all running apps in the selected group  
? Sequential Start All with live "Waiting (Ns)" countdown per app  

### 4. Authorized Process Watchdog
? Startup enforcement: apps already running are terminated with user notification  
? Unauthorized launch detection: externally-launched managed apps are killed  
? Authorization tracking via `_authorizedApps` HashSet  
? Debounced notifications: user warned only once per unauthorized attempt  
? Non-blocking notifications via `BeginInvoke`  
? Pending stop protection: watchdog doesn't interfere during user-initiated stops  

### 5. Crash Recovery (Keep Open)
? Auto-restart on crash with configurable delay (1–300 seconds)  
? Max retries with configurable limit (default: 3)  
? Live 1-second countdown: "Restarting (5s)" ? "Restarting (4s)" ? ... ? "Starting..."  
? "Failed" state when max retries exhausted  
? Configurable startup grace period before watchdog monitoring begins (default: 10 seconds)  
? Crash and retry counters resettable via Edit dialog  
? Background zombie process detection and cleanup  

### 6. Performance Optimizations
? Batch process snapshot: single `Process.GetProcesses()` for all apps  
? O(1) ListView item lookup via dictionary index  
? Configurable status poll interval (default: 10s, range: 1–60s)  
? Dedicated 1-second countdown timer (starts/stops on demand)  
? Throttled storage writes with configurable minimum save intervals  
? Atomic file writes (write-to-temp-then-rename)  
? Buffered logging with configurable flush interval and buffer size  
? Cached machine info (no repeated DNS lookups)  
? Periodic GC collection with configurable interval for 24/7 stability  
? Reusable dictionary/list instances to minimize GC pressure on hot paths  

### 7. Configurable Settings
? Station name, performance, and logging parameters in a single dialog  
? Settings grouped into General, Performance, and Logging categories  
? All parameters validated with min/max ranges  
? Reset Defaults button (does not affect station name)  
? Changes take effect immediately without restart  
? Persisted in `settings.json` with atomic writes  
? Backward compatible — old `settings.json` files with only StationName still load correctly  

### 8. Logging System
? Custom `SimpleLogger` — zero external dependencies  
? Thread-safe with lock mechanism  
? Buffered writes with configurable flush interval (default: 10s)  
? Configurable buffer size (default: 100 entries)  
? Daily log file rotation (`logs/YYYY-MM-DD.log`)  
? Automatic cleanup of logs older than configurable retention period (default: 7 days)  
? Immediate flush on ERROR/FATAL  
? Silent failure — logging never crashes the application  
? Runtime reconfiguration via `SimpleLogger.Configure()`  

**Format:**
```
[HOSTNAME/USER IP][TIMESTAMP][LEVEL][PID:id][LOCATION] - MESSAGE
```

### 9. Data Persistence
? `applications.json` — app data with custom JSON serializer  
? `groups.json` — group definitions  
? `settings.json` — station name, performance, and logging configuration  
? Proper Windows path escaping (`\\`)  
? Nullable DateTime support  
? Backward compatible with older JSON files  
? Dirty-flag tracking to avoid unnecessary writes  

### 10. User Interface
? Group list panel with Add/Edit/Delete group buttons  
? 9-column ListView: Index, Application, Directory, Status, Keep Open, Crashes, Retries, Last Start, Last Stop  
? Color-coded status (Green, Orange, Red, Black)  
? Custom MessageBox positioned relative to owner form  
? Form positioned in bottom-right corner of screen  
? Settings dialog with grouped configuration (General, Performance, Logging)  
? Input dialogs for group names  
? Edit dialog for per-app settings  
? Buttons disabled/enabled based on group selection state  

---

## Architecture

### File Structure
```
InstanceManager/
??? Form1.cs                          Main UI, watchdog, crash recovery, group management
??? Form1.Designer.cs                 UI control definitions (9-column ListView)
??? Form1.resx                        Form resources
??? Program.cs                        Application entry point
??? Models/
?   ??? ManagedApplication.cs         App model (14 properties)
?   ??? ApplicationGroup.cs           Group model (GroupId, GroupName, CreatedDate)
??? Services/
?   ??? StorageService.cs             JSON CRUD, throttled saves, atomic writes
?   ??? ProcessManager.cs             Batch snapshots, start/stop, zombie cleanup
?   ??? SettingsService.cs            Station name, performance, and logging settings
??? Utilities/
?   ??? SimpleLogger.cs               Buffered thread-safe logging with runtime config
?   ??? CustomMessageBox.cs           Owner-relative positioned dialogs
?   ??? MessageBoxHelper.cs           Convenience wrappers
?   ??? InputDialog.cs                Text input dialogs
?   ??? SettingsDialog.cs             Grouped settings UI (General, Performance, Logging)
?   ??? AppSettingsDialog.cs          Per-app settings dialog
??? applications.json                 Auto-generated
??? groups.json                       Auto-generated
??? settings.json                     Auto-generated
??? logs/                             Auto-generated daily log files
```

### Data Models

**ManagedApplication:**
| Property | Type | Default |
|----------|------|---------|
| Index | int | Auto-assigned |
| GroupId | int | From selected group |
| AppName | string | From filename |
| Directory | string | Full path |
| AddedDate | DateTime | Now |
| IsRunning | bool | false |
| LastStart | DateTime? | null |
| LastStop | DateTime? | null |
| KeepOpen | bool | false |
| CrashCount | int | 0 |
| RetryCount | int | 0 |
| MaxRetries | int | 3 |
| StartDelaySeconds | int | 5 |
| StartupDelaySeconds | int | 0 |

**ApplicationGroup:**
| Property | Type |
|----------|------|
| GroupId | int |
| GroupName | string |
| CreatedDate | DateTime |

### Timers

| Timer | Interval | Purpose |
|-------|----------|---------|
| `_statusUpdateTimer` | Configurable (default: 10000ms) | Watchdog polling, status sync, crash detection |
| `_countdownTimer` | 1000ms | Smooth restart countdown display (on-demand) |
| Sequential timer | 1000ms | Start All delay countdown (temporary, per operation) |

### Tracking Sets

| Field | Type | Purpose |
|-------|------|---------|
| `_authorizedApps` | HashSet\<int\> | Apps allowed to run |
| `_notifiedUnauthorized` | HashSet\<int\> | Prevents duplicate warnings |
| `_pendingStop` | HashSet\<int\> | Blocks watchdog during user stop |
| `_pendingRestart` | Dictionary\<int, DateTime\> | Crash restart schedule |
| `_failedApps` | HashSet\<int\> | Max retries exhausted |
| `_pendingSequentialStart` | HashSet\<int\> | Sequential Start All queue |
| `_startGracePeriod` | Dictionary\<int, DateTime\> | Post-launch grace period |

---

## Watchdog Algorithm

```
StatusUpdateTimer_Tick (every poll interval, default: 10 seconds):
???????????????????????????????????????????????????????????
?  1. Batch process snapshot (single GetProcesses() call) ?
?  2. Build ListView index (Dictionary for O(1) lookup)   ?
?  3. For each app across ALL groups:                     ?
?                                                         ?
?     Pending Restart?                                    ?
?       ? Expired? ? AttemptAutoRestart                   ?
?       ? Not yet? ? Skip (countdown timer handles UI)   ?
?                                                         ?
?     Pending Stop?                                       ?
?       ? Exited? ? "Stopped", cleanup background procs  ?
?       ? Still up? ? "Stopping..."                      ?
?                                                         ?
?     Failed?                                             ?
?       ? Show "Failed" (red)                             ?
?                                                         ?
?     Grace Period?                                       ?
?       ? Window up? ? "Running"                          ?
?       ? Not yet? ? "Starting..."                        ?
?                                                         ?
?     Was Running ? Now Stopped?                          ?
?       ? KeepOpen? ? Schedule restart, start countdown   ?
?       ? No? ? Record stop, show "Stopped"              ?
?                                                         ?
?     Was Stopped ? Now Running? (unauthorized)           ?
?       ? Kill + warn user                                ?
?                                                         ?
?     Sync state ? Update storage + ListView              ?
?  4. FlushPendingChanges()                               ?
???????????????????????????????????????????????????????????

CountdownTimer_Tick (every 1 second, on-demand):
???????????????????????????????????????????????????????????
?  For each pending restart:                              ?
?    seconds > 0? ? "Restarting (Ns)"                    ?
?    seconds ? 0? ? "Starting..."                        ?
?  No pending restarts? ? Stop timer                      ?
???????????????????????????????????????????????????????????
```

### Authorization Rules

| Action | _authorizedApps | _notifiedUnauthorized |
|--------|-----------------|----------------------|
| Instance Manager starts, app running | — (terminated) | — |
| User clicks Start | **Added** | **Removed** |
| User clicks Stop | **Removed** | **Removed** |
| User clicks Delete | **Removed** | **Removed** |
| App stopped externally (KeepOpen=No) | **Removed** | **Removed** |
| App crashed (KeepOpen=Yes) | **Removed** | **Removed** |
| App launched externally | — | **Added** (after warning) |
| Auto-restart succeeds | **Added** | **Removed** |

---

## Performance Metrics

| Metric | Target | Implementation |
|--------|--------|----------------|
| CPU usage | < 1% | Batch snapshots, configurable poll interval, on-demand countdown |
| Memory | < 50 MB | Proper disposal, reusable collections, periodic GC |
| Startup | < 1 second | Single batch snapshot for termination check |
| Detection latency | ? poll interval (default 10s) | Configurable status poll interval |
| Countdown accuracy | 1 second | Dedicated timer |
| Graceful shutdown | 3 seconds | CloseMainWindow() with timeout |
| External dependencies | 0 | Custom JSON, custom logging, custom dialogs |

---

## Technical Specifications

| Spec | Value |
|------|-------|
| .NET Target | .NET Framework 4.0 |
| C# Version | 7.3 |
| UI Framework | Windows Forms |
| Min OS | Windows XP SP3 |
| IDE | Visual Studio 2010+ |
| Dependencies | **None** |

---

## Objectives Met

| Requirement | Status |
|-------------|--------|
| Group-based app management | ? |
| Prevent simultaneous launches | ? |
| Prevent external launches | ? |
| Add/Edit/Delete apps | ? |
| Duplicate prevention per group | ? |
| Start/Stop with visual feedback | ? |
| Start All with sequential delays | ? |
| Auto-restart on crash (Keep Open) | ? |
| Max retries with failure state | ? |
| Live countdown timers | ? |
| JSON storage (no database) | ? |
| Configurable settings (performance & logging) | ? |
| Station name configuration | ? |
| Comprehensive logging | ? |
| Low CPU on 10+ apps | ? |
| Batch process enumeration | ? |
| Atomic file writes | ? |
| Zero external dependencies | ? |
| .NET Framework 4.0 compatible | ? |
| 24/7 stability (GC, memory, GDI) | ? |

---

## Repository
https://github.com/Jm-Paunlagui/InstanceManager
