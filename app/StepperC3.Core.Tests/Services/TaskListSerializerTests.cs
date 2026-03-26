using StepperC3.Core.Models;
using StepperC3.Core.Services;

namespace StepperC3.Core.Tests.Services;

public class TaskListSerializerTests
{
    [Fact]
    public void SerializeDeserialize_RoundTrips_TaskList()
    {
        var taskList = new TaskList
        {
            Name = "Test Sequence",
            Description = "A test automation sequence"
        };
        taskList.AddStep(new MoveToStep { MotorId = 0, Position = 5000, Name = "Move motor 0" });
        taskList.AddStep(new WaitStep { DurationMs = 1000, Name = "Wait 1s" });
        taskList.AddStep(new MoveByStep { MotorId = 1, Distance = -2000 });
        taskList.AddStep(new GoHomeStep { MotorId = 0 });
        taskList.AddStep(new FindHomeStep { MotorId = 2 });
        taskList.AddStep(new SetSpeedStep { MotorId = 0, SpeedHz = 3000 });
        taskList.AddStep(new RunCommandStep { Command = "echo", Arguments = "done" });

        var json = TaskListSerializer.Serialize(taskList);
        var deserialized = TaskListSerializer.Deserialize(json);

        Assert.Equal(taskList.Name, deserialized.Name);
        Assert.Equal(taskList.Description, deserialized.Description);
        Assert.Equal(taskList.Steps.Count, deserialized.Steps.Count);

        // Verify step types are preserved
        Assert.IsType<MoveToStep>(deserialized.Steps[0]);
        Assert.IsType<WaitStep>(deserialized.Steps[1]);
        Assert.IsType<MoveByStep>(deserialized.Steps[2]);
        Assert.IsType<GoHomeStep>(deserialized.Steps[3]);
        Assert.IsType<FindHomeStep>(deserialized.Steps[4]);
        Assert.IsType<SetSpeedStep>(deserialized.Steps[5]);
        Assert.IsType<RunCommandStep>(deserialized.Steps[6]);

        // Verify specific values
        var moveToStep = (MoveToStep)deserialized.Steps[0];
        Assert.Equal(0, moveToStep.MotorId);
        Assert.Equal(5000, moveToStep.Position);
        Assert.Equal("Move motor 0", moveToStep.Name);

        var waitStep = (WaitStep)deserialized.Steps[1];
        Assert.Equal(1000, waitStep.DurationMs);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips_ViaFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"stepper_test_{Guid.NewGuid()}.json");
        try
        {
            var taskList = new TaskList { Name = "File Test" };
            taskList.AddStep(new MoveToStep { MotorId = 0, Position = 100 });
            taskList.AddStep(new WaitStep { DurationMs = 500 });

            await TaskListSerializer.SaveAsync(taskList, filePath);
            Assert.True(File.Exists(filePath));

            var loaded = await TaskListSerializer.LoadAsync(filePath);
            Assert.Equal("File Test", loaded.Name);
            Assert.Equal(2, loaded.Steps.Count);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void Serialize_AllStepTypes_ProducesValidJson()
    {
        var taskList = new TaskList { Name = "All Steps" };
        taskList.AddStep(new MoveToStep { MotorId = 0, Position = 100 });
        taskList.AddStep(new MoveByStep { MotorId = 0, Distance = 50 });
        taskList.AddStep(new GoHomeStep { MotorId = 0 });
        taskList.AddStep(new FindHomeStep { MotorId = 0 });
        taskList.AddStep(new WaitStep { DurationMs = 1000 });
        taskList.AddStep(new RunCommandStep { Command = "test", Arguments = "-v" });
        taskList.AddStep(new SetSpeedStep { MotorId = 0, SpeedHz = 1000 });
        taskList.AddStep(new SetAccelerationStep { MotorId = 0, AccelerationHz = 5000 });
        taskList.AddStep(new SetCurrentStep { MotorId = 0, CurrentMA = 800 });
        taskList.AddStep(new SetMicrostepStep { MotorId = 0, Microsteps = 16 });
        taskList.AddStep(new EnableStep { MotorId = 0 });
        taskList.AddStep(new DisableStep { MotorId = 0 });
        taskList.AddStep(new StopStep { MotorId = 0 });
        taskList.AddStep(new FlipDirectionStep { MotorId = 0 });

        var json = TaskListSerializer.Serialize(taskList);
        var deserialized = TaskListSerializer.Deserialize(json);

        Assert.Equal(14, deserialized.Steps.Count);

        // Verify all types round-trip
        Assert.IsType<MoveToStep>(deserialized.Steps[0]);
        Assert.IsType<MoveByStep>(deserialized.Steps[1]);
        Assert.IsType<GoHomeStep>(deserialized.Steps[2]);
        Assert.IsType<FindHomeStep>(deserialized.Steps[3]);
        Assert.IsType<WaitStep>(deserialized.Steps[4]);
        Assert.IsType<RunCommandStep>(deserialized.Steps[5]);
        Assert.IsType<SetSpeedStep>(deserialized.Steps[6]);
        Assert.IsType<SetAccelerationStep>(deserialized.Steps[7]);
        Assert.IsType<SetCurrentStep>(deserialized.Steps[8]);
        Assert.IsType<SetMicrostepStep>(deserialized.Steps[9]);
        Assert.IsType<EnableStep>(deserialized.Steps[10]);
        Assert.IsType<DisableStep>(deserialized.Steps[11]);
        Assert.IsType<StopStep>(deserialized.Steps[12]);
        Assert.IsType<FlipDirectionStep>(deserialized.Steps[13]);
    }

    [Fact]
    public void Deserialize_EmptyTaskList_Works()
    {
        var taskList = new TaskList { Name = "Empty" };
        var json = TaskListSerializer.Serialize(taskList);
        var deserialized = TaskListSerializer.Deserialize(json);

        Assert.Equal("Empty", deserialized.Name);
        Assert.Empty(deserialized.Steps);
    }
}
