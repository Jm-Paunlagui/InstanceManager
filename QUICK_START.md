# Instance Manager — Quick Start Guide

## First Time Setup

1. **Launch the Application**
   - Double-click `InstanceManager.exe`
   - The application window will open in the **bottom-right corner** of the screen
   - The list will be empty on first run

2. **Add Your First Application**
   - Click the **Add** button
   - Browse to an executable file (e.g., `C:\Windows\System32\notepad.exe`)
   - Click OK
   - The application will appear in the list with Index 1, Status "Stopped", Last Start "Never", Last Stop "Never"

---

## Testing the Application

### Test 1: Start an Application
1. Select "notepad" (or your added app) from the list
2. Click **Start** button
3. The application should launch
4. Status column should show **"Running"** in green text
5. **Last Start** column should show the current date and time
6. Check logs: `logs\[today's date].log` for start event

### Test 2: Duplicate Detection (Add)
1. Click **Add** button
2. Browse to the **same** application you just added (e.g., notepad.exe)
3. Click OK
4. You should see a warning: *"Application 'notepad' already exists in the management list"*
5. Application should **NOT** be added again
6. Check logs for duplicate detection warning

### Test 3: Try Starting Again (Mutex Test)
1. With the application still running, select it again
2. Click **Start** button
3. You should see message: *"notepad is already running!"*
4. This proves the mutex functionality works

### Test 4: Stop an Application
1. Select the running application
2. Click **Stop** button
3. Confirm the action
4. Application should close
5. Status should change to **"Stopped"** in black text
6. **Last Stop** column should show the current date and time
7. Check logs for stop event

### Test 5: Add Multiple Applications
1. Add several different applications (e.g., calc.exe, mspaint.exe)
2. Each should get a unique index (1, 2, 3, etc.)
3. All are saved in `applications.json`

### Test 6: Real-time Monitoring (External Stop)
1. Start an application through Instance Manager
2. Manually close it using Task Manager or its own close button
3. Wait 2–3 seconds
4. Instance Manager should automatically detect it stopped
5. Status should update to **"Stopped"**
6. **Last Stop** should be recorded automatically
7. Check logs for: *"was stopped externally"*

### Test 7: Unauthorized External Launch Detection ??
> This is the core security feature — the **Authorized Process Watchdog**.

1. Add an application to Instance Manager (e.g., notepad.exe)
2. Make sure it is **not running** (Status = "Stopped")
3. **Do NOT** click Start — instead, go to File Explorer and **double-click** the .exe directly
4. Wait 2–3 seconds
5. Instance Manager should:
   - **Kill the process** automatically
   - Show a warning: *"notepad was launched outside of Instance Manager and has been terminated."*
6. Status remains **"Stopped"**
7. Check logs for: *"Unauthorized launch detected"*

### Test 8: Edit an Application
1. Select an application
2. Click **Edit** button
3. Browse to a different executable
4. Click OK
5. Application name and path should update

### Test 9: Edit to Duplicate Prevention
1. Add two different applications
2. Select the first application
3. Click **Edit** button
4. Try to change it to the **same path** as the second application
5. You should see a warning about duplicate
6. Edit should be prevented

### Test 10: Delete an Application
1. Select an application
2. Click **Delete** button
3. Confirm deletion
4. Application is removed from list (executable file remains on disk)

### Test 11: Persistence
1. Add some applications and start a few
2. Close Instance Manager
3. Reopen Instance Manager
4. All your applications should still be in the list
5. Running status should be accurately detected
6. **Last Start** and **Last Stop** timestamps should be preserved
7. Apps that were running before restart are marked as authorized (not killed)

### Test 12: Graceful Onboarding
1. Start an application **before** launching Instance Manager (e.g., open notepad manually)
2. Add that application to Instance Manager
3. It should show as **"Running"** and should **NOT** be killed
4. This proves graceful onboarding — apps running before Instance Manager are authorized

---

## ListView Columns

| Column | Description |
|--------|-------------|
| **Index** | Auto-incremented ID |
| **Application** | Name of the executable (without .exe) |
| **Directory** | Full path to the executable |
| **Status** | "Running" (green) or "Stopped" (black) |
| **Last Start** | Timestamp of last start, or "Never" |
| **Last Stop** | Timestamp of last stop, or "Never" |

---

## Log File Example

After testing, check your log file at `logs\YYYY-MM-DD.log`:

```
[YOUR-PC/YourUser 192.168.1.100][2026-02-05 14:30:00][INFO][PID:12345][Main @ Program.cs] - InstanceManager application starting
[YOUR-PC/YourUser 192.168.1.100][2026-02-05 14:30:01][INFO][PID:12345][DetectAlreadyRunningApps @ Form1.cs] - 'notepad' was already running at startup, marking as authorized
[YOUR-PC/YourUser 192.168.1.100][2026-02-05 14:30:01][INFO][PID:12345][LoadApplications @ Form1.cs] - Loaded 3 applications
[YOUR-PC/YourUser 192.168.1.100][2026-02-05 14:30:15][INFO][PID:12345][AddApplication @ StorageService.cs] - Added application: calc (Index: 4)
[YOUR-PC/YourUser 192.168.1.100][2026-02-05 14:30:20][INFO][PID:12345][StartApplication @ ProcessManager.cs] - Successfully started calc (PID: 9876)
[YOUR-PC/YourUser 192.168.1.100][2026-02-05 14:30:45][INFO][PID:12345][StopApplication @ ProcessManager.cs] - Gracefully stopped calc (PID: 9876)
[YOUR-PC/YourUser 192.168.1.100][2026-02-05 14:31:10][WARN][PID:12345][HandleUnauthorizedLaunch @ Form1.cs] - Unauthorized launch detected for 'EXCEL' - killing process
[YOUR-PC/YourUser 192.168.1.100][2026-02-05 14:31:10][WARN][PID:12345][HandleUnauthorizedLaunch @ Form1.cs] - User notified about unauthorized launch of 'EXCEL'
```

