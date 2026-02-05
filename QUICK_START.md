# Instance Manager - Quick Start Guide

## First Time Setup

1. **Launch the Application**
   - Double-click `InstanceManager.exe`
   - The application window will open with an empty list

2. **Add Your First Application**
   - Click the **Add** button
   - Browse to an executable file (e.g., `C:\Windows\System32\notepad.exe`)
   - Click OK
   - The application will appear in the list with Index 1

## Testing the Application

### Test 1: Start an Application
1. Select "notepad" (or your added app) from the list
2. Click **Start** button
3. The application should launch
4. Status column should show "Running" in green text
5. Check logs: `logs\[today's date].log` for start event

### Test 1.5: Duplicate Detection Test
1. Click **Add** button
2. Browse to the same application you just added (e.g., notepad.exe)
3. Click OK
4. You should see a warning: "Application 'notepad' already exists in the management list"
5. Application should NOT be added again
6. Check logs for duplicate detection warning

### Test 2: Try Starting Again (Mutex Test)
1. With the application still running, select it again
2. Click **Start** button
3. You should see message: "Application is already running!"
4. This proves the mutex functionality works

### Test 3: Stop an Application
1. Select the running application
2. Click **Stop** button
3. Confirm the action
4. Application should close
5. Status should change to "Stopped" in black text
6. Check logs for stop event

### Test 4: Add Multiple Applications
1. Add several different applications (e.g., calc.exe, mspaint.exe)
2. Each should get a unique index (1, 2, 3, etc.)
3. All are saved in `applications.json`

### Test 5: Real-time Monitoring
1. Start an application through Instance Manager
2. Manually close it using Task Manager or its own close button
3. Wait 2-3 seconds
4. Instance Manager should automatically detect it stopped
5. Status should update to "Stopped"

### Test 6: Edit an Application
1. Select an application
2. Click **Edit** button
3. Browse to a different executable
4. Click OK
5. Application name and path should update

### Test 6.5: Edit to Duplicate Prevention
1. Add two different applications
2. Select the first application
3. Click **Edit** button
4. Try to change it to the same path as the second application
5. You should see a warning about duplicate
6. Edit should be prevented

### Test 7: Delete an Application
1. Select an application
2. Click **Delete** button
3. Confirm deletion
4. Application is removed from list (executable file remains on disk)

### Test 8: Persistence
1. Add some applications and start a few
2. Close Instance Manager
3. Reopen Instance Manager
4. All your applications should still be in the list
5. Running status should be accurately detected

## Log File Example

After testing, check your log file at `logs\YYYY-MM-DD.log`:

```
[YOUR-PC/YourUser 192.168.1.100][2025-01-15 14:30:00][INFO][PID:12345][Main @ Program.cs] - InstanceManager application starting
[YOUR-PC/YourUser 192.168.1.100][2025-01-15 14:30:01][INFO][PID:12345][LoadApplications @ StorageService.cs] - Loaded 0 applications from storage
[YOUR-PC/YourUser 192.168.1.100][2025-01-15 14:30:15][INFO][PID:12345][AddApplication @ StorageService.cs] - Added application: notepad (Index: 1)
[YOUR-PC/YourUser 192.168.1.100][2025-01-15 14:30:20][INFO][PID:12345][StartApplication @ ProcessManager.cs] - Successfully started notepad (PID: 9876)
[YOUR-PC/YourUser 192.168.1.100][2025-01-15 14:30:45][INFO][PID:12345][StopApplication @ ProcessManager.cs] - Gracefully stopped notepad (PID: 9876)
```

## JSON Storage Example

Check `applications.json`:

```json
[
  {
    "Index": 1,
    "AppName": "notepad",
    "Directory": "C:\\Windows\\System32\\notepad.exe",
    "AddedDate": "2025-01-15T14:30:15",
    "IsRunning": false
  },
  {
    "Index": 2,
    "AppName": "calc",
    "Directory": "C:\\Windows\\System32\\calc.exe",
    "AddedDate": "2025-01-15T14:31:00",
    "IsRunning": false
  }
]
```

## Performance Testing

### CPU Usage Test
1. Add 5-10 applications
2. Start all of them
3. Open Task Manager
4. Check CPU usage of `InstanceManager.exe`
5. Should be < 1% (mostly 0%) with 2-second status updates

### Memory Test
1. Leave Instance Manager running for extended period
2. Monitor memory usage in Task Manager
3. Should remain stable (< 50MB typically)

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

### Logs Not Creating
- Verify write permissions in application directory
- Check if logs folder exists
- Manually create logs folder if needed

### Status Not Updating
- Wait 2-3 seconds for timer to tick
- Click Refresh button to force update
- Check if application name matches process name

## Advanced Usage

### Command Prompt Test
```cmd
# View logs in real-time
powershell Get-Content -Path "logs\2025-01-15.log" -Wait -Tail 10

# Check if process is running
tasklist | findstr notepad

# View JSON data
type applications.json
```

### Stress Test
1. Add 20+ applications
2. Start 10-15 simultaneously
3. Monitor performance
4. Stop all
5. Verify all logs were written correctly

## LRA Specific Testing

For your LRA reflow sorter scenario:

1. Add your production application to Instance Manager
2. Set Instance Manager to start on Windows startup
3. Only use Instance Manager to launch the application
4. Verify mutex prevention across multiple sorter lines
5. Monitor logs for any simultaneous launch attempts
6. Check that only one instance runs at a time

## Expected Behavior

? **Should Work:**
- Prevent multiple instances of same application
- Real-time status monitoring
- Persistent storage across restarts
- Graceful and force shutdown
- Comprehensive logging
- Minimal CPU/memory impact

? **Known Limitations:**
- Cannot manage applications with special permissions
- Cannot detect instances started before Instance Manager
- 2-second delay for status updates
- Basic process name matching (won't detect renamed processes)

## Support

For issues or questions:
1. Check the logs first
2. Verify application paths
3. Test with simple applications (notepad, calc) first
4. Review README.md for detailed documentation
