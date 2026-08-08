using System.Threading.Tasks;

namespace Jarvis.Core;

/// <summary>
/// Defines the contract for command executors that translate AI responses into system operations.
/// Implements the Command Pattern for decoupling command invocation from execution.
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    /// Gets the name of the executor (e.g., "ApplicationLauncher", "SystemController").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the types of commands this executor can handle.
    /// </summary>
    string[] SupportedCommandTypes { get; }

    /// <summary>
    /// Determines whether this executor can handle the specified command.
    /// </summary>
    /// <param name="commandType">The type of command to check.</param>
    /// <param name="parameters">Optional parameters for the command.</param>
    /// <returns>True if this executor can handle the command; otherwise, false.</returns>
    bool CanExecute(string commandType, object? parameters = null);

    /// <summary>
    /// Executes the specified command asynchronously.
    /// </summary>
    /// <param name="commandType">The type of command to execute.</param>
    /// <param name="parameters">Parameters required for command execution.</param>
    /// <returns>A task representing the asynchronous operation with the execution result.</returns>
    Task<ExecutionResult> ExecuteAsync(string commandType, object? parameters = null);
}

/// <summary>
/// Represents the result of a command execution.
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the command executed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the output message from the command execution.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets any data returned by the command execution.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether user confirmation is required.
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// Gets or sets the confirmation message to display to the user.
    /// </summary>
    public string? ConfirmationMessage { get; set; }

    /// <summary>
    /// Creates a successful execution result.
    /// </summary>
    public static ExecutionResult CreateSuccess(string message, object? data = null)
    {
        return new ExecutionResult
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failed execution result.
    /// </summary>
    public static ExecutionResult CreateFailure(string message, string? errorMessage = null)
    {
        return new ExecutionResult
        {
            Success = false,
            Message = message,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates an execution result that requires user confirmation.
    /// </summary>
    public static ExecutionResult CreateConfirmationRequired(string confirmationMessage)
    {
        return new ExecutionResult
        {
            Success = false,
            RequiresConfirmation = true,
            ConfirmationMessage = confirmationMessage
        };
    }
}
