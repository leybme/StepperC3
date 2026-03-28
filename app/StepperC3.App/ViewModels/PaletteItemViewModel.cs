using System.Windows.Media;
using StepperC3.Core.Models;
using StepperC3.Core.Services;

namespace StepperC3.App.ViewModels;

/// <summary>
/// Wraps a StepTypeInfo for the palette ListBox.
/// Holds editable Motor ID and primary value so the user can configure a step
/// before dragging it to the task list.
/// </summary>
public class PaletteItemViewModel : ViewModelBase
{
    public StepTypeInfo Info { get; }

    public PaletteItemViewModel(StepTypeInfo info)
    {
        Info = info;
        _editMotorId      = "0";
        _editPrimaryValue = DefaultPrimaryValue(info.Type);
    }

    public string DisplayName => Info.DisplayName;

    // ─── Editable fields ────────────────────────────────────────────────

    private string _editMotorId;
    public string EditMotorId
    {
        get => _editMotorId;
        set => SetProperty(ref _editMotorId, value);
    }

    private string _editPrimaryValue;
    public string EditPrimaryValue
    {
        get => _editPrimaryValue;
        set => SetProperty(ref _editPrimaryValue, value);
    }

    // ─── Metadata for template binding ──────────────────────────────────

    public bool HasMotorId =>
        Info.Type is not StepType.Wait and not StepType.RunCommand and not StepType.ResetTask;

    public string? PrimaryValueLabel => Info.Type switch
    {
        StepType.MoveTo          => "pos",
        StepType.MoveBy          => "dist",
        StepType.Wait            => "ms",
        StepType.WaitForIdle     => "timeout",
        StepType.SetSpeed        => "Hz",
        StepType.SetAcceleration => "acc",
        StepType.SetCurrent      => "mA",
        StepType.SetMicrostep    => "1/n",
        StepType.SetPosition     => "pos",
        StepType.RunCommand      => "cmd",
        _                        => null
    };

    public bool HasPrimaryValue => PrimaryValueLabel is not null;

    // ─── Colour (same scheme as StepViewModel.CategoryBrush) ────────────

    public Brush CategoryBrush => Info.Type switch
    {
        StepType.MoveTo  or StepType.MoveBy or StepType.GoHome
            or StepType.FindHome or StepType.Stop =>
            new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),  // blue – motion
        StepType.Wait or StepType.WaitForIdle or StepType.ResetTask or StepType.QueryStatus =>
            new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)),  // orange – timing/flow
        StepType.SetSpeed or StepType.SetAcceleration or StepType.SetCurrent
            or StepType.SetMicrostep or StepType.FlipDirection or StepType.SetPosition =>
            new SolidColorBrush(Color.FromRgb(0x74, 0x47, 0xBF)),  // purple – config
        StepType.Enable or StepType.Disable =>
            new SolidColorBrush(Color.FromRgb(0xC0, 0x16, 0x3C)),  // red – control
        StepType.RunCommand =>
            new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)),  // green – host cmd
        _ => Brushes.DimGray
    };

    // ─── Factory ────────────────────────────────────────────────────────

    /// <summary>Creates a fully configured step from the current edited values.</summary>
    public AutomationStep CreateStep()
    {
        int motorId = int.TryParse(EditMotorId, out var id) ? id : 0;
        var step    = StepFactory.Create(Info.Type, motorId);

        switch (step)
        {
            case MoveToStep s:          if (long.TryParse  (EditPrimaryValue, out var p))   s.Position       = p;   break;
            case MoveByStep s:          if (long.TryParse  (EditPrimaryValue, out var d))   s.Distance       = d;   break;
            case WaitStep s:            if (int.TryParse   (EditPrimaryValue, out var ms))  s.DurationMs     = ms;  break;
            case SetSpeedStep s:        if (uint.TryParse  (EditPrimaryValue, out var sp))  s.SpeedHz        = sp;  break;
            case SetAccelerationStep s: if (uint.TryParse  (EditPrimaryValue, out var ac))  s.AccelerationHz = ac;  break;
            case SetCurrentStep s:      if (uint.TryParse  (EditPrimaryValue, out var cu))  s.CurrentMA      = cu;  break;
            case SetMicrostepStep s:    if (ushort.TryParse(EditPrimaryValue, out var us))  s.Microsteps     = us;  break;
            case SetPositionStep s:     if (long.TryParse  (EditPrimaryValue, out var pos)) s.Position       = pos; break;
            case WaitForIdleStep s:     if (int.TryParse   (EditPrimaryValue, out var to))  s.TimeoutMs      = to;  break;
            case RunCommandStep s:      s.Command = EditPrimaryValue;                                                break;
        }

        return step;
    }

    private static string DefaultPrimaryValue(StepType type) => type switch
    {
        StepType.MoveTo          => "0",
        StepType.MoveBy          => "1000",
        StepType.Wait            => "1000",
        StepType.WaitForIdle     => "30000",
        StepType.SetSpeed        => "1000",
        StepType.SetAcceleration => "5000",
        StepType.SetCurrent      => "800",
        StepType.SetMicrostep    => "16",
        StepType.SetPosition     => "0",
        StepType.RunCommand      => "",
        _                        => ""
    };
}
