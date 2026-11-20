namespace IIGO.Config
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            // A path may be passed on the command line, which helps when testing against a copy of the file
            var settingsPath = args.Length > 0 ? args[0] : AppSettingsFile.FindDefaultPath();
            Application.Run(new MainForm(settingsPath));
        }
    }
}
