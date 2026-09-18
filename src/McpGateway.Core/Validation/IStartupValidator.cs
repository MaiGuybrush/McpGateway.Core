using System.Collections.Generic;

namespace McpGateway.Core.Validation;

/// <summary>
/// Represents a startup validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Gets the tool name associated with the error.
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the file location where the error occurred.
    /// </summary>
    public string FileLocation { get; init; } = string.Empty;

    /// <summary>
    /// Gets the specific error description.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Exception thrown when startup validation fails.
/// </summary>
public class StartupValidationException : Exception
{
    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the StartupValidationException.
    /// </summary>
    /// <param name="errors">The list of validation errors.</param>
    public StartupValidationException(IReadOnlyList<ValidationError> errors) 
        : base(FormatMessage(errors))
    {
        Errors = errors;
    }

    private static string FormatMessage(IReadOnlyList<ValidationError> errors)
    {
        if (errors == null || errors.Count == 0)
        {
            return "Startup validation failed with 0 errors.";
        }

        var sb = new System.Text.StringBuilder();
        sb.Append($"Startup validation failed with {errors.Count} error(s):");
        foreach (var error in errors)
        {
            sb.Append($"\n  - [{error.ToolName}] ({error.FileLocation}): {error.Description}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets a string representation of all errors.
    /// </summary>
    public string GetErrorDetails()
    {
        var lines = new List<string>();
        lines.Add("Startup Validation Errors:");
        lines.Add("");
        
        foreach (var error in Errors)
        {
            lines.Add($"Tool: {error.ToolName}");
            lines.Add($"  Location: {error.FileLocation}");
            lines.Add($"  Issue: {error.Description}");
            lines.Add("");
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Interface for startup validators.
/// </summary>
public interface IStartupValidator
{
    /// <summary>
    /// Validates the application startup and returns any errors found.
    /// </summary>
    /// <returns>A list of validation errors. Empty list if validation passes.</returns>
    IReadOnlyList<ValidationError> Validate();
}
