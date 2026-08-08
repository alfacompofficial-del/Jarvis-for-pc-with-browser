using System;

namespace Jarvis.Models
{
    /// <summary>
    /// Represents a single conversation entry in the chat history.
    /// Stores user input, AI response, and execution results.
    /// </summary>
    public class ConversationEntry
    {
        /// <summary>
        /// Unique identifier for this conversation entry.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Timestamp when the conversation entry was created.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The user's input text (voice or text command).
        /// </summary>
        public string UserInput { get; set; }

        /// <summary>
        /// Language of the user input (e.g., "ru-RU" or "en-US").
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// The type of command identified by the AI.
        /// </summary>
        public CommandType CommandType { get; set; }

        /// <summary>
        /// The AI's natural language response to the user.
        /// </summary>
        public string AIResponse { get; set; }

        /// <summary>
        /// Whether the command execution was successful.
        /// </summary>
        public bool ExecutionSuccess { get; set; }

        /// <summary>
        /// Message describing the execution result (success or error).
        /// </summary>
        public string ExecutionMessage { get; set; }

        public ConversationEntry()
        {
            Id = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
        }
    }
}
