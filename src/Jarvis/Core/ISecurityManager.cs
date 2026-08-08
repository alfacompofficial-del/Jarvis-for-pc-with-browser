using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;

namespace Jarvis.Core;

/// <summary>
/// Defines the contract for managing security operations including credential encryption and validation.
/// Uses Windows DPAPI for encrypting API keys and sensitive data.
/// Requirement 11.6: Store configuration and data in a secure user-specific directory.
/// Requirement 13.4: Store API credentials securely using Windows Credential Manager.
/// </summary>
public interface ISecurityManager
{
    /// <summary>
    /// Encrypts sensitive data using Windows DPAPI.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <returns>The encrypted data as a base64 string.</returns>
    string EncryptData(string plainText);

    /// <summary>
    /// Decrypts data that was encrypted using Windows DPAPI.
    /// </summary>
    /// <param name="encryptedText">The encrypted data as a base64 string.</param>
    /// <returns>The decrypted plain text.</returns>
    string DecryptData(string encryptedText);

    /// <summary>
    /// Stores an API key securely in Windows Credential Manager.
    /// </summary>
    /// <param name="serviceName">The name of the service (e.g., "GoogleGeminiAPI").</param>
    /// <param name="apiKey">The API key to store.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StoreApiKeyAsync(string serviceName, string apiKey);

    /// <summary>
    /// Retrieves an API key from Windows Credential Manager.
    /// </summary>
    /// <param name="serviceName">The name of the service (e.g., "GoogleGeminiAPI").</param>
    /// <returns>The API key, or null if not found.</returns>
    Task<string?> RetrieveApiKeyAsync(string serviceName);

    /// <summary>
    /// Deletes an API key from Windows Credential Manager.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteApiKeyAsync(string serviceName);

    /// <summary>
    /// Validates that a file path is safe and does not contain directory traversal attacks.
    /// </summary>
    /// <param name="path">The file path to validate.</param>
    /// <param name="allowedBasePath">The allowed base path for file operations.</param>
    /// <returns>True if the path is safe; otherwise, false.</returns>
    bool ValidateFilePath(string path, string allowedBasePath);

    /// <summary>
    /// Checks if a path is accessing a sensitive directory that requires warning.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path is sensitive; otherwise, false.</returns>
    bool IsSensitivePath(string path);

    /// <summary>
    /// Validates a shell command for potentially dangerous operations.
    /// </summary>
    /// <param name="command">The command to validate.</param>
    /// <returns>A validation result indicating if the command is safe or requires confirmation.</returns>
    CommandValidationResult ValidateCommand(string command);
}

/// <summary>
/// Represents the result of command validation.
/// </summary>
public class CommandValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the command is safe to execute.
    /// </summary>
    public bool IsSafe { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command is potentially destructive and requires confirmation.
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// Gets or sets the warning message to display to the user.
    /// </summary>
    public string? WarningMessage { get; set; }

    /// <summary>
    /// Gets or sets the list of detected dangerous operations.
    /// </summary>
    public List<string> DangerousOperations { get; set; } = new();

    public static CommandValidationResult Safe()
    {
        return new CommandValidationResult { IsSafe = true };
    }

    public static CommandValidationResult RequiresUserConfirmation(string warningMessage, params string[] operations)
    {
        return new CommandValidationResult
        {
            IsSafe = false,
            RequiresConfirmation = true,
            WarningMessage = warningMessage,
            DangerousOperations = new List<string>(operations)
        };
    }

    public static CommandValidationResult Unsafe(string warningMessage)
    {
        return new CommandValidationResult
        {
            IsSafe = false,
            RequiresConfirmation = false,
            WarningMessage = warningMessage
        };
    }
}
