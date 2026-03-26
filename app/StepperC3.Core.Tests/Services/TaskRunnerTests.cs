using StepperC3.Core.Models;
using StepperC3.Core.Services;

namespace StepperC3.Core.Tests.Services;

/// <summary>
/// Tests for TaskRunner using a mock connection.
/// </summary>
public class TaskRunnerTests
{
    /// <summary>
    /// Mock connection that records sent commands.
    /// </summary>
    private sealed class MockMotorConnection : IMotorConnection
    {
        public List<string> SentCommands { get; } = [];
        public bool IsConnected { get; private set; }

        public void Connect() => IsConnected = true;
        public void Disconnect() => IsConnected = false;

        public Task SendCommandAsync(string command, CancellationToken ct = default)
        {
            SentCommands.Add(command);
            return Task.CompletedTask;
        }

        public Task<string?> ReadLineAsync(CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public void Dispose() => Disconnect();
    }

    [Fact]
    public async Task RunAsync_ExecutesMotorCommands_InOrder()
    {
        var connection = new MockMotorConnection();
        connection.Connect();
        var runner = new TaskRunner(connection);

        var taskList = new TaskList { Name = "Test" };
        taskList.AddStep(new SetSpeedStep { MotorId = 0, SpeedHz = 2000 });
        taskList.AddStep(new MoveToStep { MotorId = 0, Position = 5000 });
        taskList.AddStep(new GoHomeStep { MotorId = 0 });

        await runner.RunAsync(taskList);

        Assert.Equal(3, connection.SentCommands.Count);
        Assert.Equal("0 SETSPD 2000", connection.SentCommands[0]);
        Assert.Equal("0 MOVETO 5000", connection.SentCommands[1]);
        Assert.Equal("0 GOHOME", connection.SentCommands[2]);
    }

    [Fact]
    public async Task RunAsync_SkipsDisabledSteps()
    {
        var connection = new MockMotorConnection();
        connection.Connect();
        var runner = new TaskRunner(connection);

        var taskList = new TaskList { Name = "Test" };
        taskList.AddStep(new MoveToStep { MotorId = 0, Position = 100 });
        taskList.AddStep(new MoveToStep { MotorId = 1, Position = 200, IsEnabled = false });
        taskList.AddStep(new MoveToStep { MotorId = 2, Position = 300 });

        await runner.RunAsync(taskList);

        Assert.Equal(2, connection.SentCommands.Count);
        Assert.Equal("0 MOVETO 100", connection.SentCommands[0]);
        Assert.Equal("2 MOVETO 300", connection.SentCommands[1]);
    }

    [Fact]
    public async Task RunAsync_RaisesStepExecutedEvent()
    {
        var connection = new MockMotorConnection();
        connection.Connect();
        var runner = new TaskRunner(connection);
        var events = new List<StepExecutedEventArgs>();
        runner.StepExecuted += (_, e) => events.Add(e);

        var taskList = new TaskList { Name = "Test" };
        taskList.AddStep(new MoveToStep { MotorId = 0, Position = 100 });
        taskList.AddStep(new GoHomeStep { MotorId = 0 });

        await runner.RunAsync(taskList);

        Assert.Equal(2, events.Count);
        Assert.Equal(0, events[0].StepIndex);
        Assert.Equal(2, events[0].TotalSteps);
        Assert.True(events[0].Success);
        Assert.Equal(1, events[1].StepIndex);
    }

    [Fact]
    public async Task RunAsync_Cancellation_ThrowsOperationCanceled()
    {
        var connection = new MockMotorConnection();
        connection.Connect();
        var runner = new TaskRunner(connection);

        var taskList = new TaskList { Name = "Test" };
        taskList.AddStep(new WaitStep { DurationMs = 60000 }); // Long wait

        using var cts = new CancellationTokenSource(100); // Cancel after 100ms
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(taskList, cts.Token));
    }

    [Fact]
    public async Task RunAsync_SetsIsRunningCorrectly()
    {
        var connection = new MockMotorConnection();
        connection.Connect();
        var runner = new TaskRunner(connection);

        Assert.False(runner.IsRunning);

        var taskList = new TaskList { Name = "Test" };
        taskList.AddStep(new MoveToStep { MotorId = 0, Position = 100 });

        await runner.RunAsync(taskList);

        Assert.False(runner.IsRunning); // Should be false after completion
    }

    [Fact]
    public async Task RunAsync_EmptyTaskList_CompletesSuccessfully()
    {
        var connection = new MockMotorConnection();
        connection.Connect();
        var runner = new TaskRunner(connection);

        var taskList = new TaskList { Name = "Empty" };
        await runner.RunAsync(taskList);

        Assert.Empty(connection.SentCommands);
    }

    [Fact]
    public async Task RunAsync_WaitStep_DoesNotSendSerialCommand()
    {
        var connection = new MockMotorConnection();
        connection.Connect();
        var runner = new TaskRunner(connection);

        var taskList = new TaskList { Name = "Test" };
        taskList.AddStep(new WaitStep { DurationMs = 10 });

        await runner.RunAsync(taskList);

        Assert.Empty(connection.SentCommands);
    }

    [Fact]
    public async Task RunAsync_MultipleMotors_SendsCorrectIds()
    {
        var connection = new MockMotorConnection();
        connection.Connect();
        var runner = new TaskRunner(connection);

        var taskList = new TaskList { Name = "Multi-Motor" };
        taskList.AddStep(new EnableStep { MotorId = 0 });
        taskList.AddStep(new EnableStep { MotorId = 1 });
        taskList.AddStep(new MoveToStep { MotorId = 0, Position = 1000 });
        taskList.AddStep(new MoveToStep { MotorId = 1, Position = 2000 });

        await runner.RunAsync(taskList);

        Assert.Equal(4, connection.SentCommands.Count);
        Assert.Equal("0 ENABLE", connection.SentCommands[0]);
        Assert.Equal("1 ENABLE", connection.SentCommands[1]);
        Assert.Equal("0 MOVETO 1000", connection.SentCommands[2]);
        Assert.Equal("1 MOVETO 2000", connection.SentCommands[3]);
    }
}
