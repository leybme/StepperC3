using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using StepperC3.Core.Models;
using StepperC3.Core.Services;

namespace StepperC3.App.ViewModels;

/// <summary>
/// Main ViewModel for the StepperC3 WPF application.
/// Manages task list, connection, and step execution.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private TaskList _taskList;
    private IMotorConnection? _connection;
    private TaskRunner? _runner;
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        _taskList = new TaskList { Name = "Untitled Task" };
        Steps = [];
        AvailableStepTypes = new ObservableCollection<PaletteItemViewModel>(
            StepFactory.GetAvailableStepTypes().Select(i => new PaletteItemViewModel(i)));
        AvailablePorts = new ObservableCollection<string>(SerialPort.GetPortNames());
        LogMessages = [];
        MotorStatuses = [];

        // Commands
        NewTaskListCommand = new RelayCommand(NewTaskList);
        OpenTaskListCommand = new RelayCommand(async () => await OpenTaskListAsync());
        SaveTaskListCommand = new RelayCommand(async () => await SaveTaskListAsync());
        SaveTaskListAsCommand = new RelayCommand(async () => await SaveTaskListAsAsync());
        AddStepCommand = new RelayCommand(AddStep, () => SelectedStepType is not null);
        RemoveStepCommand = new RelayCommand(RemoveStep, () => SelectedStep is not null);
        MoveStepUpCommand = new RelayCommand(MoveStepUp, () => SelectedStep is not null && SelectedStepIndex > 0);
        MoveStepDownCommand = new RelayCommand(MoveStepDown, () => SelectedStep is not null && SelectedStepIndex < Steps.Count - 1);
        DuplicateStepCommand = new RelayCommand(DuplicateStep, () => SelectedStep is not null);
        ToggleStepCommand = new RelayCommand(ToggleStep, () => SelectedStep is not null);
        ClearStepsCommand = new RelayCommand(ClearSteps, () => Steps.Count > 0);
        ConnectCommand = new RelayCommand(Connect, () => !IsConnected && !string.IsNullOrEmpty(SelectedPort));
        DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
        RunCommand = new RelayCommand(async () => await RunAsync(), () => IsConnected && !IsRunning && Steps.Count > 0);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        QueryStatusCommand = new RelayCommand(
            async () => await QueryAllStatusAsync(),
            () => IsConnected && !IsRunning && !IsQueryingStatus);
    }

    // ─── Properties ──────────────────────────────────────────────────────

    public ObservableCollection<StepViewModel> Steps { get; }
    public ObservableCollection<PaletteItemViewModel> AvailableStepTypes { get; }
    public ObservableCollection<string> AvailablePorts { get; }
    public ObservableCollection<string> LogMessages { get; }
    public ObservableCollection<MotorStatusViewModel> MotorStatuses { get; }

    public int EnabledStepCount => Steps.Count(s => s.IsEnabled);

    private string _taskListName = "Untitled Task";
    public string TaskListName
    {
        get => _taskListName;
        set => SetProperty(ref _taskListName, value);
    }

    private string _taskListDescription = string.Empty;
    public string TaskListDescription
    {
        get => _taskListDescription;
        set => SetProperty(ref _taskListDescription, value);
    }

    private StepViewModel? _selectedStep;
    public StepViewModel? SelectedStep
    {
        get => _selectedStep;
        set => SetProperty(ref _selectedStep, value);
    }

    private int _selectedStepIndex = -1;
    public int SelectedStepIndex
    {
        get => _selectedStepIndex;
        set => SetProperty(ref _selectedStepIndex, value);
    }

    private PaletteItemViewModel? _selectedStepType;
    public PaletteItemViewModel? SelectedStepType
    {
        get => _selectedStepType;
        set => SetProperty(ref _selectedStepType, value);
    }

    private string? _selectedPort;
    public string? SelectedPort
    {
        get => _selectedPort;
        set => SetProperty(ref _selectedPort, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private int _progressValue;
    public int ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    private int _progressMax = 100;
    public int ProgressMax
    {
        get => _progressMax;
        set => SetProperty(ref _progressMax, value);
    }

    private int _motorCount = 1;
    public int MotorCount
    {
        get => _motorCount;
        set => SetProperty(ref _motorCount, Math.Clamp(value, 1, 8));
    }

    private bool _isQueryingStatus;
    public bool IsQueryingStatus
    {
        get => _isQueryingStatus;
        set => SetProperty(ref _isQueryingStatus, value);
    }

    private string? _currentFilePath;
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set
        {
            SetProperty(ref _currentFilePath, value);
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string WindowTitle =>
        CurrentFilePath is not null
            ? $"StepperC3 – {Path.GetFileName(CurrentFilePath)}"
            : $"StepperC3 – {TaskListName}";

    // ─── Commands ────────────────────────────────────────────────────────

    public ICommand NewTaskListCommand { get; }
    public ICommand OpenTaskListCommand { get; }
    public ICommand SaveTaskListCommand { get; }
    public ICommand SaveTaskListAsCommand { get; }
    public ICommand AddStepCommand { get; }
    public ICommand RemoveStepCommand { get; }
    public ICommand MoveStepUpCommand { get; }
    public ICommand MoveStepDownCommand { get; }
    public ICommand DuplicateStepCommand { get; }
    public ICommand ToggleStepCommand { get; }
    public ICommand ClearStepsCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RefreshPortsCommand { get; }
    public ICommand QueryStatusCommand { get; }

    // ─── Task List Operations ────────────────────────────────────────────

    private void NewTaskList()
    {
        _taskList = new TaskList { Name = "Untitled Task" };
        Steps.Clear();
        TaskListName = _taskList.Name;
        TaskListDescription = string.Empty;
        CurrentFilePath = null;
        LogMessages.Clear();
        Log("New task list created.");
        OnPropertyChanged(nameof(EnabledStepCount));
    }

    private async Task OpenTaskListAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "StepperC3 Task (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Open Task List"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _taskList = await TaskListSerializer.LoadAsync(dlg.FileName);
            Steps.Clear();
            foreach (var step in _taskList.Steps)
                Steps.Add(new StepViewModel(step));

            TaskListName = _taskList.Name;
            TaskListDescription = _taskList.Description;
            CurrentFilePath = dlg.FileName;
            Log($"Loaded: {dlg.FileName} ({_taskList.Steps.Count} steps)");
            OnPropertyChanged(nameof(EnabledStepCount));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load task list:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveTaskListAsync()
    {
        if (CurrentFilePath is not null)
            await SaveToFileAsync(CurrentFilePath);
        else
            await SaveTaskListAsAsync();
    }

    private async Task SaveTaskListAsAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "StepperC3 Task (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Save Task List",
            FileName = $"{TaskListName}.json"
        };
        if (dlg.ShowDialog() != true) return;

        await SaveToFileAsync(dlg.FileName);
    }

    private async Task SaveToFileAsync(string filePath)
    {
        try
        {
            SyncTaskListFromSteps();
            await TaskListSerializer.SaveAsync(_taskList, filePath);
            CurrentFilePath = filePath;
            Log($"Saved: {filePath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save task list:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ─── Step Operations ─────────────────────────────────────────────────

    private void AddStep()
    {
        if (SelectedStepType is null) return;

        var step = SelectedStepType.CreateStep();
        InsertStepAt(Steps.Count, step);
    }

    private void RemoveStep()
    {
        if (SelectedStep is null || SelectedStepIndex < 0) return;
        RemoveStepAt(SelectedStepIndex);
    }

    /// <summary>Removes the step at the given index from both the view and model.</summary>
    public void RemoveStepAt(int idx)
    {
        if (idx < 0 || idx >= Steps.Count) return;

        var desc = Steps[idx].Description;
        Steps.RemoveAt(idx);
        _taskList.RemoveStepAt(idx);

        if (Steps.Count > 0)
            SelectedStepIndex = Math.Min(idx, Steps.Count - 1);

        Log($"Removed: {desc}");
        OnPropertyChanged(nameof(EnabledStepCount));
    }

    /// <summary>Inserts a new step at the given index in both the view and model.</summary>
    public void InsertStepAt(int idx, AutomationStep step)
    {
        idx = Math.Clamp(idx, 0, Steps.Count);
        var vm = new StepViewModel(step);
        Steps.Insert(idx, vm);
        _taskList.InsertStep(idx, step);
        SelectedStepIndex = idx;
        Log($"Inserted: {step.GetDescription()}");
        OnPropertyChanged(nameof(EnabledStepCount));
    }

    private void MoveStepUp()
    {
        if (SelectedStep is null || SelectedStepIndex <= 0) return;
        var idx = SelectedStepIndex;
        MoveStepInList(idx, idx - 1);
    }

    private void MoveStepDown()
    {
        if (SelectedStep is null || SelectedStepIndex >= Steps.Count - 1) return;
        var idx = SelectedStepIndex;
        MoveStepInList(idx, idx + 1);
    }

    /// <summary>Moves a step from one index to another in both the view and model.</summary>
    public void MoveStepInList(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Steps.Count || toIndex < 0 || toIndex >= Steps.Count)
            return;

        Steps.Move(fromIndex, toIndex);
        _taskList.MoveStep(fromIndex, toIndex);
        SelectedStepIndex = toIndex;
    }

    private void DuplicateStep()
    {
        if (SelectedStep is null || SelectedStepIndex < 0) return;

        var idx = SelectedStepIndex;
        _taskList.DuplicateStep(idx);

        // The duplicate is at idx + 1 in the task list
        var newStep = _taskList.Steps[idx + 1];
        var vm = new StepViewModel(newStep);
        Steps.Insert(idx + 1, vm);
        SelectedStepIndex = idx + 1;
        Log($"Duplicated step {idx}");
        OnPropertyChanged(nameof(EnabledStepCount));
    }

    private void ToggleStep()
    {
        if (SelectedStep is null) return;
        SelectedStep.IsEnabled = !SelectedStep.IsEnabled;
        Log($"Step {SelectedStepIndex} is now {(SelectedStep.IsEnabled ? "enabled" : "disabled")}");
        OnPropertyChanged(nameof(EnabledStepCount));
    }

    private void ClearSteps()
    {
        Steps.Clear();
        _taskList.Clear();
        Log("All steps cleared.");
        OnPropertyChanged(nameof(EnabledStepCount));
    }

    // ─── Connection ──────────────────────────────────────────────────────

    private void Connect()
    {
        if (string.IsNullOrEmpty(SelectedPort)) return;

        try
        {
            _connection = new MotorChainConnection(SelectedPort);
            _connection.Connect();
            IsConnected = true;
            StatusText = $"Connected to {SelectedPort}";
            Log($"Connected to {SelectedPort}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to connect:\n{ex.Message}", "Connection Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Disconnect()
    {
        _connection?.Disconnect();
        _connection?.Dispose();
        _connection = null;
        IsConnected = false;
        StatusText = "Disconnected";
        Log("Disconnected.");
    }

    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in SerialPort.GetPortNames())
            AvailablePorts.Add(port);
    }

    // ─── Execution ───────────────────────────────────────────────────────

    private async Task RunAsync()
    {
        if (_connection is null || !IsConnected) return;

        SyncTaskListFromSteps();
        _runner = new TaskRunner(_connection);
        _cts = new CancellationTokenSource();
        IsRunning = true;
        ProgressValue = 0;
        ProgressMax = _taskList.EnabledStepCount;
        StatusText = "Running...";
        Log($"Running '{TaskListName}' ({_taskList.EnabledStepCount} enabled steps)...");

        _runner.StepExecuted += (_, e) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressValue = e.StepIndex + 1;
                var status = e.Success ? "✓" : "✗";
                Log($"  [{status}] Step {e.StepIndex + 1}/{e.TotalSteps}: {e.Step.GetDescription()}");
                if (e.ErrorMessage is not null)
                    Log($"       Error: {e.ErrorMessage}");
            });
        };

        try
        {
            await _runner.RunAsync(_taskList, _cts.Token);
            StatusText = "Completed";
            Log("Task list completed successfully.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
            Log("Task list execution cancelled.");
        }
        catch (Exception ex)
        {
            StatusText = "Error";
            Log($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
            _runner = null;
        }
    }

    private void Stop()
    {
        _cts?.Cancel();
        _runner?.Cancel();
    }
    // ─── Motor Status Query ──────────────────────────────────────────────────

    private async Task QueryAllStatusAsync()
    {
        if (_connection is null || !IsConnected) return;
        IsQueryingStatus = true;
        StatusText = "Querying motors...";
        Log($"Querying {MotorCount} motor(s)...");

        // Sync collection size to MotorCount
        while (MotorStatuses.Count < MotorCount)
            MotorStatuses.Add(new MotorStatusViewModel(
                new MotorStatus { MotorId = MotorStatuses.Count }));
        while (MotorStatuses.Count > MotorCount)
            MotorStatuses.RemoveAt(MotorStatuses.Count - 1);

        for (int id = 0; id < MotorCount; id++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                await _connection.SendCommandAsync($"{id} STATUS", cts.Token);

                while (true)
                {
                    var line = await _connection.ReadLineAsync(cts.Token);
                    if (line is null) break;  // serial read timeout
                    var parsed = MotorStatus.TryParse(line.Trim());
                    if (parsed?.MotorId == id)
                    {
                        MotorStatuses[id].Update(parsed);
                        Log($"Motor {id}: {parsed.State}  pos={parsed.Position}  {parsed.CurrentMA}mA  {parsed.SpeedHz}Hz");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log($"Motor {id}: STATUS timeout");
            }
            catch (Exception ex)
            {
                Log($"Motor {id}: STATUS error – {ex.Message}");
            }
        }

        IsQueryingStatus = false;
        StatusText = $"Motor status updated ({DateTime.Now:HH:mm:ss})";
    }
    // ─── Helpers ─────────────────────────────────────────────────────────

    private void SyncTaskListFromSteps()
    {
        _taskList.Name = TaskListName;
        _taskList.Description = TaskListDescription;
        _taskList.Steps.Clear();
        foreach (var vm in Steps)
            _taskList.Steps.Add(vm.Step);
    }

    private void Log(string message)
    {
        LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
