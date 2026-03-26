using StepperC3.Core.Models;

namespace StepperC3.Core.Tests.Models;

public class AutomationStepTests
{
    [Fact]
    public void MoveToStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new MoveToStep { MotorId = 2, Position = 5000 };
        Assert.Equal("2 MOVETO 5000", step.ToCommand());
    }

    [Fact]
    public void MoveByStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new MoveByStep { MotorId = 0, Distance = -1000 };
        Assert.Equal("0 MOVE -1000", step.ToCommand());
    }

    [Fact]
    public void GoHomeStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new GoHomeStep { MotorId = 3 };
        Assert.Equal("3 GOHOME", step.ToCommand());
    }

    [Fact]
    public void FindHomeStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new FindHomeStep { MotorId = 1 };
        Assert.Equal("1 FINDHOME", step.ToCommand());
    }

    [Fact]
    public void WaitStep_ToCommand_ReturnsEmptyString()
    {
        var step = new WaitStep { DurationMs = 500 };
        Assert.Equal(string.Empty, step.ToCommand());
    }

    [Fact]
    public void RunCommandStep_ToCommand_ReturnsEmptyString()
    {
        var step = new RunCommandStep { Command = "echo", Arguments = "hello" };
        Assert.Equal(string.Empty, step.ToCommand());
    }

    [Fact]
    public void SetSpeedStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new SetSpeedStep { MotorId = 5, SpeedHz = 2000 };
        Assert.Equal("5 SETSPD 2000", step.ToCommand());
    }

    [Fact]
    public void SetAccelerationStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new SetAccelerationStep { MotorId = 1, AccelerationHz = 10000 };
        Assert.Equal("1 SETACCEL 10000", step.ToCommand());
    }

    [Fact]
    public void SetCurrentStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new SetCurrentStep { MotorId = 4, CurrentMA = 800 };
        Assert.Equal("4 SETCUR 800", step.ToCommand());
    }

    [Fact]
    public void SetMicrostepStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new SetMicrostepStep { MotorId = 0, Microsteps = 16 };
        Assert.Equal("0 SETSTEP 16", step.ToCommand());
    }

    [Fact]
    public void EnableStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new EnableStep { MotorId = 7 };
        Assert.Equal("7 ENABLE", step.ToCommand());
    }

    [Fact]
    public void DisableStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new DisableStep { MotorId = 6 };
        Assert.Equal("6 DISABLE", step.ToCommand());
    }

    [Fact]
    public void StopStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new StopStep { MotorId = 2 };
        Assert.Equal("2 STOP", step.ToCommand());
    }

    [Fact]
    public void FlipDirectionStep_ToCommand_GeneratesCorrectCommand()
    {
        var step = new FlipDirectionStep { MotorId = 3 };
        Assert.Equal("3 FLIPDIR", step.ToCommand());
    }

    [Fact]
    public void AllSteps_HaveCorrectType()
    {
        Assert.Equal(StepType.MoveTo, new MoveToStep().Type);
        Assert.Equal(StepType.MoveBy, new MoveByStep().Type);
        Assert.Equal(StepType.GoHome, new GoHomeStep().Type);
        Assert.Equal(StepType.FindHome, new FindHomeStep().Type);
        Assert.Equal(StepType.Wait, new WaitStep().Type);
        Assert.Equal(StepType.RunCommand, new RunCommandStep().Type);
        Assert.Equal(StepType.SetSpeed, new SetSpeedStep().Type);
        Assert.Equal(StepType.SetAcceleration, new SetAccelerationStep().Type);
        Assert.Equal(StepType.SetCurrent, new SetCurrentStep().Type);
        Assert.Equal(StepType.SetMicrostep, new SetMicrostepStep().Type);
        Assert.Equal(StepType.Enable, new EnableStep().Type);
        Assert.Equal(StepType.Disable, new DisableStep().Type);
        Assert.Equal(StepType.Stop, new StopStep().Type);
        Assert.Equal(StepType.FlipDirection, new FlipDirectionStep().Type);
    }

    [Fact]
    public void Steps_GetDescription_ReturnsNonEmptyString()
    {
        var steps = new AutomationStep[]
        {
            new MoveToStep { MotorId = 0, Position = 100 },
            new MoveByStep { MotorId = 0, Distance = 50 },
            new GoHomeStep { MotorId = 0 },
            new FindHomeStep { MotorId = 0 },
            new WaitStep { DurationMs = 1000 },
            new RunCommandStep { Command = "echo" },
            new SetSpeedStep { MotorId = 0, SpeedHz = 1000 },
            new SetAccelerationStep { MotorId = 0, AccelerationHz = 5000 },
            new SetCurrentStep { MotorId = 0, CurrentMA = 800 },
            new SetMicrostepStep { MotorId = 0, Microsteps = 16 },
            new EnableStep { MotorId = 0 },
            new DisableStep { MotorId = 0 },
            new StopStep { MotorId = 0 },
            new FlipDirectionStep { MotorId = 0 }
        };

        foreach (var step in steps)
        {
            Assert.False(string.IsNullOrEmpty(step.GetDescription()), $"{step.Type} has empty description");
        }
    }

    [Fact]
    public void NewStep_HasUniqueId()
    {
        var step1 = new MoveToStep();
        var step2 = new MoveToStep();
        Assert.NotEqual(step1.Id, step2.Id);
    }

    [Fact]
    public void NewStep_IsEnabledByDefault()
    {
        var step = new MoveToStep();
        Assert.True(step.IsEnabled);
    }
}
