using StepperC3.Core.Models;
using StepperC3.Core.Services;

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║    StepperC3 Motor Chain Controller      ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();

var running = true;
TaskList? currentTask = null;

while (running)
{
    Console.WriteLine("─── Main Menu ───");
    Console.WriteLine("  1. New task list");
    Console.WriteLine("  2. Load task list from file");
    Console.WriteLine("  3. Edit current task list");
    Console.WriteLine("  4. Save task list to file");
    Console.WriteLine("  5. Run task list (serial)");
    Console.WriteLine("  6. Show available step types");
    Console.WriteLine("  7. List serial ports");
    Console.WriteLine("  0. Exit");
    Console.Write("> ");
    var choice = Console.ReadLine()?.Trim();

    switch (choice)
    {
        case "1":
            currentTask = CreateNewTaskList();
            break;
        case "2":
            currentTask = await LoadTaskListAsync();
            break;
        case "3":
            if (currentTask is null)
                Console.WriteLine("No task list loaded. Create or load one first.");
            else
                EditTaskList(currentTask);
            break;
        case "4":
            if (currentTask is null)
                Console.WriteLine("No task list to save.");
            else
                await SaveTaskListAsync(currentTask);
            break;
        case "5":
            if (currentTask is null)
                Console.WriteLine("No task list to run.");
            else
                await RunTaskListAsync(currentTask);
            break;
        case "6":
            ShowStepTypes();
            break;
        case "7":
            ListSerialPorts();
            break;
        case "0":
            running = false;
            break;
        default:
            Console.WriteLine("Invalid choice.");
            break;
    }

    Console.WriteLine();
}

Console.WriteLine("Goodbye.");
return;

// ─── Helper Methods ─────────────────────────────────────────────────────────

static TaskList CreateNewTaskList()
{
    Console.Write("Task list name: ");
    var name = Console.ReadLine()?.Trim() ?? "Untitled";
    Console.Write("Description (optional): ");
    var desc = Console.ReadLine()?.Trim() ?? "";

    var task = new TaskList { Name = name, Description = desc };
    Console.WriteLine($"Created task list: {task.Name}");
    return task;
}

static async Task<TaskList?> LoadTaskListAsync()
{
    Console.Write("File path: ");
    var path = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(path) || !File.Exists(path))
    {
        Console.WriteLine("File not found.");
        return null;
    }
    var task = await TaskListSerializer.LoadAsync(path);
    Console.WriteLine($"Loaded: {task.Name} ({task.Steps.Count} steps)");
    return task;
}

static async Task SaveTaskListAsync(TaskList taskList)
{
    Console.Write("File path: ");
    var path = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(path))
    {
        Console.WriteLine("No path provided.");
        return;
    }
    await TaskListSerializer.SaveAsync(taskList, path);
    Console.WriteLine($"Saved to: {path}");
}

static void EditTaskList(TaskList taskList)
{
    var editing = true;
    while (editing)
    {
        Console.WriteLine();
        Console.WriteLine($"─── Editing: {taskList.Name} ({taskList.Steps.Count} steps) ───");
        PrintSteps(taskList);
        Console.WriteLine();
        Console.WriteLine("  a. Add step");
        Console.WriteLine("  r. Remove step");
        Console.WriteLine("  m. Move step (reorder)");
        Console.WriteLine("  d. Duplicate step");
        Console.WriteLine("  t. Toggle step enabled/disabled");
        Console.WriteLine("  c. Clear all steps");
        Console.WriteLine("  b. Back to main menu");
        Console.Write("> ");

        switch (Console.ReadLine()?.Trim().ToLowerInvariant())
        {
            case "a":
                AddStepInteractive(taskList);
                break;
            case "r":
                RemoveStepInteractive(taskList);
                break;
            case "m":
                MoveStepInteractive(taskList);
                break;
            case "d":
                DuplicateStepInteractive(taskList);
                break;
            case "t":
                ToggleStepInteractive(taskList);
                break;
            case "c":
                taskList.Clear();
                Console.WriteLine("All steps cleared.");
                break;
            case "b":
                editing = false;
                break;
            default:
                Console.WriteLine("Invalid choice.");
                break;
        }
    }
}

