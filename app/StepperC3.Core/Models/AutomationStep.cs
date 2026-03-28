using System.Text.Json.Serialization;

namespace StepperC3.Core.Models;

/// <summary>
/// Base class for all automation steps in a task list.
/// Each step represents a single action in a motor automation sequence.
/// </summary>
[JsonDerivedType(typeof(MoveToStep), "MoveTo")]
[JsonDerivedType(typeof(MoveByStep), "MoveBy")]
[JsonDerivedType(typeof(GoHomeStep), "GoHome")]
[JsonDerivedType(typeof(FindHomeStep), "FindHome")]
[JsonDerivedType(typeof(WaitStep), "Wait")]
[JsonDerivedType(typeof(RunCommandStep), "RunCommand")]
[JsonDerivedType(typeof(SetSpeedStep), "SetSpeed")]
[JsonDerivedType(typeof(SetAccelerationStep), "SetAcceleration")]
[JsonDerivedType(typeof(SetCurrentStep), "SetCurrent")]
[JsonDerivedType(typeof(SetMicrostepStep), "SetMicrostep")]
[JsonDerivedType(typeof(EnableStep), "Enable")]
[JsonDerivedType(typeof(DisableStep), "Disable")]
[JsonDerivedType(typeof(StopStep), "Stop")]
[JsonDerivedType(typeof(FlipDirectionStep), "FlipDirection")]
[JsonDerivedType(typeof(SetPositionStep), "SetPosition")]
[JsonDerivedType(typeof(WaitForIdleStep), "WaitForIdle")]
[JsonDerivedType(typeof(ResetTaskStep), "ResetTask")]
[JsonDerivedType(typeof(QueryStatusStep), "QueryStatus")]
public abstract class AutomationStep
{
    /// <summary>Unique identifier for this step.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The type of this step.</summary>
    public abstract StepType Type { get; }

    /// <summary>Optional display name for this step.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Board ID (0-7) in the daisy chain. Null targets all boards.</summary>
    public int? MotorId { get; set; }

    /// <summary>Whether this step is enabled for execution.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Generates the serial command string for this step matching the firmware protocol.
    /// </summary>
    public abstract string ToCommand();

    /// <summary>
    /// Returns a human-readable description of this step.
    /// </summary>
    public abstract string GetDescription();
}

/// <summary>Move motor to an absolute position.</summary>
public class MoveToStep : AutomationStep
{
    public override StepType Type => StepType.MoveTo;
    public long Position { get; set; }

    public override string ToCommand() => $"{MotorId} MOVETO {Position}";
    public override string GetDescription() => $"Move Motor {MotorId} to position {Position}";
}

/// <summary>Move motor by a relative distance.</summary>
public class MoveByStep : AutomationStep
{
    public override StepType Type => StepType.MoveBy;
    public long Distance { get; set; }

    public override string ToCommand() => $"{MotorId} MOVE {Distance}";
    public override string GetDescription() => $"Move Motor {MotorId} by {Distance} steps";
}

/// <summary>Return motor to position 0.</summary>
public class GoHomeStep : AutomationStep
{
    public override StepType Type => StepType.GoHome;

    public override string ToCommand() => $"{MotorId} GOHOME";
    public override string GetDescription() => $"Motor {MotorId} go to home (position 0)";
}

/// <summary>Auto-homing using end-switch detection.</summary>
public class FindHomeStep : AutomationStep
{
    public override StepType Type => StepType.FindHome;

    public override string ToCommand() => $"{MotorId} FINDHOME";
    public override string GetDescription() => $"Motor {MotorId} find home (end-switch)";
}

/// <summary>Wait for a specified duration.</summary>
public class WaitStep : AutomationStep
{
    public override StepType Type => StepType.Wait;
    public int DurationMs { get; set; }

    public override string ToCommand() => string.Empty; // Client-side delay, no serial command
    public override string GetDescription() => $"Wait {DurationMs} ms";
}

/// <summary>Run an external command or script.</summary>
public class RunCommandStep : AutomationStep
{
    public override StepType Type => StepType.RunCommand;
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 30000;

    public override string ToCommand() => string.Empty; // Executed on the host PC, not via serial
    public override string GetDescription() => $"Run: {Command} {Arguments}";
}

