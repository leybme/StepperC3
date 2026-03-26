using StepperC3.Core.Models;

namespace StepperC3.Core.Services;

/// <summary>
/// Factory for creating automation steps by type.
/// Useful for UI toolbox / drag-drop palette.
/// </summary>
public static class StepFactory
{
    /// <summary>
    /// Creates a new automation step of the specified type with default values.
    /// </summary>
    public static AutomationStep Create(StepType type, int motorId = 0)
    {
        return type switch
        {
            StepType.MoveTo => new MoveToStep { MotorId = motorId, Position = 0 },
            StepType.MoveBy => new MoveByStep { MotorId = motorId, Distance = 1000 },
            StepType.GoHome => new GoHomeStep { MotorId = motorId },
            StepType.FindHome => new FindHomeStep { MotorId = motorId },
            StepType.Wait => new WaitStep { DurationMs = 1000 },
            StepType.RunCommand => new RunCommandStep(),
            StepType.SetSpeed => new SetSpeedStep { MotorId = motorId, SpeedHz = 1000 },
            StepType.SetAcceleration => new SetAccelerationStep { MotorId = motorId, AccelerationHz = 5000 },
            StepType.SetCurrent => new SetCurrentStep { MotorId = motorId, CurrentMA = 800 },
            StepType.SetMicrostep => new SetMicrostepStep { MotorId = motorId, Microsteps = 16 },
            StepType.Enable => new EnableStep { MotorId = motorId },
            StepType.Disable => new DisableStep { MotorId = motorId },
            StepType.Stop => new StopStep { MotorId = motorId },
            StepType.FlipDirection => new FlipDirectionStep { MotorId = motorId },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown step type.")
        };
    }

    /// <summary>
    /// Returns metadata about all available step types for building a UI toolbox.
    /// </summary>
    public static IReadOnlyList<StepTypeInfo> GetAvailableStepTypes() =>
    [
        new(StepType.MoveTo,          "Move To",          "Move motor to absolute position",        "Motion"),
        new(StepType.MoveBy,          "Move By",          "Move motor by relative distance",        "Motion"),
        new(StepType.GoHome,          "Go Home",          "Return motor to position 0",             "Motion"),
        new(StepType.FindHome,        "Find Home",        "Auto-homing via end-switch",             "Motion"),
        new(StepType.Stop,            "Stop",             "Stop motor immediately",                 "Motion"),
        new(StepType.SetSpeed,        "Set Speed",        "Set motor speed (steps/sec)",            "Configuration"),
        new(StepType.SetAcceleration, "Set Acceleration", "Set motor acceleration (steps/s²)",      "Configuration"),
        new(StepType.SetCurrent,      "Set Current",      "Set motor RMS current (mA)",             "Configuration"),
        new(StepType.SetMicrostep,    "Set Microstep",    "Set microstep resolution",               "Configuration"),
        new(StepType.Enable,          "Enable",           "Enable motor driver",                    "Configuration"),
        new(StepType.Disable,         "Disable",          "Disable motor driver",                   "Configuration"),
        new(StepType.FlipDirection,   "Flip Direction",   "Toggle direction polarity",              "Configuration"),
        new(StepType.Wait,            "Wait",             "Pause for specified duration",            "Flow Control"),
        new(StepType.RunCommand,      "Run Command",      "Execute an external command or script",  "Flow Control")
    ];
}

/// <summary>
/// Metadata about a step type for UI display (toolbox palette).
/// </summary>
public record StepTypeInfo(StepType Type, string DisplayName, string Description, string Category);
