using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace IIGO.Services
{
    /// <summary>
    /// Lists, creates, changes, deletes, starts and stops Windows services. Callers check permissions;
    /// this class does none
    /// </summary>
    public class WindowsServiceManager
    {
        // How long Start, Stop and Restart wait for the service to reach the new state
        public static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(30);

        public List<WindowsServiceInfo> GetServices()
        {
            var details = QueryDetails(null);
            var result = new List<WindowsServiceInfo>();
            foreach (var controller in ServiceController.GetServices())
            {
                using (controller)
                {
                    details.TryGetValue(controller.ServiceName, out var detail);
                    result.Add(WindowsServiceInfo.From(controller, detail));
                }
            }
            return [.. result.OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
        }

        public WindowsServiceInfo? GetService(string name)
        {
            using var controller = new ServiceController(name);
            try
            {
                _ = controller.Status;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            QueryDetails(name).TryGetValue(controller.ServiceName, out var detail);
            return WindowsServiceInfo.From(controller, detail);
        }

        public void CreateService(WindowsServiceModel model)
        {
            using var scm = OpenManager(Native.SC_MANAGER_CONNECT | Native.SC_MANAGER_CREATE_SERVICE);
            var (account, password) = model.LogOnCredentials(null);

            using var service = Native.CreateService(scm, model.Name.Trim(), model.DisplayName.Trim(), Native.SERVICE_ALL_ACCESS,
                Native.SERVICE_WIN32_OWN_PROCESS, model.NativeStartType, Native.SERVICE_ERROR_NORMAL, model.BinaryPath.Trim(),
                null, IntPtr.Zero, null, account, password);
            if (service.IsInvalid)
                throw new Win32Exception();

            try
            {
                if (model.Description.Trim() != "")
                    SetDescription(service, model.Description.Trim());
                if (model.StartupType == ServiceStartupType.AutomaticDelayed)
                    SetDelayedStart(service, true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"The service was created, but its settings could not all be saved: {ex.Message}", ex);
            }
        }

        public void UpdateService(WindowsServiceInfo current, WindowsServiceModel model)
        {
            using var scm = OpenManager(Native.SC_MANAGER_CONNECT);
            using var service = OpenService(scm, current.Name, Native.SERVICE_CHANGE_CONFIG | Native.SERVICE_QUERY_CONFIG);

            // Only changed values are sent. Unchanged ones stay null, so a path with %SystemRoot% or a
            // description stored as a resource string isn't replaced by its expanded text
            var displayName = model.DisplayName.Trim();
            var binaryPath = model.BinaryPath.Trim();
            var (account, password) = model.LogOnCredentials(current);

            if (!Native.ChangeServiceConfig(service, Native.SERVICE_NO_CHANGE, model.NativeStartType, Native.SERVICE_NO_CHANGE,
                    binaryPath == current.PathName ? null : binaryPath,
                    null, IntPtr.Zero, null, account, password,
                    displayName == current.DisplayName ? null : displayName))
                throw new Win32Exception();

            var description = model.Description.Trim();
            if (description != current.Description)
                SetDescription(service, description);

            // Delayed start only applies to automatic services; the flag is left alone for the others
            if (model.StartupType is ServiceStartupType.Automatic or ServiceStartupType.AutomaticDelayed)
            {
                var delayed = model.StartupType == ServiceStartupType.AutomaticDelayed;
                if (delayed != current.DelayedStart)
                    SetDelayedStart(service, delayed);
            }
        }

        /// <summary>
        /// Stops the service if it's running, then deletes it
        /// </summary>
        public async Task DeleteServiceAsync(string name)
        {
            using (var controller = new ServiceController(name))
            {
                if (controller.Status != ServiceControllerStatus.Stopped && controller.CanStop)
                    await StopAsync(controller);
            }

            using var scm = OpenManager(Native.SC_MANAGER_CONNECT);
            using var service = OpenService(scm, name, Native.DELETE);
            if (!Native.DeleteService(service))
                throw new Win32Exception();
        }

        public async Task StartServiceAsync(string name)
        {
            using var controller = new ServiceController(name);
            await StartAsync(controller);
        }

        public async Task StopServiceAsync(string name)
        {
            using var controller = new ServiceController(name);
            await StopAsync(controller);
        }

        /// <summary>
        /// Stops the service and the services that depend on it, then starts it and the dependents that were running
        /// </summary>
        public async Task RestartServiceAsync(string name)
        {
            using var controller = new ServiceController(name);
            var dependents = controller.DependentServices;
            var runningDependents = dependents
                .Where(x => x.Status == ServiceControllerStatus.Running)
                .Select(x => x.ServiceName)
                .ToList();
            foreach (var dependent in dependents)
                dependent.Dispose();

            if (controller.Status != ServiceControllerStatus.Stopped)
                await StopAsync(controller);
            await StartAsync(controller);

            foreach (var dependent in runningDependents)
            {
                using var dependentController = new ServiceController(dependent);
                if (dependentController.Status == ServiceControllerStatus.Stopped)
                    await StartAsync(dependentController);
            }
        }

        private static Task StartAsync(ServiceController controller) => Task.Run(() =>
        {
            controller.Start();
            WaitFor(controller, ServiceControllerStatus.Running, "start");
        });

        // Stop() also stops the services that depend on this one
        private static Task StopAsync(ServiceController controller) => Task.Run(() =>
        {
            controller.Stop();
            WaitFor(controller, ServiceControllerStatus.Stopped, "stop");
        });

        private static void WaitFor(ServiceController controller, ServiceControllerStatus status, string verb)
        {
            try
            {
                controller.WaitForStatus(status, StatusTimeout);
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                throw new InvalidOperationException($"{controller.DisplayName} didn't {verb} within {StatusTimeout.TotalSeconds} seconds. It may still be {verb}ping.");
            }
        }

        private static void SetDescription(SafeServiceHandle service, string description)
        {
            var info = new Native.SERVICE_DESCRIPTION { lpDescription = description };
            if (!Native.ChangeServiceConfig2(service, Native.SERVICE_CONFIG_DESCRIPTION, ref info))
                throw new Win32Exception();
        }

        private static void SetDelayedStart(SafeServiceHandle service, bool delayed)
        {
            var info = new Native.SERVICE_DELAYED_AUTO_START_INFO { fDelayedAutostart = delayed };
            if (!Native.ChangeServiceConfig2(service, Native.SERVICE_CONFIG_DELAYED_AUTO_START_INFO, ref info))
                throw new Win32Exception();
        }

        private static SafeServiceHandle OpenManager(uint access)
        {
            var scm = Native.OpenSCManager(null, null, access);
            if (scm.IsInvalid)
                throw new Win32Exception();
            return scm;
        }

        private static SafeServiceHandle OpenService(SafeServiceHandle scm, string name, uint access)
        {
            var service = Native.OpenService(scm, name, access);
            if (service.IsInvalid)
                throw new Win32Exception();
            return service;
        }

        // One WMI query for every service (or the one named) rather than one per service
        private static Dictionary<string, ServiceDetail> QueryDetails(string? name)
        {
            var result = new Dictionary<string, ServiceDetail>(StringComparer.OrdinalIgnoreCase);
            var query = "SELECT Name, Description, StartName, PathName, ProcessId FROM Win32_Service";
            if (name != null)
                query += $" WHERE Name = '{name.Replace("\\", "\\\\").Replace("'", "\\'")}'";

            try
            {
                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        var serviceName = obj["Name"]?.ToString() ?? "";
                        result[serviceName] = new ServiceDetail(
                            obj["Description"]?.ToString() ?? "",
                            obj["StartName"]?.ToString() ?? "",
                            obj["PathName"]?.ToString() ?? "",
                            Convert.ToUInt32(obj["ProcessId"] ?? 0u));
                    }
                }
            }
            catch (ManagementException)
            {
                // Details are shown when available; the list still works without them
            }
            return result;
        }

        internal record ServiceDetail(string Description, string StartName, string PathName, uint ProcessId);

        internal class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeServiceHandle() : base(true) { }

            protected override bool ReleaseHandle() => Native.CloseServiceHandle(handle);
        }

        private static class Native
        {
            public const uint SC_MANAGER_CONNECT = 0x0001;
            public const uint SC_MANAGER_CREATE_SERVICE = 0x0002;
            public const uint SERVICE_QUERY_CONFIG = 0x0001;
            public const uint SERVICE_CHANGE_CONFIG = 0x0002;
            public const uint DELETE = 0x10000;
            public const uint SERVICE_ALL_ACCESS = 0xF01FF;
            public const uint SERVICE_WIN32_OWN_PROCESS = 0x10;
            public const uint SERVICE_AUTO_START = 2;
            public const uint SERVICE_DEMAND_START = 3;
            public const uint SERVICE_DISABLED = 4;
            public const uint SERVICE_ERROR_NORMAL = 1;
            public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
            public const uint SERVICE_CONFIG_DESCRIPTION = 1;
            public const uint SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct SERVICE_DESCRIPTION
            {
                public string? lpDescription;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SERVICE_DELAYED_AUTO_START_INFO
            {
                [MarshalAs(UnmanagedType.Bool)]
                public bool fDelayedAutostart;
            }

            [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeServiceHandle OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

            [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeServiceHandle OpenService(SafeServiceHandle scManager, string serviceName, uint desiredAccess);

            [DllImport("advapi32.dll", EntryPoint = "CreateServiceW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeServiceHandle CreateService(SafeServiceHandle scManager, string serviceName, string displayName,
                uint desiredAccess, uint serviceType, uint startType, uint errorControl, string binaryPathName,
                string? loadOrderGroup, IntPtr tagId, string? dependencies, string? serviceStartName, string? password);

            [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfigW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ChangeServiceConfig(SafeServiceHandle service, uint serviceType, uint startType, uint errorControl,
                string? binaryPathName, string? loadOrderGroup, IntPtr tagId, string? dependencies, string? serviceStartName,
                string? password, string? displayName);

            [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ChangeServiceConfig2(SafeServiceHandle service, uint infoLevel, ref SERVICE_DESCRIPTION info);

            [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ChangeServiceConfig2(SafeServiceHandle service, uint infoLevel, ref SERVICE_DELAYED_AUTO_START_INFO info);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeleteService(SafeServiceHandle service);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseServiceHandle(IntPtr handle);
        }
    }

    public enum ServiceStartupType
    {
        [Display(Name = "Automatic")]
        Automatic,
        [Display(Name = "Automatic (Delayed Start)")]
        AutomaticDelayed,
        [Display(Name = "Manual")]
        Manual,
        [Display(Name = "Disabled")]
        Disabled
    }

    public enum ServiceLogOnType
    {
        LocalSystem,
        LocalService,
        NetworkService,
        Account
    }

    public class WindowsServiceInfo
    {
        public const string LocalSystemAccount = "LocalSystem";
        public const string LocalServiceAccount = @"NT AUTHORITY\LocalService";
        public const string NetworkServiceAccount = @"NT AUTHORITY\NetworkService";

        public string Name { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Description { get; init; } = "";
        public ServiceControllerStatus Status { get; init; }
        public ServiceStartMode StartMode { get; init; }
        public bool DelayedStart { get; init; }
        public string StartName { get; init; } = "";
        public string PathName { get; init; } = "";
        public bool CanStop { get; init; }
        // This panel's own service; stopping it would take the panel down
        public bool IsSelf { get; init; }

        public string StartupText => StartMode == ServiceStartMode.Automatic && DelayedStart
            ? "Automatic (Delayed Start)"
            : StartMode.ToString();

        public string LogOnText => StartName.Replace(@"NT Authority\", "", StringComparison.OrdinalIgnoreCase);

        public ServiceLogOnType LogOnType => StartName switch
        {
            "" => ServiceLogOnType.LocalSystem,
            _ when StartName.Equals(LocalSystemAccount, StringComparison.OrdinalIgnoreCase)
                || StartName.Equals(@".\LocalSystem", StringComparison.OrdinalIgnoreCase) => ServiceLogOnType.LocalSystem,
            _ when StartName.Equals(LocalServiceAccount, StringComparison.OrdinalIgnoreCase) => ServiceLogOnType.LocalService,
            _ when StartName.Equals(NetworkServiceAccount, StringComparison.OrdinalIgnoreCase) => ServiceLogOnType.NetworkService,
            _ => ServiceLogOnType.Account
        };

        public bool IsRunning => Status == ServiceControllerStatus.Running;
        public bool IsStopped => Status == ServiceControllerStatus.Stopped;

        public string SearchText => $"{Name} {DisplayName} {Description} {Status} {StartupText} {LogOnText}".ToLowerInvariant();

        internal static WindowsServiceInfo From(ServiceController controller, WindowsServiceManager.ServiceDetail? detail)
        {
            bool canStop = false;
            try
            {
                canStop = controller.CanStop;
            }
            catch (InvalidOperationException) { }

            return new WindowsServiceInfo
            {
                Name = controller.ServiceName,
                DisplayName = controller.DisplayName,
                Description = detail?.Description ?? "",
                Status = controller.Status,
                StartMode = controller.StartType,
                DelayedStart = controller.StartType == ServiceStartMode.Automatic && GetDelayedStart(controller.ServiceName),
                StartName = detail?.StartName ?? "",
                PathName = detail?.PathName ?? "",
                CanStop = canStop,
                IsSelf = detail != null && detail.ProcessId != 0 && detail.ProcessId == Environment.ProcessId
            };
        }

        private static bool GetDelayedStart(string serviceName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName, false);
                return key?.GetValue("DelayedAutostart") is int value && value == 1;
            }
            catch
            {
                return false;
            }
        }
    }

    public class WindowsServiceModel : IValidatableObject
    {
        [Required(ErrorMessage = "Enter a service name.")]
        [StringLength(256)]
        [RegularExpression(@"^[^/\\]+$", ErrorMessage = "The service name can't contain / or \\.")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Enter a display name.")]
        [StringLength(256)]
        public string DisplayName { get; set; } = "";

        public string Description { get; set; } = "";

        [Required(ErrorMessage = "Enter the path to the program, with any arguments.")]
        public string BinaryPath { get; set; } = "";

        public ServiceStartupType StartupType { get; set; } = ServiceStartupType.Automatic;

        public ServiceLogOnType LogOnType { get; set; } = ServiceLogOnType.LocalSystem;

        public string AccountName { get; set; } = "";

        public string Password { get; set; } = "";

        public string ConfirmPassword { get; set; } = "";

        internal uint NativeStartType => StartupType switch
        {
            ServiceStartupType.Manual => 3,
            ServiceStartupType.Disabled => 4,
            _ => 2
        };

        public static WindowsServiceModel From(WindowsServiceInfo info) => new()
        {
            Name = info.Name,
            DisplayName = info.DisplayName,
            Description = info.Description,
            BinaryPath = info.PathName,
            StartupType = info.StartMode switch
            {
                ServiceStartMode.Automatic => info.DelayedStart ? ServiceStartupType.AutomaticDelayed : ServiceStartupType.Automatic,
                ServiceStartMode.Disabled => ServiceStartupType.Disabled,
                _ => ServiceStartupType.Manual
            },
            LogOnType = info.LogOnType,
            AccountName = info.LogOnType == ServiceLogOnType.Account ? info.StartName : ""
        };

        /// <summary>
        /// The account and password to send to the service manager. Both are null when nothing changed, so the
        /// stored password is kept. A blank password on a new account is sent as empty, which suits virtual
        /// and managed service accounts
        /// </summary>
        internal (string? Account, string? Password) LogOnCredentials(WindowsServiceInfo? current)
        {
            var account = LogOnType switch
            {
                ServiceLogOnType.LocalService => WindowsServiceInfo.LocalServiceAccount,
                ServiceLogOnType.NetworkService => WindowsServiceInfo.NetworkServiceAccount,
                ServiceLogOnType.Account => QualifiedAccountName,
                _ => WindowsServiceInfo.LocalSystemAccount
            };

            if (LogOnType != ServiceLogOnType.Account)
            {
                if (current != null && current.LogOnType == LogOnType)
                    return (null, null);
                return (account, "");
            }

            bool sameAccount = current != null && current.LogOnType == ServiceLogOnType.Account
                && string.Equals(current.StartName, account, StringComparison.OrdinalIgnoreCase);
            if (sameAccount && Password == "")
                return (null, null);
            return (account, Password);
        }

        // A bare user name is a local account, which the service manager wants as .\name
        private string QualifiedAccountName
        {
            get
            {
                var name = AccountName.Trim();
                return name.Contains('\\') || name.Contains('@') ? name : @".\" + name;
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (LogOnType == ServiceLogOnType.Account)
            {
                if (string.IsNullOrWhiteSpace(AccountName))
                    yield return new ValidationResult("Enter an account name.", [nameof(AccountName)]);
                if (Password != ConfirmPassword)
                    yield return new ValidationResult("The passwords don't match.", [nameof(ConfirmPassword)]);
            }
        }
    }
}
