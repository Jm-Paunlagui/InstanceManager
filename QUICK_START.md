# Instance Manager — Quick Start Guide

## First Time Setup

1. **Launch the Application**
   - Double-click `IntelligentMutexExecutionEnvironment.exe`
   - The window opens in the **bottom-right corner** of the screen
   - The group list and application list will be empty on first run

2. **Set a Station Name** (optional)
   - Click the **Settings** button
   - Enter a station name (e.g., "Line 1 Station A")
   - Optionally adjust performance and logging parameters
   - Click OK
   - The name appears in the title bar subtitle

3. **Create Your First Group**
   - Click **Add Group**
   - Enter a group name (e.g., "Production Apps")
   - The group appears in the left panel with a gray status indicator and is auto-selected

4. **Add Applications to the Group**
   - With the group selected, click **Add**
   - Browse to an executable file (e.g., `notepad.exe`)
   - The application appears in the list with Status "Stopped"

---

## Basic Operations

### Starting an Application
1. Select an application from the list
2. Click **Start**
3. Status changes: **Starting...** (orange) → **Running** (green)
4. Last Start timestamp is recorded
5. Group indicator turns green

### Stopping an Application
1. Select a running application
2. Click **Stop**
3. Confirm the action
4. Status changes: **Stopping...** (orange) → **Stopped** (black)
5. Last Stop timestamp is recorded

### Start All / Stop All
- **Start All**: Launches all stopped applications in the selected group
  - If apps have startup delays, they launch sequentially with countdown timers
- **Stop All**: Stops all running applications in the selected group

### Editing an Application
1. Select an application
2. Click **Edit**
3. The Edit dialog has 5 sections:
   - **Application Path**: Change the executable, browse, or open in Explorer
   - **Startup Settings**: Set startup delay for sequential Start All
   - **Crash Recovery Settings**: Toggle Keep Open, set max retries, restart delay, stable run period
   - **Health Monitoring**: Configure not-responding timeout, memory limit, title change detection
   - **Statistics**: View/reset crash count, retry count, and last exit code
4. Click OK to save

### Deleting an Application
1. Select an application
2. Click **Delete**
3. Confirm — the app is removed from management (the actual file is not deleted)

---

## Group Management

| Action | How |
|--------|-----|
| **Add Group** | Click "Add Group", enter name |
| **Edit Group** | Select group, click "Edit Group", enter new name |
| **Delete Group** | Select group, click "Delete Group" — running apps are stopped first |

Groups organize applications logically. The watchdog monitors apps across **all** groups, not just the visible one.

### Group Status Indicators
Each group shows a colored circle indicating aggregate status:
- **Gray** — Empty or all stopped
- **Green** — All or some running (shows "N / T" if partial)
- **Orange** — Apps in transitional state (starting, stopping, restarting)
- **Red** — Apps failed (shows "N / T" if partial)

---

## Keep Open (Auto-Restart)

Configure per-application via the **Edit** dialog:

| Setting | Description | Default |
|---------|-------------|---------|
| **Keep Open** | Enable auto-restart on crash | Off |
| **Restart Delay** | Seconds to wait before restarting | 5 |
| **Max Retries** | Maximum restart attempts before giving up | 3 |
| **Stable Run Period** | Seconds app must run before retry count resets | 30 |
| **Startup Delay** | Seconds to wait between apps during Start All | 0 |

### Crash Recovery Flow
1. App crashes → Status: **Restarting (5s)** with live countdown
2. Countdown reaches 0 → Status: **Starting...**
3. App relaunches → Status: **Starting...** → **Running**
4. App runs stably for the configured period → retry count resets to 0
5. If max retries exhausted → Status: **Failed** (red)

Reset crash/retry counters via the **Edit** dialog at any time.

---

## Health Monitoring

All health monitoring features apply only to **Keep Open** apps and trigger auto-restart on detection.

| Detection | What It Catches | Configuration |
|-----------|----------------|---------------|
| **Not Responding** | UI freeze, deadlocked UI thread | Edit → Not Responding (sec): 0 = 2 poll cycles |
| **Error Dialog** | Unhandled exception dialogs, crash dialogs, .NET error windows | Always active (27+ patterns) |
| **Title Change** | WinForms ThreadExceptionDialog (title = app name, no error keywords) | Edit → Detect Title Change: opt-in |
| **Memory Limit** | Memory leaks, runaway allocation | Edit → Memory Limit (MB): 0 = disabled |
| **CPU-Hung** | Infinite loops, spin-wait deadlocks (>95% CPU for 3 ticks) | Always active |
| **Zombie Process** | Window closed but process lingers in background | Always active |

---

## Status Column Reference

| Status | Color | Meaning |
|--------|-------|---------|
| Stopped | Black | Not running |
| Starting... | Orange | Launched, waiting for window |
| Running | Green | Process window detected |
| Running (Other Group) | Dark Cyan | Running from a different group |
| Stopping... | Orange | Shutdown in progress |
| Restarting (Ns) | Orange | Crash restart countdown |
| Starting... | Orange | Countdown finished, relaunching |
| Waiting (Ns) | Orange | Sequential Start All delay |
| Failed | Red | Max retries exhausted |

---

## ListView Columns

