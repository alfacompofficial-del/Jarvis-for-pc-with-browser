using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Core;

/// <summary>
/// Defines the contract for managing application configuration and persistence.
/// Handles settings, window position, conversation history, and user preferences.
/// Requirement 13.1: Store user preferences in a configuration file.
/// Requirement 13.4: Store API credentials securely using Windows Credential Manager.
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Loads the configuration from storage.
    /// </summary>
    /// <returns>A task representing the asynchronous load operation.</returns>
    Task LoadAsync();

    /// <summary>
    /// Saves the current configuration to storage.
    /// </summary>
    /// <returns>A task representing the asynchronous save operation.</returns>
    Task SaveAsync();

    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <typeparam name="T">The type of the configuration value.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The configuration value or the default value.</returns>
    T? GetValue<T>(string key, T? defaultValue = default);

    /// <summary>
    /// Sets a configuration value by key.
    /// </summary>
    /// <typeparam name="T">The type of the configuration value.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The value to set.</param>
    void SetValue<T>(string key, T value);

    /// <summary>
    /// Gets the window position from configuration.
    /// </summary>
    /// <returns>The window position, or null if not set.</returns>
    WindowPosition? GetWindowPosition();

    /// <summary>
    /// Sets the window position in configuration.
    /// </summary>
    /// <param name="position">The window position to save.</param>
    void SetWindowPosition(WindowPosition position);

    /// <summary>
    /// Gets conversation history for the specified number of days.
    /// </summary>
    /// <param name="days">The number of days of history to retrieve (default: 30).</param>
    /// <returns>A task representing the asynchronous operation with the conversation history.</returns>
    Task<ConversationHistory> GetConversationHistoryAsync(int days = 30);

    /// <summary>
    /// Adds an entry to the conversation history.
    /// </summary>
    /// <param name="entry">The conversation entry to add.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddConversationEntryAsync(ConversationEntry entry);

    /// <summary>
    /// Clears all conversation history.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearConversationHistoryAsync();

    /// <summary>
    /// Resets all configuration to defaults.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResetToDefaultsAsync();

    /// <summary>
    /// Gets the path to the user-specific configuration directory.
    /// </summary>
    string ConfigurationDirectory { get; }
}

/// <summary>
/// Represents a window position with coordinates and size.
/// </summary>
public class WindowPosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>
/// Represents conversation history.
/// </summary>
public class ConversationHistory
{
    public List<ConversationEntry> Entries { get; set; } = new();
}

/// <summary>
/// Represents a single conversation entry.
/// </summary>
public class ConversationEntry
{
    public DateTime Timestamp { get; set; }
    public string UserInput { get; set; } = string.Empty;
    public string AssistantResponse { get; set; } = string.Empty;
    public string InputMode { get; set; } = "Text"; // "Text" or "Voice"
}