/// <summary>Set motor speed in steps per second.</summary>
public class SetSpeedStep : AutomationStep
{
    public override StepType Type => StepType.SetSpeed;
    public uint SpeedHz { get; set; }

    public override string ToCommand() => $"{MotorId} SETSPD {SpeedHz}";
    public override string GetDescription() => $"Set Motor {MotorId} speed to {SpeedHz} Hz";
}

/// <summary>Set motor acceleration in steps/s².</summary>
public class SetAccelerationStep : AutomationStep
{
    public override StepType Type => StepType.SetAcceleration;
    public uint AccelerationHz { get; set; }

    public override string ToCommand() => $"{MotorId} SETACCEL {AccelerationHz}";
    public override string GetDescription() => $"Set Motor {MotorId} acceleration to {AccelerationHz}";
}

/// <summary>Set motor RMS current in milliamps.</summary>
public class SetCurrentStep : AutomationStep
{
    public override StepType Type => StepType.SetCurrent;
    public uint CurrentMA { get; set; }

    public override string ToCommand() => $"{MotorId} SETCUR {CurrentMA}";
    public override string GetDescription() => $"Set Motor {MotorId} current to {CurrentMA} mA";
}

/// <summary>Set motor microstep resolution.</summary>
public class SetMicrostepStep : AutomationStep
{
    public override StepType Type => StepType.SetMicrostep;
    public ushort Microsteps { get; set; } = 16;

    public override string ToCommand() => $"{MotorId} SETSTEP {Microsteps}";
    public override string GetDescription() => $"Set Motor {MotorId} microsteps to 1/{Microsteps}";
}

/// <summary>Enable motor driver.</summary>
public class EnableStep : AutomationStep
{
    public override StepType Type => StepType.Enable;

    public override string ToCommand() => $"{MotorId} ENABLE";
    public override string GetDescription() => $"Enable Motor {MotorId}";
}

/// <summary>Disable motor driver.</summary>
public class DisableStep : AutomationStep
{
    public override StepType Type => StepType.Disable;

    public override string ToCommand() => $"{MotorId} DISABLE";
    public override string GetDescription() => $"Disable Motor {MotorId}";
}

/// <summary>Stop motor immediately.</summary>
public class StopStep : AutomationStep
{
    public override StepType Type => StepType.Stop;

    public override string ToCommand() => $"{MotorId} STOP";
    public override string GetDescription() => $"Stop Motor {MotorId}";
}

/// <summary>Flip motor direction polarity.</summary>
public class FlipDirectionStep : AutomationStep
{
    public override StepType Type => StepType.FlipDirection;

    public override string ToCommand() => $"{MotorId} FLIPDIR";
    public override string GetDescription() => $"Flip direction of Motor {MotorId}";
}

/// <summary>Redefine current position without motion (e.g. after manual alignment).</summary>
public class SetPositionStep : AutomationStep
{
    public override StepType Type => StepType.SetPosition;
    public long Position { get; set; }

    public override string ToCommand() => $"{MotorId} SETPOS {Position}";
    public override string GetDescription() => $"Set Motor {MotorId} position to {Position}";
}

/// <summary>Restart the task list from the first step.</summary>
public class ResetTaskStep : AutomationStep
{
    public override StepType Type => StepType.ResetTask;

    public override string ToCommand() => string.Empty; // Handled by TaskRunner, not sent via serial
    public override string GetDescription() => "Reset task to beginning";
}

/// <summary>Wait until motor reports IDLE state by polling STATUS.</summary>
public class WaitForIdleStep : AutomationStep
{
    public override StepType Type => StepType.WaitForIdle;
    public int TimeoutMs { get; set; } = 30000;

    public override string ToCommand() => $"{MotorId ?? 0} CHECKIDLE {TimeoutMs}";
    public override string GetDescription() => $"Wait Motor {MotorId} idle (timeout {TimeoutMs} ms)";
}

/// <summary>Query motor STATUS and refresh the live status panel.</summary>
public class QueryStatusStep : AutomationStep
{
    public override StepType Type => StepType.QueryStatus;

    public override string ToCommand() => $"{MotorId ?? 0} STATUS";
    public override string GetDescription() => $"Query status of Motor {MotorId ?? 0}";
}
