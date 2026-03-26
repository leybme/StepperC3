using StepperC3.Core.Models;

namespace StepperC3.Core.Tests.Models;

public class TaskListTests
{
    [Fact]
    public void NewTaskList_HasEmptySteps()
    {
        var taskList = new TaskList();
        Assert.Empty(taskList.Steps);
    }

    [Fact]
    public void AddStep_IncreasesCount()
    {
        var taskList = new TaskList();
        taskList.AddStep(new MoveToStep { MotorId = 0, Position = 100 });
        Assert.Single(taskList.Steps);
    }

    [Fact]
    public void InsertStep_AtValidIndex_InsertsCorrectly()
    {
        var taskList = new TaskList();
        var step0 = new MoveToStep { MotorId = 0, Position = 0 };
        var step1 = new MoveToStep { MotorId = 1, Position = 100 };
        var stepInserted = new WaitStep { DurationMs = 500 };

        taskList.AddStep(step0);
        taskList.AddStep(step1);
        taskList.InsertStep(1, stepInserted);

        Assert.Equal(3, taskList.Steps.Count);
        Assert.Same(stepInserted, taskList.Steps[1]);
        Assert.Same(step1, taskList.Steps[2]);
    }

    [Fact]
    public void InsertStep_AtInvalidIndex_ThrowsArgumentOutOfRange()
    {
        var taskList = new TaskList();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            taskList.InsertStep(-1, new WaitStep()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            taskList.InsertStep(1, new WaitStep()));
    }

    [Fact]
    public void RemoveStep_ById_RemovesCorrectStep()
    {
        var taskList = new TaskList();
        var step = new MoveToStep { MotorId = 0, Position = 100 };
        taskList.AddStep(step);

        Assert.True(taskList.RemoveStep(step.Id));
        Assert.Empty(taskList.Steps);
    }

    [Fact]
    public void RemoveStep_WithNonExistentId_ReturnsFalse()
    {
        var taskList = new TaskList();
        Assert.False(taskList.RemoveStep(Guid.NewGuid()));
    }

    [Fact]
    public void RemoveStepAt_ValidIndex_RemovesStep()
    {
        var taskList = new TaskList();
        taskList.AddStep(new MoveToStep { MotorId = 0 });
        taskList.AddStep(new MoveByStep { MotorId = 1, Distance = 100 });

        taskList.RemoveStepAt(0);

        Assert.Single(taskList.Steps);
        Assert.IsType<MoveByStep>(taskList.Steps[0]);
    }

    [Fact]
    public void RemoveStepAt_InvalidIndex_ThrowsArgumentOutOfRange()
    {
        var taskList = new TaskList();
        Assert.Throws<ArgumentOutOfRangeException>(() => taskList.RemoveStepAt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => taskList.RemoveStepAt(-1));
    }

    [Fact]
    public void MoveStep_ReordersCorrectly()
    {
        var taskList = new TaskList();
        var step0 = new MoveToStep { MotorId = 0 };
        var step1 = new WaitStep { DurationMs = 100 };
        var step2 = new GoHomeStep { MotorId = 0 };

        taskList.AddStep(step0);
        taskList.AddStep(step1);
        taskList.AddStep(step2);

        // Move step0 to position 2
        taskList.MoveStep(0, 2);

        Assert.Same(step1, taskList.Steps[0]);
        Assert.Same(step2, taskList.Steps[1]);
        Assert.Same(step0, taskList.Steps[2]);
    }

    [Fact]
    public void MoveStep_SameIndex_NoChange()
    {
        var taskList = new TaskList();
        var step = new MoveToStep { MotorId = 0 };
        taskList.AddStep(step);

        taskList.MoveStep(0, 0);
        Assert.Same(step, taskList.Steps[0]);
    }

    [Fact]
    public void MoveStep_InvalidIndex_ThrowsArgumentOutOfRange()
    {
        var taskList = new TaskList();
        taskList.AddStep(new MoveToStep());
        Assert.Throws<ArgumentOutOfRangeException>(() => taskList.MoveStep(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => taskList.MoveStep(0, 1));
    }

    [Fact]
    public void ReplaceStep_ReplacesCorrectly()
    {
        var taskList = new TaskList();
        var original = new MoveToStep { MotorId = 0, Position = 100 };
        var replacement = new MoveByStep { MotorId = 1, Distance = 200 };

        taskList.AddStep(original);
        taskList.ReplaceStep(0, replacement);

        Assert.Single(taskList.Steps);
        Assert.Same(replacement, taskList.Steps[0]);
    }

    [Fact]
    public void ReplaceStep_InvalidIndex_ThrowsArgumentOutOfRange()
    {
        var taskList = new TaskList();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            taskList.ReplaceStep(0, new MoveToStep()));
    }

    [Fact]
    public void DuplicateStep_CreatesNewCopyAtNextIndex()
    {
        var taskList = new TaskList();
        var step = new MoveToStep { MotorId = 2, Position = 500, Name = "Step A" };
        taskList.AddStep(step);

        taskList.DuplicateStep(0);

        Assert.Equal(2, taskList.Steps.Count);
        var copy = taskList.Steps[1] as MoveToStep;
        Assert.NotNull(copy);
        Assert.NotEqual(step.Id, copy.Id);
        Assert.Equal(step.Position, copy.Position);
        Assert.Equal(step.MotorId, copy.MotorId);
    }

    [Fact]
    public void DuplicateStep_InvalidIndex_ThrowsArgumentOutOfRange()
    {
        var taskList = new TaskList();
        Assert.Throws<ArgumentOutOfRangeException>(() => taskList.DuplicateStep(0));
    }

    [Fact]
    public void Clear_RemovesAllSteps()
    {
        var taskList = new TaskList();
        taskList.AddStep(new MoveToStep());
        taskList.AddStep(new WaitStep());
        taskList.Clear();
        Assert.Empty(taskList.Steps);
    }

    [Fact]
    public void EnabledStepCount_CountsOnlyEnabledSteps()
    {
        var taskList = new TaskList();
        taskList.AddStep(new MoveToStep { IsEnabled = true });
        taskList.AddStep(new WaitStep { IsEnabled = false });
        taskList.AddStep(new GoHomeStep { IsEnabled = true });

        Assert.Equal(2, taskList.EnabledStepCount);
    }

    [Fact]
    public void ModifiedAt_UpdatesOnChanges()
    {
        var taskList = new TaskList();
        var initial = taskList.ModifiedAt;

        // Small delay to ensure different timestamp
        Thread.Sleep(10);
        taskList.AddStep(new MoveToStep());

        Assert.True(taskList.ModifiedAt >= initial);
    }
}
