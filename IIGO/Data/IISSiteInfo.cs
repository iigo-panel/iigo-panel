namespace IIGO.Data
{
    /// <summary>
    /// A website as returned by the API. Plain values only, so it serializes cleanly
    /// (the Microsoft.Web.Administration objects reference their parents).
    /// </summary>
    public class IISSiteInfo
    {
        public long Id { get; set; }

        public string Name { get; set; } = "";

        public string State { get; set; } = "";

        public string ApplicationPool { get; set; } = "";

        public string PhysicalPath { get; set; } = "";

        public List<IISBindingInfo> Bindings { get; set; } = [];
    }

    public class IISBindingInfo
    {
        public string Protocol { get; set; } = "";

        // "ip:port:host", as stored in applicationHost.config
        public string BindingInformation { get; set; } = "";
    }
}
