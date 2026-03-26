using StepperC3.Core.Models;

namespace StepperC3.App.ViewModels;

/// <summary>
/// ViewModel wrapper for an AutomationStep, providing UI-bindable properties.
/// </summary>
public class StepViewModel : ViewModelBase
{
    public AutomationStep Step { get; }

    public StepViewModel(AutomationStep step)
    {
        Step = step;
    }

    public string Description => Step.GetDescription();
    public StepType Type => Step.Type;
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

    /// <summary>Notifies the UI to re-read the description after edits.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(MotorId));
    }
}
