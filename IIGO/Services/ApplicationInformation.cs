namespace IIGO.Services
{
    public static class ApplicationInformation
    {
        /// <summary>
        /// Gets the executing assembly.
        /// </summary>
        /// <value>The executing assembly.</value>
        public static System.Reflection.Assembly ExecutingAssembly
        {
            get { return executingAssembly ??= System.Reflection.Assembly.GetExecutingAssembly(); }
        }
        private static System.Reflection.Assembly? executingAssembly;

        /// <summary>
        /// Gets the executing assembly version.
        /// </summary>
        /// <value>The executing assembly version.</value>
        public static Version ExecutingAssemblyVersion
        {
            get { return executingAssemblyVersion ??= ExecutingAssembly.GetName().Version!; }
        }
        private static Version? executingAssemblyVersion;

        /// <summary>
        /// Gets the compile date of the currently executing assembly.
        /// </summary>
        /// <value>The compile date.</value>
        public static DateTime CompileDate
        {
            get
            {
                if (!compileDate.HasValue)
                    compileDate = RetrieveLinkerTimestamp(ExecutingAssembly.Location);
                return compileDate ?? new DateTime();
            }
        }
        private static DateTime? compileDate;

        /// <summary>
        /// Retrieves the linker timestamp.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        /// <remarks>http://www.codinghorror.com/blog/2005/04/determining-build-date-the-hard-way.html</remarks>
        private static DateTime RetrieveLinkerTimestamp(string filePath)
        {
            const int peHeaderOffset = 60;
            const int linkerTimestampOffset = 8;
            var b = new byte[2048];
            FileStream? s = null;
            try
            {
                s = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                s.ReadExactly(b, 0, 2048);
            }
            finally
            {
                s?.Close();
            }
            var time = BitConverter.ToInt32(b, BitConverter.ToInt32(b, peHeaderOffset) + linkerTimestampOffset);
            var dt = DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime;
            //var dt = new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(time);
            return dt.AddHours(TimeZoneInfo.Local.GetUtcOffset(dt).Hours);
        }
    }
}
