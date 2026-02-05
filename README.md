# Intelligent Mutex Execution Environment

## Overview
This application prevents simultaneous opening of applications through intelligent instance management. It provides a centralized interface to manage, monitor, and control multiple applications.

## Features

### Core Functionality
- **Add Applications**: Browse and add executable files to the manager
- **Edit Applications**: Update application paths and configurations
- **Delete Applications**: Remove applications from management (doesn't delete the actual file)
- **Start Applications**: Launch applications with instance checking
- **Stop Applications**: Gracefully or forcefully terminate running applications
- **Real-time Monitoring**: Automatic status updates every 2 seconds

### Data Storage
- Applications are stored in JSON format (`applications.json`)
- No database required - lightweight file-based storage
- Stores: Index, Directory, AppName, AddedDate, IsRunning status

### Process Management
- Checks if application is already running before starting
- Prevents multiple instances
- Graceful shutdown with fallback to force kill (3-second timeout)
- Real-time process monitoring with minimal CPU impact

### Logging (Custom SimpleLogger)
All actions are logged with a special format:

```
[MACHINE_IDENTIFIER][TIMESTAMP][LEVEL][PID: processId][FUNCTION @ FILE:LINE] - MESSAGE
```

**Example:**
```
[JmPaunlagui/user 192.168.1.100][2025-12-03 14:30:00][INFO][PID:23632][AddApplication @ StorageService.cs] - Added application: Calculator (Index: 1)
```

**Logged Events:**
- Application startup/shutdown
- Add/Edit/Delete operations
- Start/Stop application attempts
- Process status checks
- All errors and warnings

**Log Location:** `logs/[date].log` (auto-generated)

**Log Levels:** INFO, DEBUG, WARN, ERROR, FATAL

## Architecture

### Files Structure
```
InstanceManager/
??? Form1.cs                        - Main UI and logic
??? Form1.Designer.cs               - UI controls
??? Program.cs                      - Application entry point with logging
??? Models/
?   ??? ManagedApplication.cs       - Data model for applications
??? Services/
?   ??? StorageService.cs           - JSON storage operations
?   ??? ProcessManager.cs           - Process monitoring and control
??? Utilities/
?   ??? SimpleLogger.cs             - Custom logging implementation
??? NLog.config                     - Configuration (optional)
??? applications.json               - Application data (auto-generated)
??? logs/                           - Log files (auto-generated)
```

### Key Classes

**ManagedApplication**
- Properties: Index, AppName, Directory, AddedDate, IsRunning
- Represents a managed application

**StorageService**
- Custom JSON serialization/deserialization (.NET 4.0 compatible)
- CRUD operations for applications
- Persistent storage management

**ProcessManager**
- Check if application is running
- Start application with instance validation
- Stop application (graceful with fallback to force)
- Get running instance count

**SimpleLogger**
- Thread-safe logging with lock mechanism
- Automatic log directory creation
- Daily log file rotation (YYYY-MM-DD.log format)
- Custom log format with machine identifier, timestamp, PID, and location
- Silent failure - never crashes the application

## Performance Considerations

### Minimal CPU Impact
- Timer-based status updates (2-second interval)
- Efficient process name checking
- No continuous polling - event-driven architecture
- Lightweight JSON storage (no database overhead)
- Thread-safe logging with minimal lock contention

### Memory Efficiency
- Proper disposal of process objects
- Timer cleanup on form close
- Minimal memory footprint for monitoring
- No external logging dependencies

## Usage

### Adding an Application
1. Click "Add" button
2. Browse to the executable (.exe) file
3. Application is automatically added with index and name
4. **Note:** If the application already exists, you'll see a warning and it won't be added again

### Starting an Application
1. Select an application from the list
2. Click "Start" button
3. System checks if already running
4. If not running, launches the application
5. Status updates to "Running" (green)

### Stopping an Application
1. Select a running application
2. Click "Stop" button
3. Confirmation dialog appears
4. System attempts graceful shutdown
5. Force kills if graceful shutdown fails (3-second timeout)

### Editing an Application
1. Select an application
2. Click "Edit" button
3. Browse to new executable path
4. Application details are updated
5. **Note:** You cannot edit an application to a path that already exists in the list

### Deleting an Application
1. Select an application
2. Click "Delete" button
3. Confirm deletion
4. Application removed from management (file not deleted)

## Technical Details

### .NET Framework
- Target: .NET Framework 4.0
- Windows Forms application
- Compatible with Windows XP and above
- No external dependencies required

### Custom JSON Serialization
- Built-in JSON parser for .NET 4.0 compatibility
- Handles escaping and unescaping of special characters
- Robust error handling and recovery

### Logging Format Components
1. **MACHINE_IDENTIFIER**: [hostname/username ip]
2. **TIMESTAMP**: [YYYY-MM-DD HH:MM:SS]
3. **LEVEL**: [INFO|ERROR|WARN|DEBUG|FATAL]
4. **PID**: [PID:12345]
5. **LOCATION**: [functionName @ file.cs]
6. **MESSAGE**: Actual log message

## Error Handling

### Comprehensive Logging
- All exceptions are caught and logged
- User-friendly error messages
- Detailed technical logs for debugging
- Silent log failures to prevent application crashes

### Foolproof Design
- File existence checks before operations
- Process validation before start/stop
- Data validation and error recovery
- Graceful degradation on failures
- Thread-safe operations

## Future Enhancements
- CSV export/import functionality
- Application groups/categories
- Scheduled application launching
- Remote monitoring capabilities
- Application crash detection and auto-restart
- Performance metrics and reporting
- Command-line interface
- System tray integration

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
- No installation required - fully portable
- Logs and data files are created in the same directory

## License
Internal LRA Reflow Sorter Tool

## Author
Developed for LRA Application Instance Management
