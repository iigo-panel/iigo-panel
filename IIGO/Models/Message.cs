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

        // The plain text body as HTML, for email services
        public string HtmlBody => "<p>" + System.Net.WebUtility.HtmlEncode(Body).Replace("\n", "<br>") + "</p>";
    }
}
