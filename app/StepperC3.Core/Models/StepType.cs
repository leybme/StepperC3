namespace StepperC3.Core.Models;

/// <summary>
/// Defines the types of automation steps available in a task list.
/// </summary>
public enum StepType
{
    /// <summary>Move motor to an absolute position (steps).</summary>
    MoveTo,

    /// <summary>Move motor by a relative distance (steps).</summary>
    MoveBy,

    /// <summary>Return motor to position 0.</summary>
    GoHome,

    /// <summary>Auto-homing using end-switch detection.</summary>
    FindHome,

    /// <summary>Wait for a specified duration.</summary>
    Wait,

    /// <summary>Run an external command or script.</summary>
    RunCommand,

    /// <summary>Set motor speed (steps/sec).</summary>
    SetSpeed,

    /// <summary>Set motor acceleration (steps/s²).</summary>
    SetAcceleration,

    /// <summary>Set motor RMS current (mA).</summary>
    SetCurrent,

    /// <summary>Set motor microstep resolution.</summary>
    SetMicrostep,

    /// <summary>Enable motor driver.</summary>
    Enable,

    /// <summary>Disable motor driver.</summary>
    Disable,

    /// <summary>Stop motor immediately.</summary>
    Stop,

    /// <summary>Flip motor direction polarity.</summary>
    FlipDirection,

    /// <summary>Redefine current position without motion (offset calibration).</summary>
    SetPosition,

    /// <summary>Wait until motor reports idle state (polls STATUS).</summary>
    WaitForIdle,

    /// <summary>Restart the task list from the first step.</summary>
    ResetTask
}
