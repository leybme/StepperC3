using System.Collections.ObjectModel;

namespace StepperC3.Core.Models;

/// <summary>
/// Represents an ordered list of automation steps that can be created, edited,
/// removed, and reordered. Supports drag-drop reordering via index-based move.
/// </summary>
public class TaskList
{
    /// <summary>Unique identifier for this task list.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name for this task list.</summary>
    public string Name { get; set; } = "Untitled Task";

    /// <summary>Optional description of what this task list does.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>When this task list was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When this task list was last modified.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The ordered collection of automation steps.</summary>
    public ObservableCollection<AutomationStep> Steps { get; set; } = [];

    /// <summary>
    /// Adds a new step to the end of the list.
    /// </summary>
    public void AddStep(AutomationStep step)
    {
        Steps.Add(step);
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Inserts a step at the specified index.
    /// </summary>
    public void InsertStep(int index, AutomationStep step)
    {
        if (index < 0 || index > Steps.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Steps.Insert(index, step);
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes a step by its unique ID.
    /// </summary>
    /// <returns>True if the step was found and removed.</returns>
    public bool RemoveStep(Guid stepId)
    {
        var step = Steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return false;

        Steps.Remove(step);
        ModifiedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Removes the step at the specified index.
    /// </summary>
    public void RemoveStepAt(int index)
    {
        if (index < 0 || index >= Steps.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Steps.RemoveAt(index);
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Moves a step from one index to another (supports drag-drop reordering).
    /// </summary>
    public void MoveStep(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Steps.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex >= Steps.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex));

        if (fromIndex == toIndex) return;

        var step = Steps[fromIndex];
        Steps.RemoveAt(fromIndex);
        Steps.Insert(toIndex, step);
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Replaces the step at the specified index with a new step.
    /// </summary>
    public void ReplaceStep(int index, AutomationStep newStep)
    {
        if (index < 0 || index >= Steps.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Steps[index] = newStep;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Duplicates the step at the specified index, inserting the copy immediately after.
    /// </summary>
    public void DuplicateStep(int index)
    {
        if (index < 0 || index >= Steps.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        // Serialize and deserialize to create a deep copy
        var original = Steps[index];
        var json = System.Text.Json.JsonSerializer.Serialize<AutomationStep>(original);
        var copy = System.Text.Json.JsonSerializer.Deserialize<AutomationStep>(json)!;
        copy.Id = Guid.NewGuid();
        copy.Name = string.IsNullOrEmpty(original.Name) ? "" : $"{original.Name} (copy)";

        Steps.Insert(index + 1, copy);
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Clears all steps from the list.
    /// </summary>
    public void Clear()
    {
        Steps.Clear();
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the count of enabled steps.
    /// </summary>
    public int EnabledStepCount => Steps.Count(s => s.IsEnabled);
}
