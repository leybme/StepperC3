using StepperC3.Core.Models;
using StepperC3.Core.Services;

namespace StepperC3.Core.Tests.Services;

public class StepFactoryTests
{
    [Theory]
    [InlineData(StepType.MoveTo, typeof(MoveToStep))]
    [InlineData(StepType.MoveBy, typeof(MoveByStep))]
    [InlineData(StepType.GoHome, typeof(GoHomeStep))]
    [InlineData(StepType.FindHome, typeof(FindHomeStep))]
    [InlineData(StepType.Wait, typeof(WaitStep))]
    [InlineData(StepType.RunCommand, typeof(RunCommandStep))]
    [InlineData(StepType.SetSpeed, typeof(SetSpeedStep))]
    [InlineData(StepType.SetAcceleration, typeof(SetAccelerationStep))]
    [InlineData(StepType.SetCurrent, typeof(SetCurrentStep))]
    [InlineData(StepType.SetMicrostep, typeof(SetMicrostepStep))]
    [InlineData(StepType.Enable, typeof(EnableStep))]
    [InlineData(StepType.Disable, typeof(DisableStep))]
    [InlineData(StepType.Stop, typeof(StopStep))]
    [InlineData(StepType.FlipDirection, typeof(FlipDirectionStep))]
    public void Create_ReturnsCorrectType(StepType type, Type expectedType)
    {
        var step = StepFactory.Create(type, motorId: 3);
        Assert.IsType(expectedType, step);
    }

    [Theory]
    [InlineData(StepType.MoveTo)]
    [InlineData(StepType.MoveBy)]
    [InlineData(StepType.GoHome)]
    [InlineData(StepType.SetSpeed)]
    [InlineData(StepType.Enable)]
    public void Create_SetsMotorId(StepType type)
    {
        var step = StepFactory.Create(type, motorId: 5);
        Assert.Equal(5, step.MotorId);
    }

    [Fact]
    public void GetAvailableStepTypes_ReturnsAllTypes()
    {
        var types = StepFactory.GetAvailableStepTypes();
        Assert.Equal(18, types.Count);

        // All enum values should be represented
        var stepTypes = Enum.GetValues<StepType>();
        foreach (var st in stepTypes)
        {
            Assert.Contains(types, t => t.Type == st);
        }
    }

    [Fact]
    public void GetAvailableStepTypes_HasCategories()
    {
        var types = StepFactory.GetAvailableStepTypes();
        var categories = types.Select(t => t.Category).Distinct().ToList();

        Assert.Contains("Motion", categories);
        Assert.Contains("Configuration", categories);
        Assert.Contains("Flow Control", categories);
    }

    [Fact]
    public void GetAvailableStepTypes_AllHaveNonEmptyDescriptions()
    {
        var types = StepFactory.GetAvailableStepTypes();
        foreach (var info in types)
        {
            Assert.False(string.IsNullOrEmpty(info.DisplayName), $"Type {info.Type} has empty display name");
            Assert.False(string.IsNullOrEmpty(info.Description), $"Type {info.Type} has empty description");
        }
    }
}
