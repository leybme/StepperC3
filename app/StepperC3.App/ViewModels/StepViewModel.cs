using System.Windows.Input;
using System.Windows.Media;
using StepperC3.Core.Models;

namespace StepperC3.App.ViewModels;

/// <summary>
/// ViewModel wrapper for an AutomationStep.
/// Supports inline quick-editing (micro:bit style) via IsEditing + per-type value properties.
/// </summary>
public class StepViewModel : ViewModelBase
{
    public AutomationStep Step { get; }

    // saved values for Cancel
    private string _savedPrimary   = string.Empty;
    private string _savedSecondary = string.Empty;
    private string _savedMotorId   = string.Empty;

    public StepViewModel(AutomationStep step)
    {
        Step = step;

        BeginEditCommand = new RelayCommand(() =>
        {
            _savedPrimary   = PrimaryValue;
            _savedSecondary = SecondaryValue ?? string.Empty;
            _savedMotorId   = Step.MotorId?.ToString() ?? string.Empty;
            _editMotorId    = _savedMotorId;
            OnPropertyChanged(nameof(EditMotorId));
            IsEditing = true;
        });

        CommitEditCommand = new RelayCommand(() =>
        {
            if (int.TryParse(EditMotorId, out var mid))
                Step.MotorId = mid;
            else if (string.IsNullOrWhiteSpace(EditMotorId))
                Step.MotorId = null;
            IsEditing = false;
            Refresh();
        });

        CancelEditCommand = new RelayCommand(() =>
        {
            PrimaryValue   = _savedPrimary;
            SecondaryValue = _savedSecondary;
            if (int.TryParse(_savedMotorId, out var mid))
                Step.MotorId = mid;
            else
                Step.MotorId = null;
            _editMotorId = _savedMotorId;
            OnPropertyChanged(nameof(EditMotorId));
            IsEditing = false;
            Refresh();
        });
    }

    // ─── Edit commands ────────────────────────────────────────────────────
    public ICommand BeginEditCommand  { get; }
    public ICommand CommitEditCommand { get; }
    public ICommand CancelEditCommand { get; }

    // ─── Edit mode ────────────────────────────────────────────────────────
    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    private string _editMotorId = string.Empty;
    public string EditMotorId
    {
        get => _editMotorId;
        set => SetProperty(ref _editMotorId, value);
    }

    // ─── Primary value (main numeric/text param for this step type) ───────
    public string? PrimaryValueLabel => Step switch
    {
        MoveToStep          => "Position",
        MoveByStep          => "Distance",
        WaitStep            => "Duration (ms)",
        WaitForIdleStep     => "Timeout (ms)",
        SetSpeedStep        => "Speed (Hz)",
        SetAccelerationStep => "Accel (steps/s\u00b2)",
        SetCurrentStep      => "Current (mA)",
        SetMicrostepStep    => "Microsteps",
        SetPositionStep     => "Position",
        RunCommandStep      => "Command",
        _                   => null
    };

    public bool HasPrimaryValue => PrimaryValueLabel is not null;

    public string PrimaryValue
    {
        get => Step switch
        {
            MoveToStep s          => s.Position.ToString(),
            MoveByStep s          => s.Distance.ToString(),
            WaitStep s            => s.DurationMs.ToString(),
            WaitForIdleStep s     => s.TimeoutMs.ToString(),
            SetSpeedStep s        => s.SpeedHz.ToString(),
            SetAccelerationStep s => s.AccelerationHz.ToString(),
            SetCurrentStep s      => s.CurrentMA.ToString(),
            SetMicrostepStep s    => s.Microsteps.ToString(),
            SetPositionStep s     => s.Position.ToString(),
            RunCommandStep s      => s.Command,
            _                     => string.Empty
        };
        set
        {
            switch (Step)
            {
                case MoveToStep s:          if (long.TryParse(value, out var p))    s.Position       = p;   break;
                case MoveByStep s:          if (long.TryParse(value, out var d))    s.Distance       = d;   break;
                case WaitStep s:            if (int.TryParse(value, out var ms))    s.DurationMs     = ms;  break;
                case WaitForIdleStep s:     if (int.TryParse(value, out var to))    s.TimeoutMs      = to;  break;
                case SetSpeedStep s:        if (uint.TryParse(value, out var sp))   s.SpeedHz        = sp;  break;
                case SetAccelerationStep s: if (uint.TryParse(value, out var ac))   s.AccelerationHz = ac;  break;
                case SetCurrentStep s:      if (uint.TryParse(value, out var cu))   s.CurrentMA      = cu;  break;
                case SetMicrostepStep s:    if (ushort.TryParse(value, out var us)) s.Microsteps     = us;  break;
                case SetPositionStep s:     if (long.TryParse(value, out var pos))  s.Position       = pos; break;
                case RunCommandStep s:      s.Command = value;                                               break;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(Description));
        }
    }

    // ─── Secondary value (RunCommandStep arguments) ───────────────────────
    public string? SecondaryValueLabel => Step is RunCommandStep ? "Args" : null;
    public bool HasSecondaryValue => SecondaryValueLabel is not null;

    public string? SecondaryValue
    {
        get => Step is RunCommandStep rc ? rc.Arguments : null;
        set
        {
            if (Step is RunCommandStep rc)
                rc.Arguments = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Description));
        }
    }

    // ─── Whether to show Motor ID field (Wait/RunCommand/ResetTask don't use it) ────
    public bool ShowMotorId => Step is not WaitStep and not RunCommandStep and not ResetTaskStep;

    // ─── Category colour (micro:bit block style) ──────────────────────────
    public Brush CategoryBrush => Step.Type switch
    {
        StepType.MoveTo  or StepType.MoveBy or StepType.GoHome or StepType.FindHome =>
            new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),   // blue  – movement
        StepType.Wait or StepType.WaitForIdle or StepType.ResetTask or StepType.QueryStatus =>
            new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)),   // orange – timing/flow
        StepType.SetSpeed or StepType.SetAcceleration or StepType.SetCurrent or
        StepType.SetMicrostep or StepType.FlipDirection or StepType.SetPosition =>
            new SolidColorBrush(Color.FromRgb(0x74, 0x47, 0xBF)),   // purple – config
        StepType.Enable or StepType.Disable or StepType.Stop =>
            new SolidColorBrush(Color.FromRgb(0xC0, 0x16, 0x3C)),   // red    – control
        StepType.RunCommand =>
            new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)),   // green  – host command
        _ => Brushes.DimGray
    };

    // ─── Display properties ───────────────────────────────────────────────
    public string Description => Step.GetDescription();
    public StepType Type      => Step.Type;

    public bool IsEnabled
    {
        get => Step.IsEnabled;
        set
        {
            if (Step.IsEnabled == value) return;
            Step.IsEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Description));
        }
    }

    public int? MotorId
    {
        get => Step.MotorId;
        set
        {
            Step.MotorId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Description));
        }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(MotorId));
        OnPropertyChanged(nameof(PrimaryValue));
        OnPropertyChanged(nameof(SecondaryValue));
    }
}
