using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Core;

/// <summary>
/// Defines the contract for audio output including text-to-speech synthesis.
/// Supports Russian and English language speech synthesis.
/// </summary>
public interface IAudioOutput
{
    /// <summary>
    /// Gets or sets a value indicating whether voice responses are enabled.
    /// </summary>
    bool VoiceEnabled { get; set; }

    /// <summary>
    /// Gets or sets the speech rate (range: -10 to 10, 0 is normal).
    /// </summary>
    int SpeechRate { get; set; }

    /// <summary>
    /// Gets or sets the speech volume (range: 0 to 100).
    /// </summary>
    int Volume { get; set; }

    /// <summary>
    /// Gets the available voices on the system.
    /// </summary>
    /// <returns>A task representing the asynchronous operation with the list of available voices.</returns>
    Task<IReadOnlyList<VoiceInfo>> GetAvailableVoicesAsync();

    /// <summary>
    /// Sets the voice to use for speech synthesis.
    /// </summary>
    /// <param name="voiceName">The name of the voice to use.</param>
    void SetVoice(string voiceName);

    /// <summary>
    /// Speaks the specified text using text-to-speech synthesis.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="language">The language of the text (e.g., "ru-RU", "en-US").</param>
    /// <returns>A task representing the asynchronous speech operation.</returns>
    Task SpeakAsync(string text, string? language = null);

    /// <summary>
    /// Stops any currently playing speech.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets a value indicating whether speech is currently playing.
    /// </summary>
    bool IsSpeaking { get; }

    /// <summary>
    /// Event raised when speech playback starts.
    /// </summary>
    event EventHandler? SpeechStarted;

    /// <summary>
    /// Event raised when speech playback completes.
    /// </summary>
    event EventHandler? SpeechCompleted;
}

/// <summary>
/// Represents information about a text-to-speech voice.
/// </summary>
public class VoiceInfo
{
    /// <summary>
    /// Gets or sets the name of the voice.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language/culture of the voice (e.g., "ru-RU", "en-US").
    /// </summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the gender of the voice.
    /// </summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the age of the voice.
    /// </summary>
    public string Age { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a description of the voice.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
