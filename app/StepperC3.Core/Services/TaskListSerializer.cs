using System.Text.Json;
using System.Text.Json.Serialization;
using StepperC3.Core.Models;

namespace StepperC3.Core.Services;

/// <summary>
/// Serializes and deserializes task lists to/from JSON files.
/// Supports polymorphic step types via System.Text.Json type discriminators.
/// </summary>
public static class TaskListSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    /// <summary>
    /// Saves a task list to a JSON file.
    /// </summary>
    public static async Task SaveAsync(TaskList taskList, string filePath, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(taskList, Options);
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    /// <summary>
    /// Loads a task list from a JSON file.
    /// </summary>
    public static async Task<TaskList> LoadAsync(string filePath, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<TaskList>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize task list.");
    }

    /// <summary>
    /// Serializes a task list to a JSON string.
    /// </summary>
    public static string Serialize(TaskList taskList)
    {
        return JsonSerializer.Serialize(taskList, Options);
    }

    /// <summary>
    /// Deserializes a task list from a JSON string.
    /// </summary>
    public static TaskList Deserialize(string json)
    {
        return JsonSerializer.Deserialize<TaskList>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize task list.");
    }
}
