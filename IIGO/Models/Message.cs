using System.Reflection;

namespace IIGO.Models
{
    [Obfuscation(Exclude = true)]
    public class MessageData
    {
        public string Subject { get; set; } = "";

        public string Body { get; set; } = "";

        // Only used by email-based messenger services
        public List<string> Recipients { get; set; } = [];
    }
}
