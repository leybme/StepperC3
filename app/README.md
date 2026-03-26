# StepperC3 Companion App

WPF desktop application for sequencing and executing motor commands against an ESP32-C3 stepper controller daisy chain.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- Windows 10/11 (WPF requirement)

## Build

```sh
cd app
dotnet build
```

To publish a standalone Release build:

```sh
dotnet publish StepperC3.App -c Release
```

Output is written to `StepperC3.App/bin/Release/net10.0-windows/publish/`.

## Run

```sh
dotnet run --project StepperC3.App
```

Or launch the published executable directly:

```sh
StepperC3.App/bin/Release/net10.0-windows/publish/StepperC3.App.exe
```

## Tests

```sh
dotnet test
```

76 xUnit tests covering models and services in `StepperC3.Core.Tests`.

## Application Layout

The window has three panels:

| Panel | Content |
|---|---|
| **Left** | Step type palette (grouped by category), motor ID input, serial connection controls |
| **Center** | Task list name/description, ordered step list with action toolbar |
| **Right** | Selected step properties editor, execution log output |

### Toolbar & Menu

- **File → New / Open / Save / Save As** — Create, load, and persist task lists as JSON files
- **Keyboard shortcuts** — `Ctrl+N` (New), `Ctrl+O` (Open), `Ctrl+S` (Save)
- **▶ Run** — Execute the task list against the connected motor chain
- **⏹ Stop** — Cancel a running execution

### Adding Steps

1. Select a step type from the categorized palette on the left
2. Set the **Motor ID** (0–7, matching the board's position in the daisy chain)
3. Click **➕ Add Step**

### Editing Steps

- **Select** a step in the center list to view/edit its properties in the right panel
- **⬆ Up / ⬇ Down** — Reorder steps
- **📋 Duplicate** — Copy a step
- **🗑 Remove** — Delete the selected step
- **⏯ Toggle** — Enable or disable a step (disabled steps are skipped during execution)
- **🧹 Clear All** — Remove all steps
- **Drag and drop** — Reorder steps by dragging within the list

### Connecting to Hardware

1. Click 🔄 to refresh the available serial ports
2. Select the COM port for your ESP32-C3 (Board 0 in the chain)
3. Click **🔌 Connect** — Status indicator turns green when connected
4. Click **🔓 Disconnect** when done

Connection settings: **115200 baud, 8-E-1** (matching the firmware protocol).

### Running a Task List

1. Connect to the motor chain (see above)
2. Click **▶ Run** — Steps execute sequentially; progress bar and log update in real time
3. Click **⏹ Stop** to cancel at any point

Disabled steps (toggled off) are skipped. `Wait` steps pause on the client side; `RunCommand` steps launch an external process.

## Step Types

### Motion

| Step | Firmware Command | Description |
|---|---|---|
| Move To | `<id> MOVETO <pos>` | Move to absolute step position |
| Move By | `<id> MOVE <steps>` | Move by relative distance |
| Go Home | `<id> GOHOME` | Return to position 0 |
| Find Home | `<id> FINDHOME` | Auto-home using end-switch detection |
| Stop | `<id> STOP` | Immediate stop |

### Configuration

| Step | Firmware Command | Description |
|---|---|---|
| Set Speed | `<id> SETSPD <hz>` | Set speed in steps/second |
| Set Acceleration | `<id> SETACCEL <n>` | Set acceleration in steps/s² |
| Set Current | `<id> SETCUR <mA>` | Set RMS current |
| Set Microstep | `<id> SETSTEP <n>` | Set microstep resolution (1/2/4/8/16/32/64/256) |
| Enable | `<id> ENABLE` | Enable motor driver |
| Disable | `<id> DISABLE` | Disable motor driver |
| Flip Direction | `<id> FLIPDIR` | Toggle direction polarity |

### Flow Control

| Step | Description |
|---|---|
| Wait | Pause execution for a specified duration (client-side delay) |
| Run Command | Execute an external command or script (client-side subprocess) |

## Task List File Format

Task lists are saved as JSON. Example:

```json
{
  "name": "Demo Sequence",
  "description": "Move two motors",
  "steps": [
    {
      "$type": "SetSpeed",
      "motorId": 0,
      "speedHz": 2000,
      "isEnabled": true
    },
    {
      "$type": "MoveTo",
      "motorId": 0,
      "position": 5000,
      "isEnabled": true
    },
    {
      "$type": "Wait",
      "durationMs": 500,
      "isEnabled": true
    },
    {
      "$type": "MoveTo",
      "motorId": 1,
      "position": 3000,
      "isEnabled": true
    }
  ]
}
```

## Project Structure

```
app/
├── StepperC3.slnx                  # Solution file
├── StepperC3.App/                  # WPF application
│   ├── MainWindow.xaml             # Main window layout
│   ├── MainWindow.xaml.cs          # Code-behind (drag-drop, shortcuts)
│   ├── ViewModels/
│   │   ├── MainViewModel.cs        # Application logic & state
│   │   ├── StepViewModel.cs        # Step wrapper with change notifications
│   │   ├── RelayCommand.cs         # ICommand implementation
│   │   └── ViewModelBase.cs        # INotifyPropertyChanged base
│   └── Converters/
│       └── BoolConverters.cs       # Bool→Visibility, InverseBool
├── StepperC3.Core/                 # Domain models & services
│   ├── Models/
│   │   ├── AutomationStep.cs       # Step base class + 14 derived types
│   │   ├── StepType.cs             # Step type enum
│   │   ├── TaskList.cs             # Ordered step collection
│   │   └── MotorStatus.cs          # Firmware status parser
│   └── Services/
│       ├── StepFactory.cs           # Step creation + metadata
│       ├── TaskRunner.cs            # Sequential executor
│       ├── TaskListSerializer.cs    # JSON persistence
│       └── MotorChainConnection.cs  # Serial port abstraction
└── StepperC3.Core.Tests/           # 76 xUnit tests
```
