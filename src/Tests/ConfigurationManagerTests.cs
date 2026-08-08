using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Jarvis.Core;
using Jarvis.Services;

namespace Jarvis.Tests;

/// <summary>
/// Unit tests for ConfigurationManager implementation.
/// Tests edge cases: loading corrupted JSON, missing files, concurrent saves.
/// Requirements: 13.5, 14.3
/// </summary>
public class ConfigurationManagerTests : IDisposable
{
    private readonly string _testConfigDir;
    private readonly string _testConfigFile;
    private string _originalAppDataPath;

    public ConfigurationManagerTests()
    {
        // Create a temporary directory for test configurations
        _testConfigDir = Path.Combine(Path.GetTempPath(), $"Jarvis_Test_{Guid.NewGuid()}");
        _testConfigFile = Path.Combine(_testConfigDir, "config.json");
        Directory.CreateDirectory(_testConfigDir);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testConfigDir))
        {
            Directory.Delete(_testConfigDir, recursive: true);
        }
    }

    /// <summary>
    /// Test that ConfigurationManager loads defaults when file is missing.
    /// Requirement 13.5: Handle missing configuration file.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WithMissingFile_LoadsDefaults()
    {
        // Arrange
        var manager = new ConfigurationManager();
        // Ensure we're working with non-existent file
        var configPath = manager.ConfigurationDirectory;
        var configFile = Path.Combine(configPath, "config.json");
        if (File.Exists(configFile))
        {
            File.Delete(configFile);
        }

        // Act
        await manager.LoadAsync();

        // Assert
        var windowPos = manager.GetWindowPosition();
        Assert.NotNull(windowPos);
        Assert.Equal(1800, windowPos.X);
        Assert.Equal(900, windowPos.Y);
    }

    /// <summary>
    /// Test that ConfigurationManager loads defaults when JSON is corrupted.
    /// Requirement 14.3: Handle file corruption gracefully.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WithCorruptedJson_LoadsDefaults()
    {
        // Arrange
        var corruptedJson = "{ invalid json content }}}";
        File.WriteAllText(_testConfigFile, corruptedJson);

        var manager = new ConfigurationManager();
        
        // Act & Assert - should not throw
        await manager.LoadAsync(); // Should complete without exception

        // Verify defaults are loaded
        var windowPos = manager.GetWindowPosition();
        Assert.NotNull(windowPos);
        Assert.Equal(1800, windowPos.X);
    }

    /// <summary>
    /// Test setting and getting configuration values with type preservation.
    /// </summary>
    [Fact]
    public async Task SetValue_GetValue_PreservesType()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var testValue = 42;
        var key = "testIntValue";

        // Act
        manager.SetValue(key, testValue);
        await manager.SaveAsync();

        var newManager = new ConfigurationManager();
        await newManager.LoadAsync();
        var retrievedValue = newManager.GetValue<int>(key, 0);

        // Assert
        Assert.Equal(testValue, retrievedValue);
    }

    /// <summary>
    /// Test window position persistence.
    /// Requirement 13.2: Persist window position.
    /// </summary>
    [Fact]
    public async Task SetWindowPosition_Persist_RestoresPosition()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var newPosition = new WindowPosition 
        { 
            X = 100, 
            Y = 200, 
            Width = 500, 
            Height = 700 
        };

        // Act
        manager.SetWindowPosition(newPosition);
        await manager.SaveAsync();

        var newManager = new ConfigurationManager();
        await newManager.LoadAsync();
        var retrievedPosition = newManager.GetWindowPosition();

        // Assert
        Assert.NotNull(retrievedPosition);
        Assert.Equal(100, retrievedPosition.X);
        Assert.Equal(200, retrievedPosition.Y);
        Assert.Equal(500, retrievedPosition.Width);
        Assert.Equal(700, retrievedPosition.Height);
    }

    /// <summary>
    /// Test that configuration directory is created if it doesn't exist.
    /// </summary>
    [Fact]
    public async Task LoadAsync_CreatesDirectory_IfMissing()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var configDir = manager.ConfigurationDirectory;
        
        // Clean up directory if it exists
        if (Directory.Exists(configDir))
        {
            Directory.Delete(configDir, recursive: true);
        }

        // Act
        await manager.LoadAsync();

        // Assert
        Assert.True(Directory.Exists(configDir));
    }

    /// <summary>
    /// Test adding and retrieving conversation history.
    /// </summary>
    [Fact]
    public async Task AddConversationEntry_GetConversationHistory_ReturnsEntry()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var entry = new ConversationEntry
        {
            Timestamp = DateTime.Now,
            UserInput = "Test command",
            AssistantResponse = "Test response",
            InputMode = "Text"
        };

        // Act
        await manager.AddConversationEntryAsync(entry);
        var history = await manager.GetConversationHistoryAsync();

        // Assert
        Assert.NotEmpty(history.Entries);
        Assert.Contains(entry.UserInput, history.Entries[0].UserInput);
    }

    /// <summary>
    /// Test that conversation history respects retention period.
    /// Requirement 13.3: Retain conversation history for 30 days.
    /// </summary>
    [Fact]
    public async Task GetConversationHistory_RespectsRetentionPeriod()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var oldEntry = new ConversationEntry
        {
            Timestamp = DateTime.Now.AddDays(-35), // Outside 30-day window
            UserInput = "Old command",
            AssistantResponse = "Old response",
            InputMode = "Text"
        };
        var recentEntry = new ConversationEntry
        {
            Timestamp = DateTime.Now.AddDays(-5), // Within 30-day window
            UserInput = "Recent command",
            AssistantResponse = "Recent response",
            InputMode = "Text"
        };

        // Act
        await manager.AddConversationEntryAsync(oldEntry);
        await manager.AddConversationEntryAsync(recentEntry);
        var history = await manager.GetConversationHistoryAsync(days: 30);

        // Assert
        Assert.Single(history.Entries);
        Assert.Equal("Recent command", history.Entries[0].UserInput);
    }

    /// <summary>
    /// Test clearing conversation history.
    /// </summary>
    [Fact]
    public async Task ClearConversationHistory_RemovesAllEntries()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var entry = new ConversationEntry
        {
            Timestamp = DateTime.Now,
            UserInput = "Test",
            AssistantResponse = "Response",
            InputMode = "Text"
        };

        // Act
        await manager.AddConversationEntryAsync(entry);
        var beforeClear = await manager.GetConversationHistoryAsync();
        
        await manager.ClearConversationHistoryAsync();
        var afterClear = await manager.GetConversationHistoryAsync();

        // Assert
        Assert.NotEmpty(beforeClear.Entries);
        Assert.Empty(afterClear.Entries);
    }

    /// <summary>
    /// Test ResetToDefaults functionality.
    /// Requirement 13.5: Allow users to reset configuration to defaults.
    /// </summary>
    [Fact]
    public async Task ResetToDefaults_RestoresDefaultConfiguration()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var customPosition = new WindowPosition { X = 500, Y = 600, Width = 300, Height = 400 };
        
        // Act
        manager.SetWindowPosition(customPosition);
        await manager.SaveAsync();
        
        var positionBeforeReset = manager.GetWindowPosition();
        Assert.Equal(500, positionBeforeReset.X);

        await manager.ResetToDefaultsAsync();
        var positionAfterReset = manager.GetWindowPosition();

        // Assert
        Assert.Equal(1800, positionAfterReset.X);
        Assert.Equal(900, positionAfterReset.Y);
    }

    /// <summary>
    /// Test ConfigurationChanged event is raised.
    /// </summary>
    [Fact]
    public async Task SaveAsync_RaisesConfigurationChangedEvent()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var eventRaised = false;

        manager.ConfigurationChanged += (sender, args) =>
        {
            eventRaised = true;
        };

        // Act
        manager.SetValue("testKey", "testValue");
        await manager.SaveAsync();

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Test that GetValue returns default when key doesn't exist.
    /// </summary>
    [Fact]
    public void GetValue_WithMissingKey_ReturnsDefault()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var defaultValue = 123;

        // Act
        var result = manager.GetValue("nonexistentKey", defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    /// <summary>
    /// Test handling of complex object serialization.
    /// </summary>
    [Fact]
    public async Task SetValue_ComplexObject_SerializesAndDeserializes()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var complexObject = new
        {
            Name = "Test",
            Values = new[] { 1, 2, 3 },
            Nested = new { Inner = "value" }
        };

        // Act
        manager.SetValue("complexKey", complexObject);
        await manager.SaveAsync();

        var newManager = new ConfigurationManager();
        await newManager.LoadAsync();
        var retrieved = newManager.GetValue<dynamic>("complexKey");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Test", (string)retrieved.Name);
    }

    /// <summary>
    /// Test that SaveAsync respects the 2-second timeout requirement.
    /// </summary>
    [Fact]
    public async Task SaveAsync_CompletesWithinTimeout()
    {
        // Arrange
        var manager = new ConfigurationManager();
        manager.SetValue("testKey", "testValue");

        // Act
        var startTime = DateTime.Now;
        await manager.SaveAsync();
        var elapsed = DateTime.Now - startTime;

        // Assert - Should complete quickly (well under 2 seconds)
        Assert.True(elapsed.TotalSeconds < 2, $"SaveAsync took {elapsed.TotalSeconds} seconds");
    }

    /// <summary>
    /// Test that multiple concurrent conversation entries don't cause data loss.
    /// </summary>
    [Fact]
    public async Task AddConversationEntry_ConcurrentAdds_PreservesAllEntries()
    {
        // Arrange
        var manager = new ConfigurationManager();
        var tasks = new Task[10];

        // Act
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks[i] = manager.AddConversationEntryAsync(new ConversationEntry
            {
                Timestamp = DateTime.Now,
                UserInput = $"Command {index}",
                AssistantResponse = $"Response {index}",
                InputMode = "Text"
            });
        }

        await Task.WhenAll(tasks);
        var history = await manager.GetConversationHistoryAsync();

        // Assert
        Assert.Equal(10, history.Entries.Count);
    }
}