---

## JSON Storage Example

Check `applications.json`:

```json
[
  {
    "Index": 1,
    "AppName": "DryCabinet",
    "Directory": "D:\\Projects\\Desktop\\DryCabinet\\bin\\Release\\DryCabinet.exe",
    "AddedDate": "2026-02-05T16:00:00",
    "IsRunning": false,
    "LastStart": "2026-02-05T16:05:30",
    "LastStop": "2026-02-05T16:10:45"
  },
  {
    "Index": 2,
    "AppName": "EXCEL",
    "Directory": "C:\\Program Files (x86)\\Microsoft Office\\root\\Office16\\EXCEL.EXE",
    "AddedDate": "2026-02-05T17:16:20",
    "IsRunning": false,
    "LastStart": "2026-02-05T17:16:25",
    "LastStop": null
  }
]
```

---

## Performance Testing

### CPU Usage Test
1. Add 5–10 applications
2. Start several of them
3. Open Task Manager
4. Check CPU usage of `InstanceManager.exe`
5. Should be **< 1%** (mostly 0%) with 2-second status updates

### Memory Test
1. Leave Instance Manager running for an extended period
2. Monitor memory usage in Task Manager
3. Should remain stable (**< 50 MB** typically)

### Watchdog Responsiveness Test
1. Add a managed application
2. Launch it externally (double-click)
3. Measure time until Instance Manager kills it
4. Should be **within 2 seconds**

---

## Troubleshooting

### Application Won't Start
- Check if file path is correct (Edit the application)
- Ensure the executable file exists
- Check logs for error messages
- Verify file permissions

### Application Won't Stop
- Check logs for "Force killed" message
- Application might have been closed externally
- Click Refresh to update status

### Unauthorized Launch Not Detected
- Ensure the application is in the managed list
- Ensure Instance Manager is running
- Wait up to 2 seconds for the timer tick
- Check logs for watchdog activity

### "Illegal characters in path" Error
- This was fixed — the JSON deserializer now properly unescapes `\\` to `\`
- If you still see this, delete `applications.json` and re-add your applications

### Logs Not Creating
- Verify write permissions in application directory
- Check if `logs/` folder exists
- Manually create `logs/` folder if needed

### Status Not Updating
- Wait 2–3 seconds for timer to tick
- Click Refresh button to force update
- Check if application name matches process name

### MessageBox Appearing in Center of Screen
- Should no longer happen — CustomMessageBox is centered on the form
- If it does, check that `MessageBoxHelper` is being used instead of `MessageBox.Show`

---

## Advanced Usage

### Command Prompt Monitoring
```cmd
# View logs in real-time
powershell Get-Content -Path "logs\2026-02-05.log" -Wait -Tail 10

# Check if process is running
tasklist | findstr notepad

# View JSON data
type applications.json
```

### Stress Test
1. Add 20+ applications
2. Start 10–15 simultaneously through Instance Manager
3. Monitor performance (CPU < 1%, Memory < 50 MB)
4. Try launching some externally — they should be killed
5. Stop all
6. Verify all logs were written correctly

---

## LRA Specific Testing

For your LRA reflow sorter scenario:

1. Add your production application to Instance Manager
2. Set Instance Manager to start on Windows startup
3. Only use Instance Manager to launch the application
4. If anyone tries to launch the app directly, it will be **automatically killed**
5. Monitor logs for any unauthorized launch attempts
6. Verify that only one instance runs at a time
7. Check Last Start / Last Stop timestamps for audit trail

---

## Expected Behavior

? **Should Work:**
- Prevent multiple instances of same application
- Detect and kill applications launched outside Instance Manager
- Real-time status monitoring (2-second interval)
- Persistent storage across restarts (including timestamps)
- Graceful and force shutdown
- Comprehensive logging with machine identifier
- Minimal CPU/memory impact
- Duplicate detection on Add and Edit
- MessageBox dialogs centered on form
- Form positioned in bottom-right corner

?? **Known Limitations:**
- Cannot manage applications requiring elevated permissions (Run as Administrator)
- 2-second delay for unauthorized launch detection (timer interval)
- Basic process name matching (won't detect renamed executables)
- MessageBox dialogs are custom — not native Windows dialogs

---

## Support

For issues or questions:
1. Check logs at `logs/YYYY-MM-DD.log` first
2. Verify application paths in the list
3. Test with simple applications (notepad, calc) before production apps
4. Review `README.md` for architecture details
5. Review `IMPLEMENTATION_SUMMARY.md` for full technical documentation

---

## Repository
https://github.com/Jm-Paunlagui/InstanceManager
