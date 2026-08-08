using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jarvis.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jarvis.Services;

/// <summary>
/// Implements configuration management with JSON persistence.
/// Stores settings in %AppData%\Jarvis\config.json with automatic fallback to defaults.
/// Requirement 13.1: Store user preferences in a configuration file.
/// Requirement 13.2: Persist window position, size, and appearance settings.
/// Requirement 13.5: Allow users to reset configuration to defaults.
/// Requirement 13.6: Load saved configuration on startup within 2 seconds.
/// </summary>
public class ConfigurationManager : IConfigurationManager
{
    private readonly string _configurationDirectory;
    private readonly string _configFilePath;
    private JObject _configuration = new();
    private bool _isDirty = false;
    
    // Default configuration values
    private static readonly Dictionary<string, object> DefaultConfiguration = new()
    {
        { "windowPosition", new { x = 1800, y = 900, width = 400, height = 600 } },
        { "voiceSettings", new { mode = "PushToTalk", language = "ru-RU", enableVoiceResponse = true } },
        { "conversationHistory", new { retentionDays = 30, maxEntries = 1000 } },
        { "preferences", new { theme = "Dark", fontSize = 14 } },
        { "lastLoadTime", DateTime.Now }
    };

    /// <summary>
    /// Initializes a new instance of the ConfigurationManager class.
    /// </summary>
    public ConfigurationManager()
    {
        _configurationDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Jarvis"
        );
        
        _configFilePath = Path.Combine(_configurationDirectory, "config.json");
        
