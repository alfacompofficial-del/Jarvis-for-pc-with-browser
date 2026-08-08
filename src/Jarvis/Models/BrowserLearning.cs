using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Models
{
    // Модель для обучения Jarvis
    public class BrowserLearning
    {
        public Dictionary<string, int> SiteVisits { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, string> CommandAliases { get; set; } = new Dictionary<string, string>();
        public Dictionary<int, List<string>> HourlyPatterns { get; set; } = new Dictionary<int, List<string>>();
        public List<FormData> SavedFormData { get; set; } = new List<FormData>();
        public UserPreferences Preferences { get; set; } = new UserPreferences();
    }

    public class FormData
    {
        public string Domain { get; set; } = "";
        public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
    }

    public class UserPreferences
    {
        public bool AutoDarkMode { get; set; } = false;
        public List<string> DarkModeSites { get; set; } = new List<string>();
        public List<string> BlockedDomains { get; set; } = new List<string>();
        public string PreferredSearchEngine { get; set; } = "google";
    }
}
