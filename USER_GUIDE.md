# IMEE — User Guide

## Table of Contents
1. [Getting Started](#getting-started)
2. [Interface Overview](#interface-overview)
3. [Managing Groups](#managing-groups)
4. [Managing Applications](#managing-applications)
5. [Starting and Stopping Applications](#starting-and-stopping-applications)
6. [Keep Open (Auto-Restart)](#keep-open-auto-restart)
7. [Health Monitoring](#health-monitoring)
8. [Understanding Status Indicators](#understanding-status-indicators)
9. [Settings](#settings)
10. [Logs](#logs)
11. [Security: Unauthorized Launch Prevention](#security-unauthorized-launch-prevention)
12. [System Tray](#system-tray)
13. [Frequently Asked Questions](#frequently-asked-questions)
14. [Troubleshooting](#troubleshooting)

---

## Getting Started

### Installation
No installation is required. Copy `IntelligentMutexExecutionEnvironment.exe` to any folder and run it. The application creates its data files automatically in the same directory:

| File | Purpose |
|------|---------|
| `applications.json` | Stores your managed applications |
| `groups.json` | Stores your groups |
| `settings.json` | Stores station name, startup, performance, and logging configuration |
| `logs/` | Daily log files |

### First Launch
1. Double-click `IntelligentMutexExecutionEnvironment.exe`
2. The window appears in the **bottom-right corner** of your screen
3. Both the group list (left) and application list (right) will be empty

### Startup Behavior
When IMEE starts, it checks if any managed applications are already running. If so, they are **automatically terminated** and a notification lists the affected apps. This ensures all managed applications are only launched through IMEE.

---

## Interface Overview

The interface is divided into two main areas:

### Left Panel — Groups
- **Group List**: Shows all your application groups with color-coded status indicators (see [Group Status Indicators](#group-status-indicators))
- **Add Group / Edit Group / Delete Group**: Manage groups

### Right Panel — Applications
- **Application List**: Shows apps in the selected group with 10 columns
- **Action Buttons**: Add, Edit, Delete, Start, Stop, Start All, Stop All, Refresh
- **User Guide / Settings / Logs**: Open the user guide PDF, configure station name, performance, and logging settings, or open the logs folder

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
| Exit Code | The exit code from the last process termination ("—" if none recorded) |

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
3. The Edit dialog is organized into the following sections:

#### Application Path
- **Directory**: The full path to the executable. Use **Browse** to change it, or **Open** to reveal the file in File Explorer.

#### Launcher Script / Exe (Optional)
If your application requires a launcher script or wrapper executable to start (e.g., a `.bat`, `.cmd`, `.vbs`, `.ps1`, or another `.exe`), you can configure it here. When a launcher is set, IMEE will execute the launcher to start the application, but will still monitor the **primary executable** (from Application Path above) for running status detection.

- **Launcher Path**: The path to the launcher file. Use **Browse** to select it, **Open** to reveal it in File Explorer, or **Clear** to remove it.
- Supported launcher types: `.exe`, `.bat`, `.cmd`, `.vbs`, `.ps1`
- Leave empty to launch the application directly from its Application Path.

> **Note**: When using a launcher, the exit code tracking is for the actual monitored application process, not the launcher process.

#### Startup Settings
| Setting | Range | Default | Description |
|---------|-------|---------|-------------|
| **Startup Delay (sec)** | 0–300 | 10 | Wait time before launching this app during sequential Start All. Apps with 0 launch immediately. |

#### Crash Recovery Settings
| Setting | Range | Default | Description |
|---------|-------|---------|-------------|
| **Keep Open (Auto-Restart)** | Yes/No | No | When enabled, the watchdog automatically restarts the app after a crash. Also enables health monitoring — when Keep Open is active, IMEE will actively kill and restart unhealthy processes. When disabled, IMEE only logs and notifies about detected issues without taking corrective action. |
| **Max Retries** | 1–100 | 3 | Maximum number of consecutive restart attempts before giving up. |
| **Restart Delay (sec)** | 1–300 | 5 | Seconds to wait before attempting a restart after a crash. |
| **Stable Run Period (sec)** | 5–600 | 30 | How long the app must run continuously after restart before the retry count resets to 0. |

#### Health Monitoring
| Setting | Range | Default | Description |
|---------|-------|---------|-------------|
| **Not Responding (sec)** | 0–600 | 0 | How long a process can remain unresponsive before being force-killed. `0` = default behavior (2 consecutive poll cycles). Set a higher value for apps that legitimately freeze briefly during heavy operations. |
| **Memory Limit (MB)** | 0–65536 | 0 | Maximum working set memory allowed. If the process exceeds this limit, it is force-killed for auto-restart. `0` = no limit (disabled). Useful for detecting memory leaks in long-running apps. |
| **Detect Title Change** | Yes/No | No | When enabled, the watchdog records the app's initial window title and treats a persistent title change as a suspected error dialog (e.g., a WinForms unhandled exception dialog whose title is just the app name). Requires 2 consecutive detections to confirm. Leave disabled for apps that legitimately change their window title (e.g., showing a document name). |

> **Note**: Health monitoring controls (Not Responding, Memory Limit, Detect Title Change) are only enabled when **Keep Open** is checked. When Keep Open is active, health monitoring is automatically enforced — unhealthy processes will be killed and restarted. When Keep Open is off, IMEE will only log and notify about detected issues.

#### Statistics
| Field | Description |
|-------|-------------|
| **Crash Count** | Total number of detected crashes (cumulative). Click **Reset** to clear. |
| **Retry Count** | Current consecutive restart attempt count vs. max retries (e.g., "1 / 3"). Click **Reset** to clear and remove "Failed" state. |
| **Last Exit Code** | The exit code from the last process termination. `N/A` if no exit has been recorded. A non-zero exit code (shown in red with "abnormal") typically indicates the process crashed or terminated with an error. Click **Copy** to copy the exit code to the clipboard. |

4. Click **OK** to save changes, or **Cancel** to discard.

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

IMEE attempts a graceful shutdown first. If the application doesn't close within 3 seconds, it is force-killed.

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
   - **Restart Delay**: How long to wait before restarting (default: 5 seconds)
   - **Max Retries**: How many times to attempt restarting (default: 3)
5. Click OK

### How It Works
When a Keep Open application stops unexpectedly:

1. The crash is detected by the watchdog (within one poll interval, default: 10 seconds)
2. The crash count increments
3. The retry count increments
4. A countdown begins: **Restarting (5s)** to **Restarting (4s)** to ... to **Starting...**
5. The application relaunches automatically
6. If the relaunch succeeds and the app runs stably for the **Stable Run Period** (default: 30 seconds), the retry count resets to 0

### When Max Retries Are Exhausted
If the application crashes more times than the max retry limit (without running stably between crashes):
1. The status changes to **Failed** (red)
2. No further auto-restart attempts are made
3. You can fix the issue and either:
   - Click **Start** to manually restart (resets the retry count)
   - Click **Edit** and reset the retry count, then start

### Stable Run Period
The retry count only resets to 0 after the application has been running continuously for the configured **Stable Run Period** (default: 30 seconds). This prevents apps that crash shortly after starting from resetting retries indefinitely and never reaching "Failed" state.

You can configure this per-application via **Edit** to **Stable Run Period (seconds)**.

### Important Notes
- Manually stopping an app via the **Stop** button does **not** trigger a restart — even with Keep Open enabled
- The retry count resets to 0 immediately when the app is manually started via the **Start** button
- After an auto-restart, the retry count resets to 0 only after the app runs stably for the configured **Stable Run Period** (default: 30 seconds)
- The crash count is cumulative and never resets automatically (reset it via Edit)

---

## Health Monitoring

IMEE includes several health monitoring mechanisms that detect unhealthy applications beyond simple crash detection. All health monitoring features apply only to **Keep Open** applications and trigger the same auto-restart flow as a regular crash.

When Keep Open is **disabled**, IMEE will still detect health issues but will only **log and notify** the user without taking automatic corrective action. The application status will show **Warning** (orange) to indicate a detected issue.

### Not Responding Detection

Detects when an application's UI thread is frozen (e.g., infinite loop, UI deadlock, blocked message pump).

**How it works:**
1. Each poll tick checks `Process.Responding` for all windowed processes
2. If an app is not responding, tracking begins
3. After the configured timeout (or 2 consecutive poll cycles if timeout is `0`), the process is force-killed
4. Auto-restart takes over

**Configure via:** Edit to **Not Responding (sec)**

> **Tip**: Set a non-zero value (e.g., 30 seconds) for apps that occasionally freeze briefly during heavy operations like loading large files.

### Error Dialog Detection

Detects when an application shows a crash or error dialog that is technically "responding" (pumping messages) but has the app effectively stuck. This catches error dialogs that `Process.Responding` cannot detect because the dialog's message pump keeps the window alive.

**Detected patterns include:**
- "has stopped working", "has encountered a problem"
- "unhandled exception", "application error", "runtime error", "fatal error"
- ".NET Framework", "CLR error", "just-in-time debugging"
- "assertion failed", "access violation", "stack overflow"
- .NET exception type names (e.g., `NullReferenceException`, `DivideByZeroException`, `OutOfMemoryException`, `StackOverflowException`, `AccessViolationException`, `InvalidOperationException`)
- Generic phrases like "an error occurred", "error in application"

**How it works:**
1. Each poll tick checks the main window title against known error dialog patterns
2. If a match is found, it waits one more poll cycle to confirm (avoids false positives)
3. On second consecutive detection, the process is force-killed
4. Auto-restart takes over

This detection is **always active** for Keep Open apps — no configuration needed.

### Window Title Change Detection

Detects when an application's window title changes unexpectedly, which can indicate a WinForms `ThreadExceptionDialog` or similar error dialog whose title doesn't contain any recognizable error keywords.

**How it works:**
1. When an app first appears as running, its window title is recorded as the baseline
2. Each poll tick compares the current title to the baseline
3. If the title changes and stays changed for 2 consecutive poll cycles, the process is force-killed
4. Auto-restart takes over

**Configure via:** Edit to **Detect Title Change** (opt-in, disabled by default)

> **Warning**: Only enable this for apps with a stable, unchanging window title. Apps that show document names, status text, or progress in their title bar will trigger false positives.

### Memory Limit Detection

Detects when an application's working set memory exceeds a configured threshold. Useful for catching memory leaks in long-running 24/7 applications.

**How it works:**
1. Each poll tick reads `Process.WorkingSet64` (a lightweight kernel query with no overhead)
2. If the working set exceeds the configured limit, the process is force-killed immediately (no confirmation wait)
3. Auto-restart takes over

**Configure via:** Edit to **Memory Limit (MB)** (set to `0` to disable)

> **Tip**: Monitor your app's normal memory usage first, then set the limit to 2–3× the expected peak. For example, if an app normally uses 200 MB, set the limit to 500 MB.

### CPU-Hung Process Detection

Detects when an application is consuming excessive CPU continuously, indicating an infinite loop or spin-wait deadlock.

**How it works:**
1. Each poll tick samples `Process.TotalProcessorTime` (a lightweight kernel query)
2. CPU utilization is computed by comparing samples across ticks: `ΔCpuTime / (Δtime × ProcessorCount)`
3. If CPU usage exceeds 95% for 3 consecutive poll cycles, the process is force-killed
4. Brief CPU spikes are tolerated — only sustained high CPU triggers the kill

This detection is **always active** for Keep Open apps — no configuration needed.

> **Note**: This catches spin-wait deadlocks (high CPU) but not idle deadlocks (0% CPU with frozen UI). Idle deadlocks are caught by [Not Responding Detection](#not-responding-detection) instead.

### Background Zombie Detection

Detects when an application's window disappears but its process remains alive in the background (e.g., Excel closing its window but lingering as a background process).

**How it works:**
1. If the watchdog detects that an app was running (had a window) but now has no window, it checks for background processes with the same name
2. Any found background processes are force-killed
3. The app is then treated as crashed and the normal restart flow begins

This detection is **always active** — no configuration needed.

### Detection Coverage Summary

The following table shows which types of application failures IMEE can detect and handle:

| Failure Type | Example | Detection Method | Auto-Restart? |
|---|---|---|---|
| **UI Freeze** | Infinite loop on UI thread | Not Responding detection | ✅ Yes |
| **Unhandled Exception Dialog** | `NullReferenceException`, `DivideByZeroException` on UI thread | Error Dialog title matching + Title Change detection | ✅ Yes |
| **Stack Overflow** | Infinite recursion | Process terminates to crash detection | ✅ Yes |
| **Out of Memory** | Memory exhaustion | Memory Limit detection (proactive) + crash detection (reactive) | ✅ Yes |
| **Access Violation** | Writing to invalid memory | Process terminates to crash detection | ✅ Yes |
| **UI Deadlock** | Two locks acquired in opposite order | Not Responding detection (UI blocked) + CPU-Hung detection (spin-wait) | ✅ Yes |
| **Background Thread Exception** | Unhandled exception on worker thread | Process terminates to crash detection + zombie detection | ✅ Yes |
| **Environment.FailFast** | Immediate process termination | Process terminates to crash detection | ✅ Yes |
| **Process Crash** | Any unexpected process exit | Window disappears to crash detection | ✅ Yes |
| **Memory Leak** | Gradual memory growth | Memory Limit detection | ✅ Yes |
| **CPU Spin Loop** | Infinite `while(true)` with work | CPU-Hung detection (3 consecutive ticks >95%) | ✅ Yes |
| **Zombie Process** | Window closed but process lingers | Background Zombie detection | ✅ Yes |

---

## Understanding Status Indicators

### Application Status

| Status | Color | What It Means | How Long / What to Do |
|--------|-------|---------------|-----------------------|
| **Stopped** | Black | Application is not running | Click Start to launch |
| **Stopped (Crashed)** | Orange | Application crashed unexpectedly (non-KeepOpen apps) | Investigate the crash, then click Start to relaunch |
| **Stopped (Issue Detected)** | Orange | Health issue detected but automatic action is disabled | Check logs for details, then take manual action |
| **Starting...** | Orange | Application was just launched; grace period is running while the window appears | Waits for the **Start Grace Period** (default 10 s). Changes to **Running** once the window is confirmed. |
| **Running** | Green | Application is running normally | No action needed |
| **Running (Other Group)** | Dark Cyan | This entry's executable path is already running, started from a different group | Stop it in the other group first before starting it here |
| **Stopping...** | Orange | Stop command was sent, waiting for the process to exit | Usually resolves in under 5 s. Changes to **Stopped** automatically. |
| **Restarting (Ns)** | Orange | Application crashed (KeepOpen); counting down before relaunch. **N** = seconds remaining | Countdown is the app's **Start Delay** setting (default 5 s). When it hits 0 it changes to **Starting...** |
| **Starting...** | Orange | Restart countdown finished; relaunching now | Same grace period as a manual start (see **Starting...** above) |
| **Waiting (Ns)** | Orange | Sequential Start All (or Server Mode auto-start); this app is queued. **N** = seconds remaining until launch | Countdown is this app's **Startup Delay** setting. When it hits 0 the app launches and shows **Starting...** |
| **Warning** | Orange | Health issue detected but automatic corrective action is disabled | Check logs for details; take manual action if needed |
| **Failed** | Red | Auto-restart gave up after reaching **Max Retries** | Check the application for errors, then click Start to relaunch manually |

**Timed status note:** The countdown shown in `Restarting (Ns)` and `Waiting (Ns)` updates every second via a dedicated display timer, independent of the main poll interval. The actual seconds shown are exact — "0s" means the action will fire on the next timer tick (within 1 second).

### Group Status Indicators

Each group in the left panel displays a colored circle indicator that summarizes the state of all applications in that group. When only some applications are in a given state, a count suffix (e.g., "2 / 5") appears next to the group name.

| Indicator | Color | What It Means | Count Suffix |
|-----------|-------|---------------|--------------|
| ● | Gray | Group has no applications, or all applications are stopped | No |
| ● | Green | All applications are running | No |
| ● | Green | Some applications are running | "N / T" (running / total) |
| ● | Orange | One or more applications are in a transitional state (Starting, Stopping, Restarting, or Waiting) | No |
| ● | Red | All applications have failed (max retries exhausted) | No |
| ● | Red | Some applications have failed | "N / T" (failed / total) |

**Priority**: If a group has apps in multiple states, the indicator shows the highest-priority state: **Red** (failed) > **Orange** (transitional) > **Green** (running) > **Gray** (stopped/empty).

### Same-Name Applications (Different Directories)

IMEE detects running processes by **executable file name** (e.g., `launcher.exe`), not by full path. This means:

- If you have `C:\App1\launcher.exe` and `C:\App2\launcher.exe` as two separate entries, IMEE **cannot tell them apart at the OS level**.
- If one is running, IMEE will show **both** entries as **Running**, even though only one process is actually active.
- Stopping one will not automatically affect the other, but the watchdog may attempt to restart the wrong one.

**Recommendation:** If you must monitor two different programs that happen to share the same `.exe` filename, place them in **separate groups** and only start one group at a time, or rename one of the executables before adding it to IMEE.

The **Running (Other Group)** status is a specific case of this: it means the **exact same directory path** is running but was started by another group. Two apps with the same filename but *different* directories do not trigger this status — they simply both appear Running simultaneously, which is the known same-name limitation described above.

---

## Settings

The Settings dialog lets you configure the station name, startup behavior, and tune performance and logging parameters. Click the **Settings** button in the header to open it.

### General

| Setting | Description | Default |
|---------|-------------|---------|
| **Station Name** | Identifies the workstation in the title bar | (empty) |
| **Run on Windows Startup** | When enabled, IMEE automatically launches when Windows starts so managed applications are monitored at all times. Uses the current user's registry Run key — no administrator rights required. | Off |

### Performance

| Setting | Range | Default | Description |
|---------|-------|---------|-------------|
| **Status Poll Interval (ms)** | 1000–60000 | 10000 | How often the watchdog checks process statuses. Lower = more responsive but higher CPU usage. |
| **Start Grace Period (seconds)** | 1–120 | 10 | Time after launching an app before the watchdog starts monitoring it. Allows the window to appear. |
| **GC Collect Interval (minutes)** | 5–1440 | 30 | How often a periodic garbage collection runs to prevent long-term memory growth during 24/7 operation. |
| **Storage Save Interval (seconds)** | 5–300 | 30 | Minimum time between throttled disk writes for timer-tick updates. User-initiated saves (Add, Edit, Delete) always write immediately. |

### Logging

| Setting | Range | Default | Description |
|---------|-------|---------|-------------|
| **Log Flush Interval (seconds)** | 1–120 | 10 | How often buffered log entries are written to disk. |
| **Log Buffer Size (entries)** | 10–1000 | 100 | Maximum buffered entries before a flush is forced. |
| **Log Retention (days)** | 1–365 | 7 | Log files older than this are automatically deleted. |

### Server Startup

Click **Server Startup...** in the Settings dialog to configure automatic group launching when the machine starts. This is intended for Windows Server deployments where IMEE should bring managed applications online without any user interaction.

| Setting | Description |
|---------|-------------|
| **Enable Server Mode** | When checked, IMEE auto-starts the configured groups when the form first appears after launch. Requires **Run on Windows Startup** to be enabled so IMEE itself starts with the machine. |
| **Auto-run all groups** | Every group is started on launch. |
| **Auto-run selected groups only** | Only the groups you check in the list are started. All other groups remain stopped. |

**Startup order and delays:** Server Mode uses the same sequential launch engine as **Start All**. Each application's **Startup Delay** (configured per-app in App Settings) is respected — applications launch one after another with the configured gap between them. The status column shows `Waiting (Ns)` for queued apps and `Starting...` while the grace period runs, exactly as it does for a manual Start All.

**Important:** Server Mode only fires once per IMEE session (when the form is first shown). It does not re-trigger if IMEE is minimized to tray and restored.

### Reset Defaults

Click **Reset Defaults** in the Settings dialog to restore all performance and logging settings to their defaults. The station name is not affected by reset.

All settings are saved in `settings.json` and persist across restarts. Changes take effect immediately — no restart required.

---

## Logs

IMEE logs all operations to daily log files.

### Viewing Logs
- Click the **Logs** button to open the logs folder in File Explorer
- Log files are named `YYYY-MM-DD.log`

### What Gets Logged
- Application starts and stops (including PIDs)
- Unauthorized launch detections
- Crash detections and auto-restart attempts
- Health monitoring events (not responding, error dialogs, memory limit breaches, CPU-hung detection, title changes)
- Group and application add/edit/delete operations
- Individual setting changes (both app-level and global settings)
- Errors and warnings
- Startup terminations

### Log Format
```
[HOSTNAME/USER IP][2025-06-15 14:30:00][INFO][PID:12345][StartApplication @ ProcessManager.cs] - Successfully started notepad (PID: 9876)
```

### Log Retention
Logs older than the configured retention period (default: 7 days) are automatically deleted. You can change this in **Settings** to **Log Retention (days)**.

### Real-Time Log Monitoring
Open a PowerShell window and run:
```powershell
Get-Content -Path "logs\2025-06-15.log" -Wait -Tail 20
```

---

## Security: Unauthorized Launch Prevention

IMEE enforces that managed applications can **only** be launched through its interface.

### How It Works
1. When you start an app through IMEE, it's added to an authorized list
2. At each poll interval (default: 10 seconds, configurable in Settings), the watchdog checks all managed applications
3. If a managed app is running but **not** in the authorized list, it was launched externally
4. The unauthorized process is **killed immediately**
5. A warning notification is shown (once per attempt)

### Scenarios

| Scenario | What Happens |
|----------|-------------|
| User starts app via IMEE | ✅ Allowed — added to authorized list |
| User double-clicks the .exe directly | ❌ Killed within one poll interval + warning shown |
| User opens app via shortcut | ❌ Killed within one poll interval + warning shown |
| App was running before IMEE started | ❌ Terminated on startup + notification |
| App crashes and auto-restarts (Keep Open) | ✅ Allowed — re-authorized automatically |
| Same app running in another group | ⚠️ Shown as "Running (Other Group)" — must stop in the other group first |

### Why This Matters
- Ensures only one instance of each managed application runs
- Provides an audit trail of who started what and when
- Prevents circumventing instance management by launching apps directly

---

## System Tray

IMEE can minimize to the system tray to continue monitoring applications in the background without occupying taskbar space.

### Minimizing to Tray
When you close the IMEE window (click the X button), a prompt appears with three options:
- **Yes** — Minimize to tray: IMEE hides from the taskbar and continues running in the background. A tray icon appears with a balloon notification.
- **No** — Exit: IMEE closes completely. Managed applications continue running but are no longer monitored.
- **Cancel** — Cancel: The close is cancelled and the window remains open.

### Restoring from Tray
- **Double-click** the tray icon to restore the main window
- **Right-click** the tray icon for a context menu with **Restore** and **Exit** options

### Tray Context Menu

| Option | Description |
|--------|-------------|
| **Restore** | Restores the main window from the tray |
| **Exit** | Prompts for confirmation, then exits IMEE completely |

> **Note**: While minimized to tray, IMEE continues all monitoring, watchdog checks, and auto-restart operations normally.

---

## Frequently Asked Questions

### Can I manage applications that require Administrator privileges?
No. IMEE runs at standard user level and cannot manage elevated processes. If your managed application requires elevation, IMEE won't be able to start or stop it.

### What happens if I close IMEE while applications are running?
If you choose **Exit** (not minimize to tray), the managed applications **continue running** — IMEE does not stop them on exit. However, the watchdog will no longer monitor them. When you restart IMEE, any still-running managed apps will be terminated.

If you choose **Minimize to tray**, IMEE continues running in the background and all monitoring remains active.

### Can I manage the same application in multiple groups?
Yes, you can add the same executable to different groups. Each entry is tracked independently. However, you cannot run the same application simultaneously from multiple groups. If you try to start an application that is already running in another group, IMEE will show a warning telling you which group it's running in. You must stop it in the other group first before starting it in a new one. The status column will show **Running (Other Group)** in dark cyan for entries whose executable is running from a different group.

### Does IMEE modify my applications?
No. IMEE only starts and stops processes. It never modifies any application files.

### What if my application doesn't have a visible window?
IMEE detects running applications by checking for a main window handle. Applications that run purely in the background (no window at all) may not be detected as "Running" and could be falsely treated as crashed. However, applications minimized to the system tray (which have hidden top-level windows) are correctly detected as running.

### What if my application needs a launcher script to start?
Use the **Launcher Script / Exe** feature in the Edit dialog:
1. Click **Edit** on the application
2. In the **Launcher Script / Exe** section, click **Browse** to select a launcher file (`.exe`, `.bat`, `.cmd`, `.vbs`, or `.ps1`)
3. Click OK

IMEE will execute the launcher to start the application but will still monitor the **primary executable** (from Application Path) for running status, health checks, and crash detection.

### What if my application shows an error dialog (unhandled exception)?
IMEE has multiple layers of detection for error dialogs:
1. **Error Dialog Pattern Matching** — If the dialog's window title contains a known error keyword (e.g., "unhandled exception", "NullReferenceException", "fatal error"), it is detected automatically and the process is force-killed after 2 consecutive poll cycles.
2. **Not Responding Detection** — If the error dialog blocks the UI thread and the window becomes unresponsive, it is detected via `Process.Responding` and force-killed after the configured timeout.
3. **Title Change Detection** — If the error dialog's title doesn't match any known pattern (e.g., a WinForms `ThreadExceptionDialog` that uses the app's product name as its title), enable **Detect Title Change** in the Edit dialog. The watchdog will detect the title change and force-kill the process.

All three mechanisms trigger auto-restart for Keep Open applications. For non-Keep Open apps, IMEE will log and notify about the issue without taking automatic action.

### How do I set a memory limit for my application?
1. Click **Edit** on the application
2. Enable **Keep Open** (required for health monitoring controls)
3. Set **Memory Limit (MB)** to your desired maximum (e.g., 500 for 500 MB)
4. Click OK
5. If the application's working set exceeds this limit, it will be force-killed and auto-restarted

> **Tip**: Monitor your app's normal memory usage in Task Manager first, then set the limit to 2–3× the expected peak.

### What is the "Last Exit Code" in the Edit dialog?
The Last Exit Code shows the exit code from the most recent process termination. A value of `0` typically means the process exited normally. A non-zero value (shown in red with "abnormal") usually indicates a crash or error. Common non-zero codes:
- **-1073741819** (0xC0000005) — Access violation
- **-1073740791** (0xC0000409) — Stack buffer overrun
- **-532462766** (0xE0434352) — .NET unhandled exception

### Can I change the watchdog polling interval?
Yes. Click **Settings** and adjust the **Status Poll Interval** value. The default is 10000ms (10 seconds). Lower values make detection faster but use more CPU.

### Can I make IMEE start automatically with Windows?
Yes. Click **Settings** and check **Run on Windows Startup**. This adds IMEE to the current user's Windows startup registry — no administrator rights required.

### Where is my data stored?
All data files are in the same folder as `IntelligentMutexExecutionEnvironment.exe`:
- `applications.json` — your managed applications
- `groups.json` — your groups
- `settings.json` — station name, startup, performance, and logging configuration
- `logs/` — daily log files

### How do I reset everything?
Delete `applications.json`, `groups.json`, and `settings.json`, then restart IMEE.

---

## Troubleshooting

### Application Won't Start
1. Check that the file path is correct — click **Edit** and verify the Directory
2. If using a launcher, verify the launcher path also exists
3. Make sure the `.exe` file exists at that path
4. Check the log file for error messages
5. Verify you have permission to run the application

### Application Won't Stop
1. Check the log file for "Access denied" errors
2. The application may have already closed externally
3. Click **Refresh** to force a status update
4. Try stopping it via Task Manager as a last resort

### Status Stuck on "Starting..."
The application has been launched but its window hasn't appeared yet. Possible causes:
- The application is still loading (wait for the grace period, default: 10 seconds, configurable in Settings)
- The application runs without a visible window (not supported for status detection)
- The application crashed immediately after launch (check logs)

### Status Shows "Failed"
The application has exhausted its maximum retry attempts. To fix:
1. Investigate why the application keeps crashing
2. Click **Edit** on the application
3. Reset the **Retry Count** to 0
4. Click OK
5. Click **Start** to try again

### Application Keeps Getting Killed by Memory Limit
The memory limit may be set too low for the application's normal operation:
1. Click **Edit** on the application
2. Either increase the **Memory Limit (MB)** value or set it to `0` to disable
3. Click OK

> **Tip**: Use Task Manager to observe the application's working set under normal conditions and set the limit well above that baseline.

### Application Keeps Getting Killed by Title Change Detection
The application legitimately changes its window title (e.g., showing document names, status text, or progress):
1. Click **Edit** on the application
2. Uncheck **Detect Title Change**
3. Click OK

### Application Keeps Getting Killed by CPU-Hung Detection
The application legitimately uses sustained high CPU (e.g., rendering, video encoding):
- CPU-hung detection is always active and cannot be disabled per-app. However, it requires **95% CPU usage across all cores for 3 consecutive poll cycles** (default: 30 seconds total). Most legitimate high-CPU operations won't trigger this unless they consume 100% of all CPU cores for an extended period.
- If this is a problem, increase the **Status Poll Interval** in Settings to give the detection more time between checks.

### Unauthorized Launch Warning Keeps Appearing
This means someone (or something) keeps trying to launch the managed application outside of IMEE. Check for:
- Scheduled tasks that launch the application
- Startup items in Windows
- Other users launching the application directly

### Logs Not Being Created
1. Make sure IMEE has write permission in its directory
2. Check if the `logs/` folder exists — if not, try creating it manually
3. Restart IMEE

### Data File Corruption
If `applications.json` or `groups.json` becomes corrupted:
1. Check for a `.bak` or `.tmp` backup file in the same directory
2. Rename the backup file to replace the corrupted file
3. If no backup exists, delete the corrupted file (you'll lose that data)
4. Restart IMEE

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

