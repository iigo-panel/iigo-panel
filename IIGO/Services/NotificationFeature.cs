namespace IIGO.Services
{
    /// <summary>
    /// Features that send notifications. Stored in NotificationSubscription.Feature, so values must not change.
    /// </summary>
    public static class NotificationFeature
    {
        public const string AppPoolMonitor = "AppPoolMonitor";
        public const string CertificateMonitor = "CertificateMonitor";
    }
}
