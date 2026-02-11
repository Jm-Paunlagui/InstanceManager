# Instance Manager — User Guide

## Table of Contents
1. [Getting Started](#getting-started)
2. [Interface Overview](#interface-overview)
3. [Managing Groups](#managing-groups)
4. [Managing Applications](#managing-applications)
5. [Starting and Stopping Applications](#starting-and-stopping-applications)
6. [Keep Open (Auto-Restart)](#keep-open-auto-restart)
7. [Understanding Status Indicators](#understanding-status-indicators)
8. [Station Name](#station-name)
9. [Logs](#logs)
10. [Security: Unauthorized Launch Prevention](#security-unauthorized-launch-prevention)
11. [Frequently Asked Questions](#frequently-asked-questions)
12. [Troubleshooting](#troubleshooting)

---

## Getting Started

### Installation
No installation is required. Copy `InstanceManager.exe` to any folder and run it. The application creates its data files automatically in the same directory:

| File | Purpose |
|------|---------|
| `applications.json` | Stores your managed applications |
| `groups.json` | Stores your groups |
| `settings.json` | Stores your station name |
| `logs/` | Daily log files |

### First Launch
1. Double-click `InstanceManager.exe`
2. The window appears in the **bottom-right corner** of your screen
3. Both the group list (left) and application list (right) will be empty

### Startup Behavior
When Instance Manager starts, it checks if any managed applications are already running. If so, they are **automatically terminated** and a notification lists the affected apps. This ensures all managed applications are only launched through Instance Manager.

---

## Interface Overview

The interface is divided into two main areas:

### Left Panel — Groups
- **Group List**: Shows all your application groups
- **Add Group / Edit Group / Delete Group**: Manage groups

### Right Panel — Applications
- **Application List**: Shows apps in the selected group with 9 columns
- **Action Buttons**: Add, Edit, Delete, Start, Stop, Start All, Stop All, Refresh
- **Settings / Logs**: Configure station name or open the logs folder

### Application List Columns

| Column | Description |
|--------|-------------|
| Index | Unique auto-incremented ID |
| Application | Name of the executable |
| Directory | Full file path |
| Status | Current state (see [Status Indicators](#understanding-status-indicators)) |
| Keep Open | "Yes" if auto-restart is enabled |
| Crashes | Total number of detected crashes |
| Retries | Current auto-restart retry count |
| Last Start | When the app was last started |
| Last Stop | When the app was last stopped |

---

## Managing Groups

Groups let you organize your applications logically — for example, by production line, station, or purpose.

### Create a Group
1. Click **Add Group**
2. Enter a name (e.g., "Line 1 Apps")
3. Click OK
4. The new group is created and automatically selected

### Rename a Group
1. Select the group in the left panel
2. Click **Edit Group**
3. Enter the new name
4. Click OK

### Delete a Group
1. Select the group
2. Click **Delete Group**
3. Confirm the deletion
4. **Important**: All running applications in the group will be stopped, and all applications will be removed from management

> **Note**: Deleting a group does **not** delete the actual application files from disk.

---

## Managing Applications

### Adding an Application
1. Select a group from the left panel
2. Click **Add**
3. Browse to the executable (`.exe`) file
4. Click Open
5. The application appears in the list with status "Stopped"

> **Note**: You cannot add the same executable path twice within the same group. A warning will appear if you try.

### Editing an Application
1. Select an application in the list
2. Click **Edit**
3. In the Edit dialog, you can:
   - **Change the executable path** (browse to a different file)
   - **Toggle Keep Open** (auto-restart on crash)
   - **Set Start Delay** — seconds to wait before auto-restart (1–300)
   - **Set Startup Delay** — seconds to wait during sequential Start All (0–300)
   - **Set Max Retries** — maximum restart attempts before giving up
   - **Reset Crash Count** — clear the crash counter
   - **Reset Retry Count** — clear the retry counter and remove "Failed" state
4. Click OK to save changes

### Deleting an Application
1. Select an application
2. Click **Delete**
3. Confirm the deletion
4. The application is removed from management

> **Note**: The actual executable file is **not** deleted from disk.

---

## Starting and Stopping Applications

### Starting a Single Application
1. Select an application from the list
2. Click **Start**
3. The status changes to **Starting...** (orange)
4. Once the application window appears, the status changes to **Running** (green)
5. The "Last Start" timestamp is recorded

### Stopping a Single Application
1. Select a running application
2. Click **Stop**
3. Confirm the action
4. The status changes to **Stopping...** (orange)
5. Once the process exits, the status changes to **Stopped** (black)
6. The "Last Stop" timestamp is recorded

Instance Manager attempts a graceful shutdown first. If the application doesn't close within 3 seconds, it is force-killed.

### Starting All Applications
1. Select a group
2. Click **Start All**
3. Confirm the action
4. All stopped applications in the group are launched

**With Startup Delays**: If any applications have a Startup Delay configured (via Edit), they launch one at a time. Each app shows a "Waiting (Ns)" countdown before it launches. This prevents overloading the system when starting many apps at once.

### Stopping All Applications
1. Select a group
2. Click **Stop All**
3. Confirm the action
4. All running applications in the group are stopped

---

## Keep Open (Auto-Restart)

Keep Open automatically restarts an application when it crashes or is closed unexpectedly.

### Enabling Keep Open
1. Select an application
2. Click **Edit**
3. Check **Keep Open**
4. Configure the settings:
   - **Start Delay**: How long to wait before restarting (default: 5 seconds)
   - **Max Retries**: How many times to attempt restarting (default: 3)
5. Click OK

### How It Works
When a Keep Open application stops unexpectedly:

1. The crash is detected by the watchdog (within 5 seconds)
2. The crash count increments
3. The retry count increments
4. A countdown begins: **Restarting (5s)** ? **Restarting (4s)** ? ... ? **Starting...**
5. The application relaunches automatically
6. If the relaunch succeeds, the retry count resets to 0

### When Max Retries Are Exhausted
If the application crashes more times than the max retry limit (in a row, without a successful run between crashes):
1. The status changes to **Failed** (red)
2. No further auto-restart attempts are made
3. You can fix the issue and either:
   - Click **Start** to manually restart (resets the retry count)
   - Click **Edit** and reset the retry count, then start

### Important Notes
- Manually stopping an app via the **Stop** button does **not** trigger a restart — even with Keep Open enabled
- The retry count resets to 0 whenever the app successfully starts (manual or auto)
- The crash count is cumulative and never resets automatically (reset it via Edit)

---

## Understanding Status Indicators

| Status | Color | What It Means | What to Do |
|--------|-------|---------------|------------|
| **Stopped** | Black | Application is not running | Click Start to launch |
| **Starting...** | Orange | Application was just launched, waiting for its window to appear | Wait — will change to Running |
| **Running** | Green | Application is running normally | No action needed |
| **Stopping...** | Orange | Stop command was sent, waiting for the process to exit | Wait — will change to Stopped |
| **Restarting (Ns)** | Orange | Application crashed, auto-restart countdown in progress | Wait for countdown to finish |
| **Starting...** | Orange | Restart countdown finished, application is being relaunched | Wait — will change to Running |
| **Waiting (Ns)** | Orange | Sequential Start All — this app is next in line | Wait for countdown to finish |
| **Failed** | Red | Auto-restart gave up after max retries | Check the app, then Start manually or reset retries |

---

## Station Name

Set a station name to identify which workstation Instance Manager is running on.

### Setting the Station Name
1. Click **Settings**
2. Enter a station name (e.g., "LRA Line 2 - Station B")
3. Click OK
4. The name appears in the title bar as a subtitle

The station name is saved in `settings.json` and persists across restarts.

---

## Logs

Instance Manager logs all operations to daily log files.

### Viewing Logs
- Click the **Logs** button to open the logs folder in File Explorer
- Log files are named `YYYY-MM-DD.log`

### What Gets Logged
- Application starts and stops (including PIDs)
- Unauthorized launch detections
- Crash detections and auto-restart attempts
- Group and application add/edit/delete operations
- Errors and warnings
- Startup terminations

### Log Format
```
[HOSTNAME/USER IP][2025-06-15 14:30:00][INFO][PID:12345][StartApplication @ ProcessManager.cs] - Successfully started notepad (PID: 9876)
```

### Log Retention
Logs older than 30 days are automatically deleted on startup.

### Real-Time Log Monitoring
Open a PowerShell window and run:
```powershell
Get-Content -Path "logs\2025-06-15.log" -Wait -Tail 20
```

---

## Security: Unauthorized Launch Prevention

Instance Manager enforces that managed applications can **only** be launched through its interface.

### How It Works
1. When you start an app through Instance Manager, it's added to an authorized list
2. Every 5 seconds, the watchdog checks all managed applications
3. If a managed app is running but **not** in the authorized list, it was launched externally
4. The unauthorized process is **killed immediately**
5. A warning notification is shown (once per attempt)

### Scenarios

| Scenario | What Happens |
|----------|-------------|
| User starts app via Instance Manager | ? Allowed — added to authorized list |
| User double-clicks the .exe directly | ? Killed within 5 seconds + warning shown |
| User opens app via shortcut | ? Killed within 5 seconds + warning shown |
| App was running before Instance Manager started | ? Terminated on startup + notification |
| App crashes and auto-restarts (Keep Open) | ? Allowed — re-authorized automatically |

### Why This Matters
- Ensures only one instance of each managed application runs
- Provides an audit trail of who started what and when
- Prevents circumventing instance management by launching apps directly

---

## Frequently Asked Questions

### Can I manage applications that require Administrator privileges?
No. Instance Manager runs at standard user level and cannot manage elevated processes. If your managed application requires elevation, Instance Manager won't be able to start or stop it.

### What happens if I close Instance Manager while applications are running?
The managed applications **continue running** — Instance Manager does not stop them on exit. However, the watchdog will no longer monitor them. When you restart Instance Manager, any still-running managed apps will be terminated.

### Can I manage the same application in multiple groups?
Yes, you can add the same executable to different groups. Each entry is tracked independently.

### Does Instance Manager modify my applications?
No. Instance Manager only starts and stops processes. It never modifies any application files.

### What if my application doesn't have a visible window?
Instance Manager detects running applications by checking for a main window handle. Applications that run purely in the background (no window at all) may not be detected as "Running" and could be falsely treated as crashed.

### Can I change the watchdog polling interval?
Not through the UI. The interval is set to 5 seconds in the code (`_statusUpdateTimer.Interval = 5000`).

### Where is my data stored?
All data files are in the same folder as `InstanceManager.exe`:
- `applications.json` — your managed applications
- `groups.json` — your groups
- `settings.json` — station name
- `logs/` — daily log files

### How do I reset everything?
Delete `applications.json`, `groups.json`, and `settings.json`, then restart Instance Manager.

---

## Troubleshooting

### Application Won't Start
1. Check that the file path is correct — click **Edit** and verify the Directory
2. Make sure the `.exe` file exists at that path
3. Check the log file for error messages
4. Verify you have permission to run the application

### Application Won't Stop
1. Check the log file for "Access denied" errors
2. The application may have already closed externally
3. Click **Refresh** to force a status update
4. Try stopping it via Task Manager as a last resort

### Status Stuck on "Starting..."
The application has been launched but its window hasn't appeared yet. Possible causes:
- The application is still loading (wait for the 10-second grace period)
- The application runs without a visible window (not supported for status detection)
- The application crashed immediately after launch (check logs)

### Status Shows "Failed"
The application has exhausted its maximum retry attempts. To fix:
1. Investigate why the application keeps crashing
2. Click **Edit** on the application
3. Reset the **Retry Count** to 0
4. Click OK
5. Click **Start** to try again

### Unauthorized Launch Warning Keeps Appearing
This means someone (or something) keeps trying to launch the managed application outside of Instance Manager. Check for:
- Scheduled tasks that launch the application
- Startup items in Windows
- Other users launching the application directly

### Logs Not Being Created
1. Make sure Instance Manager has write permission in its directory
2. Check if the `logs/` folder exists — if not, try creating it manually
3. Restart Instance Manager

### Data File Corruption
If `applications.json` or `groups.json` becomes corrupted:
1. Check for a `.tmp` backup file in the same directory
2. Rename the `.tmp` file to replace the corrupted file
3. If no backup exists, delete the corrupted file (you'll lose that data)
4. Restart Instance Manager

---

## System Requirements

| Requirement | Minimum |
|-------------|---------|
| Operating System | Windows XP SP3 or later |
| .NET Framework | 4.0 or higher |
| RAM | 50 MB available |
| Disk Space | 5 MB + log files |
| Display | 1024×768 or higher |

---

## Repository
https://github.com/Jm-Paunlagui/InstanceManager