        // Initialize with defaults
        InitializeDefaultConfiguration();
    }

    /// <summary>
    /// Gets the configuration directory path.
    /// </summary>
    public string ConfigurationDirectory => _configurationDirectory;

    /// <summary>
    /// Loads the configuration from disk.
    /// If the file is corrupted or missing, loads defaults.
    /// Requirement 13.6: Load within 2 seconds timeout.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_configurationDirectory))
            {
                Directory.CreateDirectory(_configurationDirectory);
            }

            // If file doesn't exist, use defaults
            if (!File.Exists(_configFilePath))
            {
                InitializeDefaultConfiguration();
                return;
            }

            // Use a timeout to prevent hanging
            var loadTask = Task.Run(() =>
            {
                try
                {
                    var json = File.ReadAllText(_configFilePath);
                    _configuration = JObject.Parse(json);
                    _isDirty = false;
                }
                catch (JsonException)
                {
                    // File is corrupted, load defaults
                    InitializeDefaultConfiguration();
                }
            });

            // 2-second timeout as per Requirement 13.6
            if (await Task.WhenAny(loadTask, Task.Delay(2000)) == loadTask)
            {
                await loadTask;
            }
            else
            {
                // Timeout occurred, load defaults
                InitializeDefaultConfiguration();
            }
        }
        catch (Exception)
        {
            // Any other error, load defaults
            InitializeDefaultConfiguration();
        }
    }

    /// <summary>
    /// Saves the current configuration to disk.
    /// </summary>
    public async Task SaveAsync()
    {
        if (!_isDirty)
        {
            return;
        }

        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_configurationDirectory))
            {
                Directory.CreateDirectory(_configurationDirectory);
            }

            // Write to temp file first, then move (atomic operation)
            var tempPath = _configFilePath + ".tmp";
            var json = _configuration.ToString(Formatting.Indented);
            
            await Task.Run(() =>
            {
                File.WriteAllText(tempPath, json);
                
                // Atomic replacement
                if (File.Exists(_configFilePath))
                {
                    File.Delete(_configFilePath);
                }
                File.Move(tempPath, _configFilePath);
            });

            _isDirty = false;
            
            // Raise event
            OnConfigurationChanged();
        }
        catch (Exception ex)
        {
            // Log but don't throw - configuration changes should not crash the app
            System.Diagnostics.Debug.WriteLine($"Failed to save configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a configuration value by key using generic typing.
    /// </summary>
    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        try
        {
            if (_configuration.TryGetValue(key, out var token))
            {
                return token.ToObject<T>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get configuration value '{key}': {ex.Message}");
        }

        return defaultValue;
    }

    /// <summary>
    /// Sets a configuration value by key using generic typing.
    /// </summary>
    public void SetValue<T>(string key, T value)
    {
        try
        {
            _configuration[key] = JToken.FromObject(value);
            _isDirty = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set configuration value '{key}': {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the window position from configuration.
    /// </summary>
    public WindowPosition? GetWindowPosition()
    {
        try
        {
            if (_configuration.TryGetValue("windowPosition", out var token))
            {
                var pos = token.ToObject<WindowPosition>();
                return pos;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get window position: {ex.Message}");
        }

        // Return default position if not found or error
        return new WindowPosition { X = 1800, Y = 900, Width = 400, Height = 600 };
    }

    /// <summary>
    /// Sets the window position in configuration.
    /// </summary>
    public void SetWindowPosition(WindowPosition position)
    {
        try
        {
            _configuration["windowPosition"] = JToken.FromObject(position);
            _isDirty = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set window position: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets conversation history for the specified number of days.
    /// Filters entries to retain only those within the retention period.
    /// </summary>
    public async Task<ConversationHistory> GetConversationHistoryAsync(int days = 30)
    {
        return await Task.Run(() =>
        {
            var history = new ConversationHistory();

            try
            {
                var historyPath = Path.Combine(_configurationDirectory, "history.json");
                
                if (!File.Exists(historyPath))
                {
                    return history;
                }

                var json = File.ReadAllText(historyPath);
                var entries = JsonConvert.DeserializeObject<List<ConversationEntry>>(json) ?? new();

                var cutoffDate = DateTime.Now.AddDays(-days);
                
                // Filter entries within retention period
                foreach (var entry in entries)
                {
                    if (entry.Timestamp >= cutoffDate)
                    {
                        history.Entries.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get conversation history: {ex.Message}");
            }

            return history;
        });
    }

    /// <summary>
    /// Adds an entry to the conversation history.
    /// </summary>
    public async Task AddConversationEntryAsync(ConversationEntry entry)
    {
        await Task.Run(() =>
        {
            try
            {
                var historyPath = Path.Combine(_configurationDirectory, "history.json");
                
                var entries = new List<ConversationEntry>();
                
                if (File.Exists(historyPath))
                {
                    var json = File.ReadAllText(historyPath);
                    entries = JsonConvert.DeserializeObject<List<ConversationEntry>>(json) ?? new();
                }

                // Ensure timestamp is set
                if (entry.Timestamp == default)
                {
                    entry.Timestamp = DateTime.Now;
                }

                entries.Add(entry);

                // Enforce max entries limit
                const int maxEntries = 1000;
                if (entries.Count > maxEntries)
                {
                    entries = entries.Skip(entries.Count - maxEntries).ToList();
                }

                var json_output = JsonConvert.SerializeObject(entries, Formatting.Indented);
                File.WriteAllText(historyPath, json_output);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add conversation entry: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Clears all conversation history.
    /// </summary>
    public async Task ClearConversationHistoryAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var historyPath = Path.Combine(_configurationDirectory, "history.json");
                
                if (File.Exists(historyPath))
                {
                    File.Delete(historyPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear conversation history: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Resets all configuration to defaults.
    /// </summary>
    public async Task ResetToDefaultsAsync()
    {
        await Task.Run(() =>
        {
            InitializeDefaultConfiguration();
            _isDirty = true;
        });
    }

    /// <summary>
    /// Initializes configuration with default values.
    /// </summary>
    private void InitializeDefaultConfiguration()
    {
        _configuration = JObject.FromObject(DefaultConfiguration);
        _isDirty = false;
    }

    /// <summary>
    /// Event raised when configuration changes.
    /// </summary>
    public event EventHandler? ConfigurationChanged;

    /// <summary>
    /// Raises the ConfigurationChanged event.
    /// </summary>
    protected virtual void OnConfigurationChanged()
    {
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
}