static void PrintSteps(TaskList taskList)
{
    if (taskList.Steps.Count == 0)
    {
        Console.WriteLine("  (no steps)");
        return;
    }

    for (var i = 0; i < taskList.Steps.Count; i++)
    {
        var step = taskList.Steps[i];
        var status = step.IsEnabled ? "✓" : "✗";
        Console.WriteLine($"  [{i}] [{status}] {step.GetDescription()}");
    }
}

static void AddStepInteractive(TaskList taskList)
{
    var types = StepFactory.GetAvailableStepTypes();
    Console.WriteLine("Available step types:");
    for (var i = 0; i < types.Count; i++)
        Console.WriteLine($"  {i,2}. [{types[i].Category}] {types[i].DisplayName} - {types[i].Description}");

    Console.Write("Step type #: ");
    if (!int.TryParse(Console.ReadLine()?.Trim(), out var idx) || idx < 0 || idx >= types.Count)
    {
        Console.WriteLine("Invalid selection.");
        return;
    }

    var stepType = types[idx].Type;
    Console.Write("Motor ID (0-7, or empty for N/A): ");
    var motorIdStr = Console.ReadLine()?.Trim();
    var motorId = int.TryParse(motorIdStr, out var mid) ? mid : 0;

    var step = StepFactory.Create(stepType, motorId);

    // Prompt for step-specific parameters
    switch (step)
    {
        case MoveToStep moveTo:
            Console.Write("Target position (steps): ");
            if (long.TryParse(Console.ReadLine()?.Trim(), out var pos))
                moveTo.Position = pos;
            break;
        case MoveByStep moveBy:
            Console.Write("Distance (steps, +/-): ");
            if (long.TryParse(Console.ReadLine()?.Trim(), out var dist))
                moveBy.Distance = dist;
            break;
        case WaitStep wait:
            Console.Write("Duration (ms): ");
            if (int.TryParse(Console.ReadLine()?.Trim(), out var dur))
                wait.DurationMs = dur;
            break;
        case RunCommandStep runCmd:
            Console.Write("Command: ");
            runCmd.Command = Console.ReadLine()?.Trim() ?? "";
            Console.Write("Arguments: ");
            runCmd.Arguments = Console.ReadLine()?.Trim() ?? "";
            Console.Write("Timeout (ms, default 30000): ");
            if (int.TryParse(Console.ReadLine()?.Trim(), out var timeout))
                runCmd.TimeoutMs = timeout;
            break;
        case SetSpeedStep speed:
            Console.Write("Speed (Hz): ");
            if (uint.TryParse(Console.ReadLine()?.Trim(), out var hz))
                speed.SpeedHz = hz;
            break;
        case SetAccelerationStep accel:
            Console.Write("Acceleration (steps/s²): ");
            if (uint.TryParse(Console.ReadLine()?.Trim(), out var acc))
                accel.AccelerationHz = acc;
            break;
        case SetCurrentStep current:
            Console.Write("Current (mA): ");
            if (uint.TryParse(Console.ReadLine()?.Trim(), out var ma))
                current.CurrentMA = ma;
            break;
        case SetMicrostepStep microstep:
            Console.Write("Microsteps (1/2/4/8/16/32/64/256): ");
            if (ushort.TryParse(Console.ReadLine()?.Trim(), out var ms))
                microstep.Microsteps = ms;
            break;
    }

    taskList.AddStep(step);
    Console.WriteLine($"Added: {step.GetDescription()}");
}

