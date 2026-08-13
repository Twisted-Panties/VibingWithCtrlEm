# Vibing With CtrlEm application

### Purpose and Scope:
Vibing With CtrlEm is a local Windows desktop application that watches a user-selected text log, interprets configured events, and translates those events into safe haptic commands sent to connected devices through a running Intiface Central/Intiface Engine server using the Buttplug.io client protocol.

The application **does not** pair with, scan for, or directly control Bluetooth hardware. Intiface is responsible for hardware discovery and pairing. Vibing With CtrlEm acts only as a Buttplug client over a user-configurable local or network WebSocket endpoint.

### Goals:
- Tail a live log file without locking or modifying it.
- Match plain-text events and map them to vibration patterns.
- Each command to be processed will be described in a config file
- The communication to Initface Central will be configurable, but the default is ws://127.0.0.1:12345
- The configuration will be stored in a simple JSON config file
- There may be a new log file everyday, so the application must always use the most recently modified file
- Once a path is set and a log file identified, the application will start reading the last command of the log file, and continue until the application closes
- Keep operation local and offline; do not transmit log contents or device data to a cloud service.

### Explicit non-goals for the first release
- Embedded Intiface/Buttplug server or direct Bluetooth control.
- Mobile, macOS, Linux, or web clients.
- Automatic device scanning/pairing from CtrlEm-Vibe.
- A scripting engine, plug-in marketplace, cloud sync, or multi-user control.
- Parsing arbitrary binary logs.

### Platform:
- This application will be written in the latest stable C#.NET, and use WPF for the UI
- The application should be easily distributable, without requiring the user to download extra files
- The application distributable should be as small as is feasible

### Log File
- A sample log file is located in the same directory as this SPEC.md file, named "commands_2026-06-07.log"
- Default location of the log files should be %UserProfile%\Documents\CtrlEm Client\Logs
- The log file should be read non-locking and non-exclusive.

### UI:
- The application will have a basic UI, and be able to run from the tray
- The UI will allow users to connect to Intiface Central, select a toy, and test to make sure the toy is connected and working
- The UI will allow the user to see all the commands, with the keyword, intensity and duration
- The UI will show the last command processed
- The UI should be a stack of three major areas:
	Top: Title and application description
	Middle: Three panels as described below
		Left panel: shows the Intiface Central URL (editable and saved in the  config file), connected status, and a Connect/Disconnect button
		Middle Panel: shows the path to the log file (browseable and saved in the config file), and the current log file to be read from
		Right Panel: shows a scrollable list of the commands that will be processed, with the intensity and duration.
	Bottom: Start/Stop buttons and any status messages
- The Start button will start sending commands to the toy
- The Stop button will stop sending commands to the toy immediately

### Config File:
- The Intiface Central URL/configuration is stored in the config file
- The path to the log files is stored in the config file
- Each command will be described in the config file, with the keyword, the intensity (from 0.0 to 1.0), and the duration (in milliseconds)



