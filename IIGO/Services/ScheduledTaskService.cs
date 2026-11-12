using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json.Serialization;
using Microsoft.Win32.TaskScheduler;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace IIGO.Services
{
    /// <summary>
    /// Tasks in the root folder of Task Scheduler. Windows' own tasks live in subfolders and are left alone
    /// </summary>
    public class ScheduledTaskService
    {
        private static TaskFolder Root => TaskService.Instance.RootFolder;

        public List<Task> GetTasks() => [.. Root.Tasks.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];

        public Task? GetTask(string name) =>
            Root.Tasks.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        public void Run(Task task) => task.Run();

        public void Stop(Task task) => task.Stop();

        public void SetEnabled(Task task, bool enabled) => task.Enabled = enabled;

        public void Delete(Task task) => Root.DeleteTask(task.Name);

        /// <summary>
        /// Why the form can't edit this task, or null when it can. The form covers the common triggers, program
        /// actions and run-as accounts; saving anything else through it would drop what it can't show
        /// </summary>
        public static string? NotEditableReason(Task task)
        {
            var definition = task.Definition;
            var unsupportedTrigger = definition.Triggers.FirstOrDefault(x => TriggerKindOf(x) == null);
            if (unsupportedTrigger != null)
                return $"It has a trigger type ({unsupportedTrigger.TriggerType}) that can only be changed in Task Scheduler.";
            if (definition.Actions.Any(x => x is not ExecAction))
                return "It has an action other than starting a program, which can only be changed in Task Scheduler.";
            if (RunAsOf(definition.Principal) == null)
                return $"It runs as {PrincipalText(definition.Principal)}, which can only be changed in Task Scheduler.";
            return null;
        }

        /// <summary>
        /// Adds a task, or saves changes to the task named <paramref name="originalName"/>. A new name registers the
        /// task under that name and then deletes the old one, since Task Scheduler can't rename
        /// </summary>
        public Task Save(ScheduledTaskModel model, string? originalName, string author)
        {
            var name = model.Name.Trim();
            var existing = originalName == null ? null : GetTask(originalName) ?? throw new ArgumentException($"{originalName} no longer exists.");
            var renamed = existing != null && !string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase);

            if ((existing == null || renamed) && GetTask(name) != null)
                throw new ArgumentException($"A task named {name} already exists.");
            if (existing != null && NotEditableReason(existing) is string reason)
                throw new InvalidOperationException(reason);

            TaskDefinition definition;
            if (existing == null)
            {
                definition = TaskService.Instance.NewTask();
                definition.RegistrationInfo.Author = author;
                // Servers have no battery, but the defaults would still skip runs on a UPS
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;
            }
            else
            {
                definition = existing.Definition;
            }

            definition.RegistrationInfo.Description = model.Description.Trim();
            definition.Settings.Enabled = model.Enabled;
            definition.Settings.ExecutionTimeLimit = model.StopAfter;
            definition.Settings.MultipleInstances = model.MultipleInstances;
            definition.Settings.StartWhenAvailable = model.StartWhenAvailable;
            definition.Principal.RunLevel = model.HighestPrivileges ? TaskRunLevel.Highest : TaskRunLevel.LUA;

            // Build from copies first: clearing a collection releases the triggers and actions it held.
            // Copying the originals keeps settings the form doesn't show, such as random delays
            var triggers = model.Triggers.Select(BuildTrigger).ToList();
            var actions = model.Actions.Select(BuildAction).ToList();
            definition.Triggers.Clear();
            foreach (var trigger in triggers)
                definition.Triggers.Add(trigger);
            definition.Actions.Clear();
            foreach (var action in actions)
                definition.Actions.Add(action);

            var (userId, password, logonType) = model.RunAs switch
            {
                TaskRunAs.System => ("SYSTEM", null, TaskLogonType.ServiceAccount),
                TaskRunAs.LocalService => (@"NT AUTHORITY\LOCALSERVICE", null, TaskLogonType.ServiceAccount),
                TaskRunAs.NetworkService => (@"NT AUTHORITY\NETWORKSERVICE", null, TaskLogonType.ServiceAccount),
                _ => (model.UserName.Trim(), model.UserLogon == TaskUserLogon.Password ? model.Password : null, ToLogonType(model.UserLogon))
            };

            Task saved;
            try
            {
                saved = Root.RegisterTaskDefinition(name, definition, TaskCreation.CreateOrUpdate, userId, password, logonType);
            }
            // Windows' messages for these name the problem but not the field
            catch (COMException ex) when ((uint)ex.HResult is 0x8007052E or 0x80070534)
            {
                throw new ArgumentException((uint)ex.HResult == 0x8007052E
                    ? $"Run as: the user name or password for {userId} is wrong."
                    : $"Run as: no account named {userId} was found.", ex);
            }
            if (renamed)
                Root.DeleteTask(existing!.Name, false);
            return saved;
        }

        private static Trigger BuildTrigger(TaskTriggerModel model)
        {
            Trigger trigger = model.Original != null && TriggerKindOf(model.Original) == model.Kind
                ? (Trigger)model.Original.Clone()
                : model.Kind switch
                {
                    TaskTriggerKind.Daily => new DailyTrigger(),
                    TaskTriggerKind.Weekly => new WeeklyTrigger(),
                    TaskTriggerKind.Monthly => new MonthlyTrigger(),
                    TaskTriggerKind.AtStartup => new BootTrigger(),
                    TaskTriggerKind.AtLogon => new LogonTrigger(),
                    _ => new TimeTrigger()
                };

            trigger.Enabled = model.Enabled;
            switch (trigger)
            {
                case DailyTrigger daily:
                    daily.DaysInterval = model.DaysInterval;
                    break;
                case WeeklyTrigger weekly:
                    weekly.WeeksInterval = model.WeeksInterval;
                    weekly.DaysOfWeek = model.DaysOfWeek;
                    break;
                case MonthlyTrigger monthly:
                    monthly.MonthsOfYear = model.Months;
                    monthly.DaysOfMonth = model.ParsedDaysOfMonth;
                    monthly.RunOnLastDayOfMonth = model.LastDayOfMonth;
                    break;
                case BootTrigger boot:
                    boot.Delay = model.Delay;
                    break;
                case LogonTrigger logon:
                    logon.UserId = string.IsNullOrWhiteSpace(model.LogonUser) ? null : model.LogonUser.Trim();
                    logon.Delay = model.Delay;
                    break;
            }

            if (model.IsScheduled)
                trigger.StartBoundary = model.Start;
            trigger.EndBoundary = model.HasExpiry ? model.Expires : DateTime.MaxValue;

            // Clear the duration first; each setter checks itself against the other
            trigger.Repetition.Duration = TimeSpan.Zero;
            trigger.Repetition.Interval = model.RepeatInterval;
            trigger.Repetition.Duration = model.RepeatInterval == TimeSpan.Zero ? TimeSpan.Zero : model.RepeatDuration;
            return trigger;
        }

        private static ExecAction BuildAction(TaskActionModel model)
        {
            var action = model.Original != null ? (ExecAction)model.Original.Clone() : new ExecAction();
            action.Path = model.Program.Trim();
            action.Arguments = Blank(model.Arguments);
            action.WorkingDirectory = Blank(model.StartIn);
            return action;
        }

        private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static TaskTriggerKind? TriggerKindOf(Trigger trigger) => trigger switch
        {
            TimeTrigger => TaskTriggerKind.Once,
            DailyTrigger => TaskTriggerKind.Daily,
            WeeklyTrigger => TaskTriggerKind.Weekly,
            MonthlyTrigger => TaskTriggerKind.Monthly,
            BootTrigger => TaskTriggerKind.AtStartup,
            LogonTrigger => TaskTriggerKind.AtLogon,
            _ => null
        };

        /// <summary>
        /// The form's run-as choice for a principal, or null when the form can't show it
        /// </summary>
        public static TaskRunAs? RunAsOf(TaskPrincipal principal)
        {
            if (!string.IsNullOrEmpty(principal.GroupId))
                return null;
            if (ServiceAccountOf(principal.UserId) is TaskRunAs account)
                return account;
            return principal.LogonType is TaskLogonType.Password or TaskLogonType.S4U or TaskLogonType.InteractiveToken
                && !string.IsNullOrWhiteSpace(principal.UserId) ? TaskRunAs.User : null;
        }

        // Tasks name these accounts many ways: SYSTEM, NT AUTHORITY\SYSTEM, S-1-5-18
        private static TaskRunAs? ServiceAccountOf(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;
            try
            {
                var sid = userId.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase)
                    ? new SecurityIdentifier(userId)
                    : (SecurityIdentifier)new NTAccount(userId).Translate(typeof(SecurityIdentifier));
                if (sid.IsWellKnown(WellKnownSidType.LocalSystemSid))
                    return TaskRunAs.System;
                if (sid.IsWellKnown(WellKnownSidType.LocalServiceSid))
                    return TaskRunAs.LocalService;
                if (sid.IsWellKnown(WellKnownSidType.NetworkServiceSid))
                    return TaskRunAs.NetworkService;
            }
            catch (Exception)
            {
                // An account that no longer exists, or a name that doesn't resolve
            }
            return null;
        }

        public static string PrincipalText(TaskPrincipal principal) =>
            !string.IsNullOrEmpty(principal.GroupId) ? $"the {principal.GroupId} group"
            : !string.IsNullOrEmpty(principal.UserId) ? principal.UserId
            : "an unknown account";

        public static string LogonText(TaskLogonType logonType) => logonType switch
        {
            TaskLogonType.Password => "Whether the user is signed in or not (password stored)",
            TaskLogonType.S4U => "Whether the user is signed in or not (no password stored, no network access)",
            TaskLogonType.InteractiveToken => "Only when the user is signed in",
            TaskLogonType.ServiceAccount => "Service account",
            TaskLogonType.Group => "When a member of the group is signed in",
            TaskLogonType.InteractiveTokenOrPassword => "Only when the user is signed in, or with a stored password",
            _ => logonType.ToString()
        };

        private static TaskLogonType ToLogonType(TaskUserLogon logon) => logon switch
        {
            TaskUserLogon.S4U => TaskLogonType.S4U,
            TaskUserLogon.InteractiveToken => TaskLogonType.InteractiveToken,
            _ => TaskLogonType.Password
        };

        /// <summary>
        /// Task Scheduler's own status codes, which it reports in place of an exit code. Anything else is the
        /// program's exit code or an error code
        /// </summary>
        public static string ResultText(int result) => unchecked((uint)result) switch
        {
            0 => "Completed successfully (0x0)",
            0x41300 => "Ready to run (0x41300)",
            0x41301 => "Running (0x41301)",
            0x41302 => "Disabled (0x41302)",
            0x41303 => "Has not run yet (0x41303)",
            0x41304 => "No more scheduled runs (0x41304)",
            0x41306 => "Ended by a user (0x41306)",
            0x8004131F => "Skipped: already running (0x8004131F)",
            0x800710E0 => "Refused: check the run-as account (0x800710E0)",
            0x80070002 => "Program not found (0x80070002)",
            0x80070005 => "Access denied (0x80070005)",
            0xC000013A => "Closed by Ctrl+C (0xC000013A)",
            var code => $"Exit code {result} (0x{code:X})"
        };

        public static string ResultStatus(int result) => unchecked((uint)result) switch
        {
            0 => "Succeeded",
            0x41301 => "Running",
            0x41300 or 0x41302 or 0x41303 or 0x41304 or 0x41306 => "Other",
            _ => "Failed"
        };

        // Tasks that never ran report 30 Nov 1999 or DateTime.MinValue
        public static string RunTimeText(DateTime value, string none) => value.Year < 2000 ? none : value.ToString("g");
    }

    public enum TaskTriggerKind { Once, Daily, Weekly, Monthly, AtStartup, AtLogon }

    public enum TaskRunAs { System, LocalService, NetworkService, User }

    public enum TaskUserLogon { Password, S4U, InteractiveToken }

    public class ScheduledTaskModel : IValidatableObject
    {
        // Task Scheduler's limit on the full path, less the leading backslash
        [Required(ErrorMessage = "Enter a name.")]
        [StringLength(238)]
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public bool Enabled { get; set; } = true;

        public TaskRunAs RunAs { get; set; } = TaskRunAs.System;

        public string UserName { get; set; } = "";

        public string Password { get; set; } = "";

        public TaskUserLogon UserLogon { get; set; } = TaskUserLogon.Password;

        public bool HighestPrivileges { get; set; }

        // TimeSpan.Zero means no limit. Windows' default is 3 days
        public TimeSpan StopAfter { get; set; } = TimeSpan.FromDays(3);

        public TaskInstancesPolicy MultipleInstances { get; set; } = TaskInstancesPolicy.IgnoreNew;

        public bool StartWhenAvailable { get; set; }

        public List<TaskActionModel> Actions { get; set; } = [new()];

        public List<TaskTriggerModel> Triggers { get; set; } = [new()];

        public static ScheduledTaskModel From(Task task)
        {
            var definition = task.Definition;
            var principal = definition.Principal;
            var runAs = ScheduledTaskService.RunAsOf(principal) ?? TaskRunAs.System;
            return new ScheduledTaskModel
            {
                Name = task.Name,
                Description = definition.RegistrationInfo.Description ?? "",
                Enabled = definition.Settings.Enabled,
                RunAs = runAs,
                UserName = runAs == TaskRunAs.User ? principal.UserId ?? "" : "",
                UserLogon = principal.LogonType switch
                {
                    TaskLogonType.S4U => TaskUserLogon.S4U,
                    TaskLogonType.InteractiveToken => TaskUserLogon.InteractiveToken,
                    _ => TaskUserLogon.Password
                },
                HighestPrivileges = principal.RunLevel == TaskRunLevel.Highest,
                StopAfter = definition.Settings.ExecutionTimeLimit,
                MultipleInstances = definition.Settings.MultipleInstances,
                StartWhenAvailable = definition.Settings.StartWhenAvailable,
                Actions = [.. definition.Actions.OfType<ExecAction>().Select(TaskActionModel.From)],
                Triggers = [.. definition.Triggers.Select(TaskTriggerModel.From)]
            };
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) >= 0)
                yield return new ValidationResult("A name can't contain \\ / : * ? \" < > |", [nameof(Name)]);

            if (RunAs == TaskRunAs.User)
            {
                if (string.IsNullOrWhiteSpace(UserName))
                    yield return new ValidationResult("Enter a user name.", [nameof(UserName)]);
                if (UserLogon == TaskUserLogon.Password && string.IsNullOrEmpty(Password))
                    yield return new ValidationResult("Enter the account's password. Windows asks for it every time the task is saved.", [nameof(Password)]);
            }

            if (StopAfter < TimeSpan.Zero)
                yield return new ValidationResult("Enter 0 or more.", [nameof(StopAfter)]);

            if (Actions.Count == 0)
                yield return new ValidationResult("Add at least one program to run.", [nameof(Actions)]);
            for (int i = 0; i < Actions.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(Actions[i].Program))
                    yield return new ValidationResult($"Action {i + 1}: enter a program.", [nameof(Actions)]);
            }

            for (int i = 0; i < Triggers.Count; i++)
            {
                foreach (var error in Triggers[i].Validate())
                    yield return new ValidationResult($"Trigger {i + 1}: {error}", [nameof(Triggers)]);
            }
        }
    }

    public class TaskActionModel
    {
        public string Program { get; set; } = "";

        public string Arguments { get; set; } = "";

        public string StartIn { get; set; } = "";

        // The action this came from, so saving keeps what the form doesn't show
        [JsonIgnore]
        public ExecAction? Original { get; set; }

        public static TaskActionModel From(ExecAction action) => new()
        {
            Program = action.Path ?? "",
            Arguments = action.Arguments ?? "",
            StartIn = action.WorkingDirectory ?? "",
            Original = action
        };
    }

    public class TaskTriggerModel
    {
        public TaskTriggerKind Kind { get; set; } = TaskTriggerKind.Daily;

        public bool Enabled { get; set; } = true;

        // Next whole hour, so a new daily trigger starts at a sensible time
        public DateTime Start { get; set; } = DateTime.Today.AddHours(DateTime.Now.Hour + 1);

        public short DaysInterval { get; set; } = 1;

        public short WeeksInterval { get; set; } = 1;

        public DaysOfTheWeek DaysOfWeek { get; set; } = DaysOfTheWeek.Monday;

        public MonthsOfTheYear Months { get; set; } = MonthsOfTheYear.AllMonths;

        public string DaysOfMonth { get; set; } = "1";

        public bool LastDayOfMonth { get; set; }

        // Blank for any user
        public string LogonUser { get; set; } = "";

        public TimeSpan Delay { get; set; }

        // TimeSpan.Zero means the trigger doesn't repeat
        public TimeSpan RepeatInterval { get; set; }

        // TimeSpan.Zero means it repeats indefinitely
        public TimeSpan RepeatDuration { get; set; }

        public bool HasExpiry { get; set; }

        public DateTime Expires { get; set; } = DateTime.Today.AddYears(1);

        // The trigger this came from, so saving keeps what the form doesn't show
        [JsonIgnore]
        public Trigger? Original { get; set; }

        public bool IsScheduled => Kind is TaskTriggerKind.Once or TaskTriggerKind.Daily or TaskTriggerKind.Weekly or TaskTriggerKind.Monthly;

        public bool HasDelay => Kind is TaskTriggerKind.AtStartup or TaskTriggerKind.AtLogon;

        public int[] ParsedDaysOfMonth =>
            [.. DaysOfMonth.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var day) ? day : 0).Distinct().Order()];

        public static TaskTriggerModel From(Trigger trigger)
        {
            var model = new TaskTriggerModel
            {
                Kind = ScheduledTaskService.TriggerKindOf(trigger) ?? TaskTriggerKind.Once,
                Enabled = trigger.Enabled,
                Start = trigger.StartBoundary,
                RepeatInterval = trigger.Repetition.Interval,
                RepeatDuration = trigger.Repetition.Duration,
                HasExpiry = trigger.EndBoundary != DateTime.MaxValue,
                Expires = trigger.EndBoundary != DateTime.MaxValue ? trigger.EndBoundary : DateTime.Today.AddYears(1),
                Original = trigger
            };
            switch (trigger)
            {
                case DailyTrigger daily:
                    model.DaysInterval = daily.DaysInterval;
                    break;
                case WeeklyTrigger weekly:
                    model.WeeksInterval = weekly.WeeksInterval;
                    model.DaysOfWeek = weekly.DaysOfWeek;
                    break;
                case MonthlyTrigger monthly:
                    model.Months = monthly.MonthsOfYear;
                    model.DaysOfMonth = string.Join(", ", monthly.DaysOfMonth);
                    model.LastDayOfMonth = monthly.RunOnLastDayOfMonth;
                    break;
                case BootTrigger boot:
                    model.Delay = boot.Delay;
                    break;
                case LogonTrigger logon:
                    model.LogonUser = logon.UserId ?? "";
                    model.Delay = logon.Delay;
                    break;
            }
            return model;
        }

        public IEnumerable<string> Validate()
        {
            if (Kind == TaskTriggerKind.Daily && (DaysInterval < 1 || DaysInterval > 365))
                yield return "repeat every 1 to 365 days.";
            if (Kind == TaskTriggerKind.Weekly)
            {
                if (WeeksInterval < 1 || WeeksInterval > 52)
                    yield return "repeat every 1 to 52 weeks.";
                if (DaysOfWeek == 0)
                    yield return "pick at least one day of the week.";
            }
            if (Kind == TaskTriggerKind.Monthly)
            {
                if (Months == 0)
                    yield return "pick at least one month.";
                var days = ParsedDaysOfMonth;
                if (days.Any(x => x < 1 || x > 31))
                    yield return "days of the month must be numbers from 1 to 31.";
                else if (days.Length == 0 && !LastDayOfMonth)
                    yield return "enter at least one day of the month, or pick the last day.";
            }
            if (Delay < TimeSpan.Zero)
                yield return "the delay can't be negative.";
            if (RepeatInterval != TimeSpan.Zero && (RepeatInterval < TimeSpan.FromMinutes(1) || RepeatInterval > TimeSpan.FromDays(31)))
                yield return "repeat every 1 minute to 31 days.";
            if (RepeatInterval != TimeSpan.Zero && RepeatDuration != TimeSpan.Zero && RepeatDuration < RepeatInterval)
                yield return "the repeat duration must be longer than the repeat interval.";
            if (HasExpiry && IsScheduled && Expires <= Start)
                yield return "the expiry must be after the start.";
        }
    }
}
