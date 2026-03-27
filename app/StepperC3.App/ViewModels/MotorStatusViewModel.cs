using System.Windows.Media;
using StepperC3.Core.Models;

namespace StepperC3.App.ViewModels;

/// <summary>
/// ViewModel for a single motor's live status, updated when the app queries STATUS.
/// </summary>
public class MotorStatusViewModel : ViewModelBase
{
    private MotorStatus _status;

    public MotorStatusViewModel(MotorStatus status)
    {
        _status = status;
    }

    // ─── Forwarded status properties ─────────────────────────────────────
    public int    MotorId     => _status.MotorId;
    public long   Position    => _status.Position;
    public long   Target      => _status.Target;
    public string State       => _status.State;
    public bool   IsEnabled   => _status.IsEnabled;
    public bool   IsFlipped   => _status.IsFlipped;
    public int    Microsteps  => _status.Microsteps;
    public int    CurrentMA   => _status.CurrentMA;
    public int    SpeedHz     => _status.SpeedHz;
    public int    Acceleration => _status.Acceleration;

    public string EnabledText => _status.IsEnabled ? "Yes" : "No";
    public string Header      => $"Motor {_status.MotorId}  ·  {_status.State}";

    public DateTime LastUpdated { get; private set; } = DateTime.MinValue;
    public string LastUpdatedText =>
        LastUpdated == DateTime.MinValue ? "—" : LastUpdated.ToString("HH:mm:ss");

    // ─── Colour-coded by state ────────────────────────────────────────────
    public Brush StateBrush => _status.State switch
    {
        "IDLE"     => new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)),  // green
        "MOVING"   => new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),  // blue
        "HOMING"   => new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)),  // orange
        "STALLED" or
        "ERROR"    => new SolidColorBrush(Color.FromRgb(0xC0, 0x16, 0x3C)),  // red
        "DISABLED" => new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),  // grey
        _          => Brushes.DimGray
    };

    // ─── Called by MainViewModel when a fresh STATUS response arrives ─────
    public void Update(MotorStatus status)
    {
        _status     = status;
        LastUpdated = DateTime.Now;
        OnPropertyChanged(null);   // null → WPF refreshes all bindings on this VM
    }
}
