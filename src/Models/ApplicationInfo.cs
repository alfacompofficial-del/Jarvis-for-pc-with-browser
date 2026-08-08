using System;

namespace Jarvis.Models
{
    /// <summary>
    /// Represents information about an installed application on the system.
    /// Used for application discovery and launching.
    /// </summary>
    public class ApplicationInfo
    {
        /// <summary>
        /// Display name of the application.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Full path to the application's executable file.
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Path to the application's icon file.
        /// </summary>
        public string IconPath { get; set; }

        /// <summary>
        /// Timestamp of when the application was last launched through Jarvis.
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Number of times the application has been launched through Jarvis.
        /// </summary>
        public int UsageCount { get; set; }

        public ApplicationInfo()
        {
            UsageCount = 0;
            LastUsed = DateTime.MinValue;
        }
    }
}