static void RemoveStepInteractive(TaskList taskList)
{
    if (taskList.Steps.Count == 0) { Console.WriteLine("No steps to remove."); return; }
    Console.Write("Step index to remove: ");
    if (int.TryParse(Console.ReadLine()?.Trim(), out var idx) && idx >= 0 && idx < taskList.Steps.Count)
    {
        var desc = taskList.Steps[idx].GetDescription();
        taskList.RemoveStepAt(idx);
        Console.WriteLine($"Removed: {desc}");
    }
    else Console.WriteLine("Invalid index.");
}

static void MoveStepInteractive(TaskList taskList)
{
    if (taskList.Steps.Count < 2) { Console.WriteLine("Need at least 2 steps to reorder."); return; }
    Console.Write("Move from index: ");
    if (!int.TryParse(Console.ReadLine()?.Trim(), out var from)) { Console.WriteLine("Invalid."); return; }
    Console.Write("Move to index: ");
    if (!int.TryParse(Console.ReadLine()?.Trim(), out var to)) { Console.WriteLine("Invalid."); return; }
    try
    {
        taskList.MoveStep(from, to);
        Console.WriteLine($"Moved step {from} → {to}");
    }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("Index out of range."); }
}

static void DuplicateStepInteractive(TaskList taskList)
{
    if (taskList.Steps.Count == 0) { Console.WriteLine("No steps to duplicate."); return; }
    Console.Write("Step index to duplicate: ");
    if (int.TryParse(Console.ReadLine()?.Trim(), out var idx) && idx >= 0 && idx < taskList.Steps.Count)
    {
        taskList.DuplicateStep(idx);
        Console.WriteLine($"Duplicated step {idx}");
    }
    else Console.WriteLine("Invalid index.");
}

static void ToggleStepInteractive(TaskList taskList)
{
    if (taskList.Steps.Count == 0) { Console.WriteLine("No steps."); return; }
    Console.Write("Step index to toggle: ");
    if (int.TryParse(Console.ReadLine()?.Trim(), out var idx) && idx >= 0 && idx < taskList.Steps.Count)
    {
        var step = taskList.Steps[idx];
        step.IsEnabled = !step.IsEnabled;
        Console.WriteLine($"Step {idx} is now {(step.IsEnabled ? "enabled" : "disabled")}");
    }
    else Console.WriteLine("Invalid index.");
}

static void ShowStepTypes()
{
    Console.WriteLine("Available automation step types:");
    var groups = StepFactory.GetAvailableStepTypes().GroupBy(s => s.Category);
    foreach (var group in groups)
    {
        Console.WriteLine($"\n  [{group.Key}]");
        foreach (var info in group)
            Console.WriteLine($"    • {info.DisplayName,-18} {info.Description}");
    }
}

static async Task RunTaskListAsync(TaskList taskList)
{
    Console.Write("Serial port (e.g., COM9 or /dev/ttyUSB0): ");
    var port = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(port))
    {
        Console.WriteLine("No port specified.");
        return;
    }

    using var connection = new MotorChainConnection(port);
    var runner = new TaskRunner(connection);
    runner.StepExecuted += (_, e) =>
    {
        var status = e.Success ? "✓" : "✗";
        Console.WriteLine($"  [{status}] Step {e.StepIndex + 1}/{e.TotalSteps}: {e.Step.GetDescription()}");
        if (e.ErrorMessage is not null)
            Console.WriteLine($"       Error: {e.ErrorMessage}");
    };

    try
    {
        Console.WriteLine($"Connecting to {port}...");
        connection.Connect();
        Console.WriteLine($"Running '{taskList.Name}' ({taskList.EnabledStepCount} enabled steps)...\n");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nCancelling...");
        };

        await runner.RunAsync(taskList, cts.Token);
        Console.WriteLine("\nTask list completed successfully.");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\nTask list execution cancelled.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
    }
}

static void ListSerialPorts()
{
    var ports = IMotorConnection.GetAvailablePorts();
    if (ports.Length == 0)
    {
        Console.WriteLine("No serial ports found.");
        return;
    }
    Console.WriteLine("Available serial ports:");
    foreach (var port in ports)
        Console.WriteLine($"  • {port}");
}
