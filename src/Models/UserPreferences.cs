using System.Collections.Generic;

namespace Jarvis.Models
{
    /// <summary>
    /// Stores user preferences and settings for the Jarvis application.
    /// Persisted in configuration file.
    /// </summary>
    public class UserPreferences
    {
        /// <summary>
        /// Voice input mode (Continuous or PushToTalk).
        /// </summary>
        public VoiceInputMode VoiceMode { get; set; }

        /// <summary>
        /// Preferred language for interaction (e.g., "ru-RU" or "en-US").
        /// </summary>
        public string PreferredLanguage { get; set; }

        /// <summary>
        /// Whether voice responses (TTS) are enabled.
        /// </summary>
        public bool EnableVoiceResponse { get; set; }

        /// <summary>
        /// UI theme ("Light" or "Dark").
        /// </summary>
        public string Theme { get; set; }

        /// <summary>
        /// Font size for UI text.
        /// </summary>
        public int FontSize { get; set; }

        /// <summary>
        /// User's preferred music genres or moods mapped to specific preferences.
        /// Key: mood/genre, Value: player preference or playlist.
        /// </summary>
        public Dictionary<string, string> MusicMoodPreferences { get; set; }

        public UserPreferences()
        {
            VoiceMode = VoiceInputMode.PushToTalk;
            PreferredLanguage = "en-US";
            EnableVoiceResponse = true;
            Theme = "Dark";
            FontSize = 14;
            MusicMoodPreferences = new Dictionary<string, string>();
        }
    }
}
