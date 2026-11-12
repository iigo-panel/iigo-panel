namespace IIGO.Plugins
{
    /// <summary>
    /// A sidebar link added by a plugin
    /// </summary>
    /// <param name="Section">
    /// Sidebar section: one of <see cref="NavSections"/>, or any other name to start a new section below Administration
    /// </param>
    /// <param name="Title">Link text</param>
    /// <param name="Href">Page address, such as "/backups"</param>
    /// <param name="Icon">Bootstrap Icons class, such as "bi bi-archive"</param>
    /// <param name="Policy">Authorization policy the user needs, usually one of the plugin's features</param>
    /// <param name="Roles">Comma-separated roles, one of which the user needs</param>
    /// <param name="Order">Position within the section; plugin links follow the panel's own links</param>
    public record NavMenuItem(
        string Section,
        string Title,
        string Href,
        string Icon = "bi bi-puzzle",
        string? Policy = null,
        string? Roles = null,
        int Order = 100);

    /// <summary>
    /// The panel's own sidebar sections
    /// </summary>
    public static class NavSections
    {
        public const string WebServer = "Web server";
        public const string System = "System";
        public const string Administration = "Administration";

        public static readonly IReadOnlyList<string> All = [WebServer, System, Administration];
    }
}
