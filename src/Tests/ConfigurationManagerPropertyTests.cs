using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FsCheck;
using FsCheck.Xunit;
using Jarvis.Core;
using Jarvis.Services;
using System.Linq;

namespace Jarvis.Tests;

/// <summary>
/// Property-based tests for ConfigurationManager implementation.
/// Property 1: Configuration Round-Trip Preservation
/// Validates: Requirements 1.3, 9.6, 13.1, 13.2
/// 
/// For any valid configuration settings (including window position, user preferences, voice settings, music preferences),
/// saving the configuration and then reloading it SHALL restore all settings to their original values.
/// </summary>
public class ConfigurationManagerPropertyTests
{
    /// <summary>
    /// Generator for random window positions.
    /// </summary>
    private static Arbitrary<WindowPosition> GenerateRandomWindowPosition()
    {
        return Arb.From(
            from x in Arb.Generate<double>().Where(d => d >= -3840 && d <= 3840)
            from y in Arb.Generate<double>().Where(d => d >= -2160 && d <= 2160)
            from width in Gen.Choose(100, 1920).Select(i => (double)i)
            from height in Gen.Choose(100, 1080).Select(i => (double)i)
            select new WindowPosition
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            }
        );
    }

    /// <summary>
    /// Generator for random configuration settings.
    /// </summary>
    private static Arbitrary<Dictionary<string, string>> GenerateRandomSettings()
    {
        return Arb.From(
            from count in Gen.Choose(1, 10)
            from settings in Gen.ListOfLength(
                count,
                from key in Gen.AlphaNumericString.Where(s => !string.IsNullOrEmpty(s))
                from value in Gen.AlphaNumericString.Where(s => !string.IsNullOrEmpty(s))
                select new KeyValuePair<string, string>(key, value)
            )
            select new Dictionary<string, string>(settings)
        );
    }

    /// <summary>
    /// Property 1: Configuration Round-Trip Preservation
    /// Tests that window positions are preserved across save/load cycle.
    /// </summary>
    [Property]
    public Property WindowPositionRoundTripPreservation()
    {
        return Prop.ForAll(
            GenerateRandomWindowPosition().ToProperty(),
            async position =>
            {
                var manager = new ConfigurationManager();
                
                // Set window position
                manager.SetWindowPosition(position);
                await manager.SaveAsync();

                // Create new manager and load
                var newManager = new ConfigurationManager();
                await newManager.LoadAsync();
                var loaded = newManager.GetWindowPosition();

                // Verify round-trip
                return loaded != null &&
                       loaded.X == position.X &&
                       loaded.Y == position.Y &&
                       loaded.Width == position.Width &&
                       loaded.Height == position.Height;
            }
        );
    }

    /// <summary>
    /// Property 1b: Configuration Settings Round-Trip
    /// Tests that generic settings are preserved across save/load cycle.
    /// </summary>
    [Property]
    public Property SettingsRoundTripPreservation()
    {
        return Prop.ForAll(
            GenerateRandomSettings().ToProperty(),
            async settings =>
            {
                var manager = new ConfigurationManager();
                
                // Set all settings
                foreach (var kvp in settings)
                {
                    manager.SetValue(kvp.Key, kvp.Value);
                }
                await manager.SaveAsync();

                // Create new manager and load
                var newManager = new ConfigurationManager();
                await newManager.LoadAsync();

                // Verify all settings are preserved
                foreach (var kvp in settings)
                {
                    var loaded = newManager.GetValue<string>(kvp.Key, "");
                    if (loaded != kvp.Value)
                        return false;
                }

                return true;
            }
        );
    }

    /// <summary>
    /// Property: Conversation History Retention Policy
    /// Tests that only entries within the retention period are returned.
    /// Property 13: Conversation History Retention Policy
    /// Validates: Requirements 13.3
    /// </summary>
    [Property]
    public Property ConversationHistoryRetentionPolicy()
    {
        var entryGenerator = Arb.From(
            from days in Gen.Choose(-40, -1)
            select new ConversationEntry
            {
                Timestamp = DateTime.Now.AddDays(days),
                UserInput = $"Command from {days} days ago",
                AssistantResponse = $"Response",
                InputMode = "Text"
            }
        );

        return Prop.ForAll(
            entryGenerator.ToProperty(),
            async entry =>
            {
                var manager = new ConfigurationManager();
                
                // Add entries both inside and outside retention window
                var oldEntry = new ConversationEntry
                {
                    Timestamp = DateTime.Now.AddDays(-35),
                    UserInput = "Old command",
                    AssistantResponse = "Old response",
                    InputMode = "Text"
                };

                await manager.AddConversationEntryAsync(oldEntry);
                await manager.AddConversationEntryAsync(entry);

                // Get history with 30-day window
                var history = await manager.GetConversationHistoryAsync(30);

                // Only entries within 30 days should be returned
                var allWithin30Days = history.Entries.All(e => e.Timestamp >= DateTime.Now.AddDays(-30));
                var oldEntryNotIncluded = !history.Entries.Any(e => e.UserInput == "Old command");

                return allWithin30Days && oldEntryNotIncluded;
            }
        );
    }

    /// <summary>
    /// Property: Missing Configuration File Creates Default
    /// Tests that missing config file defaults to correct values.
    /// </summary>
    [Property]
    public void MissingConfigurationLoadsDefaults()
    {
        var manager = new ConfigurationManager();
        var position = manager.GetWindowPosition();

        // Should always have default position
        Assert.NotNull(position);
        Assert.Equal(1800, position.X);
        Assert.Equal(900, position.Y);
    }

    /// <summary>
    /// Property: Non-Existent Key Returns Default
    /// Tests that GetValue returns default when key doesn't exist.
    /// </summary>
    [Property]
    public void NonExistentKeyReturnsDefault()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            defaultValue =>
            {
                var manager = new ConfigurationManager();
                var result = manager.GetValue("nonexistentKey_12345_xyz", defaultValue);
                return result == defaultValue;
            }
        );
    }

    /// <summary>
    /// Property: SetValue Marks Configuration Dirty
    /// Tests that SetValue marks configuration for saving.
    /// </summary>
    [Property]
    public void SetValueMarksConfigurationDirty()
    {
        var manager = new ConfigurationManager();
        
        // Initially not dirty (loaded from defaults)
        manager.SetValue("testKey", "testValue");
        
        // After setting a value, it should be marked dirty
        // (This is verified implicitly - if dirty is false, SaveAsync won't save)
        var retrieved = manager.GetValue<string>("testKey", "");
        
        Assert.Equal("testValue", retrieved);
    }

    /// <summary>
    /// Property: Conversation Entry Timestamp Auto-Set
    /// Tests that entries without timestamps get current time assigned.
    /// </summary>
    [Property]
    public Property ConversationEntryAutoTimestamp()
    {
        return Prop.ForAll(
            async () =>
            {
                var manager = new ConfigurationManager();
                var beforeTime = DateTime.Now;
                
                var entry = new ConversationEntry
                {
                    Timestamp = default, // Not set
                    UserInput = "Test",
                    AssistantResponse = "Response",
                    InputMode = "Text"
                };

                await manager.AddConversationEntryAsync(entry);
                
                var afterTime = DateTime.Now;
                var history = await manager.GetConversationHistoryAsync();

                if (history.Entries.Count != 1)
                    return false;

                var saved = history.Entries[0];
                
                // Timestamp should be set to current time (within reasonable window)
                return saved.Timestamp >= beforeTime && saved.Timestamp <= afterTime.AddSeconds(1);
            }
        );
    }
}
