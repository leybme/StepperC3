using System.Diagnostics;
using StepperC3.Core.Models;

namespace StepperC3.Core.Services;

/// <summary>
/// Event arguments for task execution progress reporting.
/// </summary>
public class StepExecutedEventArgs : EventArgs
{
    public int StepIndex { get; init; }
    public int TotalSteps { get; init; }
    public AutomationStep Step { get; init; } = null!;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Executes automation task lists step-by-step, sending commands to the motor chain
/// and handling client-side actions (wait, run command).
/// </summary>
public class TaskRunner
{
    private readonly IMotorConnection _connection;
    private CancellationTokenSource? _cts;

    /// <summary>Raised after each step is executed.</summary>
    public event EventHandler<StepExecutedEventArgs>? StepExecuted;

    /// <summary>Raised when a QueryStatus step receives a STATUS response.</summary>
    public event EventHandler<MotorStatus>? StatusReceived;

    /// <summary>Whether the runner is currently executing a task list.</summary>
    public bool IsRunning { get; private set; }

    public TaskRunner(IMotorConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Executes all enabled steps in the task list sequentially.
    /// </summary>
    public async Task RunAsync(TaskList taskList, CancellationToken ct = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("Task runner is already executing.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsRunning = true;

        try
        {
            var enabledSteps = taskList.Steps.Where(s => s.IsEnabled).ToList();

            for (var i = 0; i < enabledSteps.Count; i++)
            {
                _cts.Token.ThrowIfCancellationRequested();

                var step = enabledSteps[i];

                // ResetTask: restart the sequence from the beginning
                if (step is ResetTaskStep)
                {
                    StepExecuted?.Invoke(this, new StepExecutedEventArgs
                    {
                        StepIndex = i,
                        TotalSteps = enabledSteps.Count,
                        Step = step,
                        Success = true
                    });
                    i = -1; // will be incremented to 0 by the for-loop
                    continue;
                }

                var success = true;
                string? error = null;

                try
                {
                    await ExecuteStepAsync(step, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    success = false;
                    error = ex.Message;
                }

                StepExecuted?.Invoke(this, new StepExecutedEventArgs
                {
                    StepIndex = i,
                    TotalSteps = enabledSteps.Count,
                    Step = step,
                    Success = success,
                    ErrorMessage = error
                });

                if (!success)
                    throw new InvalidOperationException(
                        $"Step {i + 1} ({step.GetDescription()}) failed: {error}");
            }
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Cancels the currently running task list execution.
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
    }

    private async Task ExecuteStepAsync(AutomationStep step, CancellationToken ct)
    {
        switch (step)
        {
            case WaitStep waitStep:
                await Task.Delay(waitStep.DurationMs, ct);
                break;

            case WaitForIdleStep waitIdle:
                await WaitForMotorIdleAsync(waitIdle, ct);
                break;

            case QueryStatusStep queryStatus:
                await QueryMotorStatusAsync(queryStatus, ct);
                break;

            case RunCommandStep runCmd:
                await RunExternalCommandAsync(runCmd, ct);
                break;

            default:
                // Motor commands are sent via serial
                var command = step.ToCommand();
                if (!string.IsNullOrEmpty(command))
                {
                    await _connection.SendCommandAsync(command, ct);
                }
                break;
        }
    }

    private async Task QueryMotorStatusAsync(QueryStatusStep step, CancellationToken ct)
    {
        var id = step.MotorId ?? 0;
        await _connection.SendCommandAsync($"{id} STATUS", ct);
        var prefix = $"STATUS {id}";

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(4000);

        while (true)
        {
            var line = await _connection.ReadLineAsync(timeoutCts.Token);
            if (line is null) continue;
            var parsed = MotorStatus.TryParse(line.Trim());
            if (parsed?.MotorId == id)
            {
                StatusReceived?.Invoke(this, parsed);
                return;
            }
        }
    }

    private async Task WaitForMotorIdleAsync(WaitForIdleStep step, CancellationToken ct)
    {
        // Add extra margin so our token fires after the firmware timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(step.TimeoutMs + 5000);

        // Send the CHECKIDLE command once; the firmware handles polling internally
        await _connection.SendCommandAsync(step.ToCommand(), timeoutCts.Token);

        // Read lines until we receive IDLE <id> (success) or ERR CHECKIDLE timeout <id>
        var idlePrefix = $"IDLE {step.MotorId ?? 0}";
        var errPrefix  = $"ERR CHECKIDLE timeout {step.MotorId ?? 0}";

        while (true)
        {
            var line = await _connection.ReadLineAsync(timeoutCts.Token);
            if (line is null) continue;
            line = line.Trim();
            if (line.StartsWith(idlePrefix, StringComparison.OrdinalIgnoreCase))
                return;
            if (line.StartsWith(errPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Motor {step.MotorId} did not reach idle within {step.TimeoutMs} ms");
        }
    }

    private static async Task RunExternalCommandAsync(RunCommandStep step, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = step.Command,
            Arguments = step.Arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(step.TimeoutMs);

        await process.WaitForExitAsync(timeoutCts.Token);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException(
                $"Command '{step.Command}' exited with code {process.ExitCode}: {stderr}");
        }
    }
}