| # | Column | Description |
|---|--------|-------------|
| 0 | **Index** | Auto-incremented ID |
| 1 | **Application** | Executable name (without .exe) |
| 2 | **Directory** | Full path to the executable |
| 3 | **Status** | Current state with color coding |
| 4 | **Keep Open** | "Yes" or "No" |
| 5 | **Crashes** | Total crash count |
| 6 | **Retries** | Current retry count (resets after stable run period) |
| 7 | **Last Start** | Timestamp or "Never" |
| 8 | **Last Stop** | Timestamp or "Never" |

---

## Unauthorized Launch Detection

Instance Manager enforces that managed applications must be started through its interface:

1. Add an application to Instance Manager
2. If someone launches that `.exe` directly (double-click, shortcut, etc.)
3. Within one poll interval (default: 10 seconds, configurable in Settings), Instance Manager:
   - **Kills** the unauthorized process
   - Shows a warning notification
4. The status remains **Stopped**

> **Note:** Applications already running when Instance Manager starts are **terminated** with a notification listing all affected apps.

> **Cross-Group:** If the same executable is added to multiple groups and started from one group, other groups show **Running (Other Group)** in dark cyan instead of treating it as unauthorized.

---

## Testing Checklist

### Basic Tests
- [ ] Create a group and add an application
- [ ] Start → verify "Starting..." → "Running" transition
- [ ] Stop → verify "Stopping..." → "Stopped" transition
- [ ] Start All / Stop All with multiple apps
- [ ] Edit an application's path and settings
- [ ] Delete an application
- [ ] Delete a group (with running apps)

### Watchdog Tests
- [ ] Launch a managed app externally → verify it gets killed
- [ ] Close a running app via Task Manager → verify status updates to "Stopped"
- [ ] Start Instance Manager while a managed app is already running → verify it gets terminated
- [ ] Add same app to two groups, start in one → verify "Running (Other Group)" in the other

### Keep Open Tests
- [ ] Enable Keep Open on an app, start it, then close it externally
- [ ] Verify countdown appears: "Restarting (5s)" → "Starting..." → "Running"
- [ ] Close it enough times to exhaust max retries → verify "Failed" status
- [ ] Reset retry count via Edit dialog → verify app can restart again
- [ ] Verify retry count resets to 0 after stable run period elapses

### Health Monitoring Tests
- [ ] Set Memory Limit (MB) to a low value → start app → verify force-kill when exceeded
- [ ] Enable Detect Title Change → start app → change its title → verify force-kill
- [ ] Open an app that shows an error dialog → verify error dialog detection kills it
- [ ] Verify Last Exit Code is recorded in Edit dialog after a crash

### Sequential Start All Tests
- [ ] Set startup delays on multiple apps (e.g., 3s, 5s)
- [ ] Click Start All → verify apps launch one-by-one with "Waiting (Ns)" countdowns

### Group Indicator Tests
- [ ] Start some apps in a group → verify green indicator with "N / T" suffix
- [ ] Start all apps → verify solid green (no suffix)
- [ ] Exhaust retries on an app → verify red indicator
- [ ] Stop all apps → verify gray indicator

### Performance Tests
- [ ] Add 10+ apps to a single group
- [ ] Start several and monitor CPU in Task Manager → should be < 1%
- [ ] Memory should remain < 50 MB

---

## Log Files

**Location:** `logs/YYYY-MM-DD.log`

**Format:**
```
[HOSTNAME/USER IP][2025-06-15 14:30:00][INFO][PID:12345][StartApplication @ ProcessManager.cs] - Successfully started notepad (PID: 9876)
[HOSTNAME/USER IP][2025-06-15 14:30:05][WARN][PID:12345][HandleUnauthorizedLaunch @ Form1.cs] - Unauthorized launch detected for 'notepad' - killing process
```

**Health monitoring events are also logged:**
```
[...][WARN][...][UpdateApplicationStatuses @ Form1.cs] - 'MyApp' exceeded memory limit (512MB / 256MB) — force-killing for auto-restart
[...][WARN][...][UpdateApplicationStatuses @ Form1.cs] - 'MyApp' confirmed error dialog: "NullReferenceException" — force-killing for auto-restart
[...][WARN][...][UpdateApplicationStatuses @ Form1.cs] - 'MyApp' has been at 99% CPU for 3 consecutive checks — force-killing as CPU-hung
```

**View logs in real-time:**
```cmd
powershell Get-Content -Path "logs\2025-06-15.log" -Wait -Tail 20
```

---

## Data Files

| File | Content |
|------|---------|
| `applications.json` | All managed applications with settings, health monitoring config, and timestamps |
| `groups.json` | Group definitions |
| `settings.json` | Station name, performance, and logging configuration |
| `logs/*.log` | Daily log files (auto-cleaned after configured retention period, default: 7 days) |

All files are created automatically. Delete any file to reset that data.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| App won't start | Check file path in Edit dialog; verify the .exe exists |
| App won't stop | Check logs for errors; app may have already closed externally |
| Status stuck on "Starting..." | Wait for the grace period (default: 10 seconds, configurable in Settings); check if the app creates a window |
| Unauthorized launch not detected | Wait up to one poll interval (default: 10s); ensure the app is in the managed list |
| "Failed" status won't clear | Edit the app and reset the retry count |
| App killed by memory limit | Increase Memory Limit (MB) in Edit or set to 0 to disable |
| App killed by title change | Disable Detect Title Change in Edit if the app legitimately changes its title |
| Logs not appearing | Check write permissions in the application directory |
| Station name not saving | Check write permissions for `settings.json` |
| Data file corrupted | Check for `.bak` or `.tmp` backup files; rename to replace the corrupted file |

