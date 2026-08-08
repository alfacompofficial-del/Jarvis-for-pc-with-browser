namespace Jarvis.Models
{
    /// <summary>
    /// Represents a contact with messaging platform information.
    /// Used for sending messages through various platforms.
    /// </summary>
    public class Contact
    {
        /// <summary>
        /// Contact's display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Email address for email messaging.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Phone number for WhatsApp (with country code, e.g., "+79001234567").
        /// </summary>
        public string WhatsAppNumber { get; set; }

        /// <summary>
        /// Telegram username (e.g., "@username").
        /// </summary>
        public string TelegramUsername { get; set; }
    }
}
