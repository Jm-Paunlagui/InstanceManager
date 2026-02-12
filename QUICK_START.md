# Instance Manager — Quick Start Guide

## First Time Setup

1. **Launch the Application**
   - Double-click `IntelligentMutexExecutionEnvironment.exe`
   - The window opens in the **bottom-right corner** of the screen
   - The group list and application list will be empty on first run

2. **Set a Station Name** (optional)
   - Click the **Settings** button
   - Enter a station name (e.g., "Line 1 Station A")
   - The name appears in the title bar subtitle

3. **Create Your First Group**
   - Click **Add Group**
   - Enter a group name (e.g., "Production Apps")
   - The group appears in the left panel and is auto-selected

4. **Add Applications to the Group**
   - With the group selected, click **Add**
   - Browse to an executable file (e.g., `notepad.exe`)
   - The application appears in the list with Status "Stopped"

---

## Basic Operations

### Starting an Application
1. Select an application from the list
2. Click **Start**
3. Status changes: **Starting...** (orange) ? **Running** (green)
4. Last Start timestamp is recorded

### Stopping an Application
1. Select a running application
2. Click **Stop**
3. Confirm the action
4. Status changes: **Stopping...** (orange) ? **Stopped** (black)
5. Last Stop timestamp is recorded

### Start All / Stop All
- **Start All**: Launches all stopped applications in the selected group
  - If apps have startup delays, they launch sequentially with countdown timers
- **Stop All**: Stops all running applications in the selected group

### Editing an Application
1. Select an application
2. Click **Edit**
3. Change the executable path, toggle Keep Open, adjust delays, or reset counters
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

---

## Keep Open (Auto-Restart)

Configure per-application via the **Edit** dialog:

| Setting | Description | Default |
|---------|-------------|---------|
| **Keep Open** | Enable auto-restart on crash | Off |
| **Start Delay** | Seconds to wait before restarting | 5 |
| **Max Retries** | Maximum restart attempts before giving up | 3 |
| **Startup Delay** | Seconds to wait between apps during Start All | 0 |

### Crash Recovery Flow
1. App crashes ? Status: **Restarting (5s)** with live countdown
2. Countdown reaches 0 ? Status: **Starting...**
3. App relaunches ? Status: **Starting...** ? **Running**
4. If max retries exhausted ? Status: **Failed** (red)

Reset crash/retry counters via the **Edit** dialog at any time.

---

## Status Column Reference

| Status | Color | Meaning |
|--------|-------|---------|
| Stopped | Black | Not running |
| Starting... | Orange | Launched, waiting for window |
| Running | Green | Process window detected |
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
| 6 | **Retries** | Current retry count (resets on successful start) |
| 7 | **Last Start** | Timestamp or "Never" |
| 8 | **Last Stop** | Timestamp or "Never" |

---

## Unauthorized Launch Detection

Instance Manager enforces that managed applications must be started through its interface:

1. Add an application to Instance Manager
2. If someone launches that `.exe` directly (double-click, shortcut, etc.)
3. Within 5 seconds, Instance Manager:
   - **Kills** the unauthorized process
   - Shows a warning notification
4. The status remains **Stopped**

> **Note:** Applications already running when Instance Manager starts are **terminated** with a notification listing all affected apps.

---

## Testing Checklist

### Basic Tests
- [ ] Create a group and add an application
- [ ] Start ? verify "Starting..." ? "Running" transition
- [ ] Stop ? verify "Stopping..." ? "Stopped" transition
- [ ] Start All / Stop All with multiple apps
- [ ] Edit an application's path and settings
- [ ] Delete an application
- [ ] Delete a group (with running apps)

### Watchdog Tests
- [ ] Launch a managed app externally ? verify it gets killed
- [ ] Close a running app via Task Manager ? verify status updates to "Stopped"
- [ ] Start Instance Manager while a managed app is already running ? verify it gets terminated

### Keep Open Tests
- [ ] Enable Keep Open on an app, start it, then close it externally
- [ ] Verify countdown appears: "Restarting (5s)" ? "Starting..." ? "Running"
- [ ] Close it enough times to exhaust max retries ? verify "Failed" status
- [ ] Reset retry count via Edit dialog ? verify app can restart again

### Sequential Start All Tests
- [ ] Set startup delays on multiple apps (e.g., 3s, 5s)
- [ ] Click Start All ? verify apps launch one-by-one with "Waiting (Ns)" countdowns

### Performance Tests
- [ ] Add 10+ apps to a single group
- [ ] Start several and monitor CPU in Task Manager ? should be < 1%
- [ ] Memory should remain < 50 MB

---

## Log Files

**Location:** `logs/YYYY-MM-DD.log`

**Format:**
```
[HOSTNAME/USER IP][2025-06-15 14:30:00][INFO][PID:12345][StartApplication @ ProcessManager.cs] - Successfully started notepad (PID: 9876)
[HOSTNAME/USER IP][2025-06-15 14:30:05][WARN][PID:12345][HandleUnauthorizedLaunch @ Form1.cs] - Unauthorized launch detected for 'notepad' - killing process
```

**View logs in real-time:**
```cmd
powershell Get-Content -Path "logs\2025-06-15.log" -Wait -Tail 20
```

---

## Data Files

| File | Content |
|------|---------|
| `applications.json` | All managed applications with settings and timestamps |
| `groups.json` | Group definitions |
| `settings.json` | Station name |
| `logs/*.log` | Daily log files (auto-cleaned after 30 days) |

All files are created automatically. Delete any file to reset that data.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| App won't start | Check file path in Edit dialog; verify the .exe exists |
| App won't stop | Check logs for errors; app may have already closed externally |
| Status stuck on "Starting..." | Wait for the 10-second grace period; check if the app creates a window |
| Unauthorized launch not detected | Wait up to 5 seconds; ensure the app is in the managed list |
| "Failed" status won't clear | Edit the app and reset the retry count |
| Logs not appearing | Check write permissions in the application directory |
| Station name not saving | Check write permissions for `settings.json` |

---

## Repository
https://github.com/Jm-Paunlagui/InstanceManager
