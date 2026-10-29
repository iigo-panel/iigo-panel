// Copyright (c) Lex Li. All rights reserved.
// 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Web.Administration
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    /// Server manager for IIS.
    /// </summary>
    public sealed class IisServerManager : ServerManager
    {
        public override bool SupportsSni => Environment.OSVersion.Version >= Version.Parse("6.2");
        public override bool SupportsWildcard => Environment.OSVersion.Version >= Version.Parse("10.0");

        public IisServerManager()
            : this(null, true)
        {
        }

        public IisServerManager(string hostName, bool local)
            : this(hostName)
        {
        }

        public IisServerManager(bool readOnly, string applicationHostConfigurationPath)
            : this("localhost", readOnly, applicationHostConfigurationPath)
        {
        }

        public IisServerManager(string applicationHostConfigurationPath)
            : this(false, applicationHostConfigurationPath)
        {
        }

        internal IisServerManager(string hostName, bool readOnly, string fileName)
            : base(hostName, readOnly, fileName)
        {
            Mode = WorkingMode.Iis;
        }

        internal override bool GetSiteState(Site site) => IsStarted(IisStateReader.Sites, site.Name);

        internal override bool GetPoolState(ApplicationPool pool) => IsStarted(IisStateReader.AppPools, pool.Name);

        // One cached appcmd call covers every object, instead of one appcmd call per object
        private static bool IsStarted(string objectType, string name)
        {
            try
            {
                return IisStateReader.GetStates(objectType).TryGetValue(name, out var state) && state == "Started";
            }
            catch (Exception)
            {
                // Same as before: report stopped when appcmd is missing or fails
                return false;
            }
        }

        internal override void Start(Site site)
        {
            var appcmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "appcmd.exe");
            if (File.Exists(appcmd))
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = appcmd,
                        ArgumentList = { "start", "site", site.Name },
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Verb = "runas",
                        UseShellExecute = true
                    }
                };
                process.Start();
                process.WaitForExit();
                IisStateReader.Invalidate();
            }
        }

        internal override void Stop(Site site)
        {
            var appcmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "appcmd.exe");
            if (File.Exists(appcmd))
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = appcmd,
                        ArgumentList = { "stop", "site", site.Name },
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Verb = "runas",
                        UseShellExecute = true
                    }
                };
                process.Start();
                process.WaitForExit();
                IisStateReader.Invalidate();
            }
        }

        internal override void Start(ApplicationPool pool)
        {
            var appcmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "appcmd.exe");
            if (File.Exists(appcmd))
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = appcmd,
                        ArgumentList = { "start", "apppool", pool.Name },
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Verb = "runas",
                        UseShellExecute = true
                    }
                };
                process.Start();
                process.WaitForExit();
                IisStateReader.Invalidate();
            }
        }

        internal override void Stop(ApplicationPool pool)
        {
            var appcmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "appcmd.exe");
            if (File.Exists(appcmd))
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = appcmd,
                        ArgumentList = { "stop", "apppool", pool.Name },
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Verb = "runas",
                        UseShellExecute = true
                    }
                };
                process.Start();
                process.WaitForExit();
                IisStateReader.Invalidate();
            }
        }

        internal override void Recycle(ApplicationPool pool)
        {
            var appcmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "appcmd.exe");
            if (File.Exists(appcmd))
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = appcmd,
                        ArgumentList = { "recycle", "apppool", pool.Name },
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Verb = "runas",
                        UseShellExecute = true
                    }
                };
                process.Start();
                process.WaitForExit();
                IisStateReader.Invalidate();
            }
        }

        internal override IEnumerable<string> GetSchemaFiles()
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "inetsrv",
                "config",
                "schema");
            return Directory.Exists(directory) ? Directory.GetFiles(directory) : base.GetSchemaFiles();
        }
    }
}
