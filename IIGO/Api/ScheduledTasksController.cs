using IIGO.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32.TaskScheduler;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace IIGO.Api
{
    /// <summary>
    /// Tasks in the root folder of Task Scheduler: the same actions as the Scheduled Tasks pages.
    /// </summary>
    /// <remarks>
    /// Trigger start and expiry times are the server's local time with no offset, because a schedule such as
    /// "daily at 02:00" follows the server's clock. A time sent with an offset or Z is converted to the server's time.
    /// Last and next run times are UTC, like the rest of the API.
    /// </remarks>
    [Route("api/tasks")]
    public class ScheduledTasksController(ILogger<ScheduledTasksController> logger, ScheduledTaskService taskService) : ApiControllerBase(logger)
    {
        /// <summary>Lists tasks. Every word in <paramref name="search"/> has to match the name, description, status, a trigger or a program.</summary>
        [HttpGet]
        public ActionResult<List<ScheduledTaskDto>> GetTasks([FromQuery] string? search = null)
        {
            try
            {
                var terms = (search ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return taskService.GetTasks()
                    .Select(ScheduledTaskDto.From)
                    .Where(task => terms.All(task.SearchText().Contains))
                    .ToList();
            }
            catch (Exception ex)
            {
                return Failed(ex, "loading scheduled tasks");
            }
        }

        [HttpGet("{name}")]
        public ActionResult<ScheduledTaskDto> GetTask(string name)
        {
            try
            {
                var task = taskService.GetTask(name);
                return task == null ? TaskNotFound(name) : ScheduledTaskDto.From(task);
            }
            catch (Exception ex)
            {
                return Failed(ex, $"loading scheduled task {name}");
            }
        }

        [HttpPost]
        public ActionResult<ScheduledTaskDto> AddTask([FromBody] ScheduledTaskModel model)
        {
            var name = model.Name.Trim();
            try
            {
                ToServerTime(model);
                var saved = taskService.Save(model, null, $"API key {KeyName}");
                Logger.LogWarning("API key {KeyName} added scheduled task {Name} running as {RunAs}", KeyName, saved.Name, RunAsText(model));
                return StatusCode(StatusCodes.Status201Created, ScheduledTaskDto.From(saved));
            }
            catch (Exception ex)
            {
                return Failed(ex, $"adding scheduled task {name}");
            }
        }

        /// <summary>
        /// Replaces every setting of the task with the body. A different name in the body renames the task. Triggers and
        /// actions are matched to the current ones by position, so settings the API doesn't show, such as random delays, are kept.
        /// Tasks with "editable": false can only be run, stopped, enabled, disabled or deleted.
        /// </summary>
        [HttpPut("{name}")]
        public ActionResult<ScheduledTaskDto> UpdateTask(string name, [FromBody] ScheduledTaskModel model)
        {
            try
            {
                var task = taskService.GetTask(name);
                if (task == null)
                    return TaskNotFound(name);

                var current = ScheduledTaskModel.From(task);
                for (int i = 0; i < model.Triggers.Count && i < current.Triggers.Count; i++)
                    model.Triggers[i].Original = current.Triggers[i].Original;
                for (int i = 0; i < model.Actions.Count && i < current.Actions.Count; i++)
                    model.Actions[i].Original = current.Actions[i].Original;

                ToServerTime(model);
                var saved = taskService.Save(model, task.Name, $"API key {KeyName}");
                Logger.LogWarning("API key {KeyName} changed scheduled task {Name} ({NewName}) running as {RunAs}", KeyName, task.Name, saved.Name, RunAsText(model));
                return ScheduledTaskDto.From(saved);
            }
            catch (Exception ex)
            {
                return Failed(ex, $"changing scheduled task {name}");
            }
        }

        [HttpDelete("{name}")]
        public IActionResult DeleteTask(string name) => Change(name, "deleted", taskService.Delete, returnTask: false);

        /// <summary>Starts the task now. A disabled task gets 409 Conflict.</summary>
        [HttpPost("{name}/run")]
        public IActionResult RunTask(string name) => Change(name, "ran", task =>
        {
            if (!task.Enabled)
                throw new InvalidOperationException($"{task.Name} is disabled. Enable it first.");
            taskService.Run(task);
        });

        /// <summary>Ends the running instances of the task.</summary>
        [HttpPost("{name}/stop")]
        public IActionResult StopTask(string name) => Change(name, "stopped", taskService.Stop);

        [HttpPost("{name}/enable")]
        public IActionResult EnableTask(string name) => Change(name, "enabled", task => taskService.SetEnabled(task, true));

        [HttpPost("{name}/disable")]
        public IActionResult DisableTask(string name) => Change(name, "disabled", task => taskService.SetEnabled(task, false));

        private IActionResult Change(string name, string verb, Action<Task> change, bool returnTask = true)
        {
            try
            {
                var task = taskService.GetTask(name);
                if (task == null)
                    return TaskNotFound(name);

                change(task);
                Logger.LogWarning("API key {KeyName} {Verb} scheduled task {Name}", KeyName, verb, task.Name);
                if (!returnTask)
                    return NoContent();

                var updated = taskService.GetTask(task.Name);
                return updated == null ? NoContent() : Ok(ScheduledTaskDto.From(updated));
            }
            catch (Exception ex)
            {
                return Failed(ex, $"{verb} scheduled task {name}");
            }
        }

        private ObjectResult TaskNotFound(string name) => NotFoundProblem($"No scheduled task named {name} was found in the root folder");

        private static string RunAsText(ScheduledTaskModel model) => model.RunAs == TaskRunAs.User ? model.UserName.Trim() : model.RunAs.ToString();

        // Schedules follow the server's clock, so store them as its local time with no zone, as the page does
        private static void ToServerTime(ScheduledTaskModel model)
        {
            foreach (var trigger in model.Triggers)
            {
                trigger.Start = ServerTime(trigger.Start);
                trigger.Expires = ServerTime(trigger.Expires);
            }
        }

        private static DateTime ServerTime(DateTime value) =>
            DateTime.SpecifyKind(value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value, DateTimeKind.Unspecified);

        private static DateTime? Utc(DateTime value) => value.Year < 2000 ? null : value.ToUniversalTime();

        // Same field names as the request body, so a task can be read, changed and sent back.
        // Password is never returned; send it again when saving a task that stores one
        public record ScheduledTaskDto(
            string Name,
            string Path,
            string Description,
            string Author,
            DateTime? Created,
            bool Enabled,
            TaskState State,
            TaskRunAs? RunAs,
            string UserName,
            TaskUserLogon UserLogon,
            TaskLogonType LogonType,
            bool HighestPrivileges,
            TimeSpan StopAfter,
            TaskInstancesPolicy MultipleInstances,
            bool StartWhenAvailable,
            List<ScheduledTaskActionDto> Actions,
            List<ScheduledTaskTriggerDto> Triggers,
            DateTime? LastRunTime,
            int LastResult,
            string LastResultText,
            DateTime? NextRunTime,
            int MissedRuns,
            bool Editable,
            string? NotEditableReason)
        {
            public static ScheduledTaskDto From(Task task)
            {
                var definition = task.Definition;
                var principal = definition.Principal;
                var settings = definition.Settings;
                var runAs = ScheduledTaskService.RunAsOf(principal);
                var reason = ScheduledTaskService.NotEditableReason(task);
                return new ScheduledTaskDto(
                    task.Name,
                    task.Path,
                    definition.RegistrationInfo.Description ?? "",
                    definition.RegistrationInfo.Author ?? "",
                    Utc(definition.RegistrationInfo.Date),
                    task.Enabled,
                    task.State,
                    runAs,
                    runAs == TaskRunAs.User ? principal.UserId ?? "" : "",
                    principal.LogonType switch
                    {
                        TaskLogonType.S4U => TaskUserLogon.S4U,
                        TaskLogonType.InteractiveToken => TaskUserLogon.InteractiveToken,
                        _ => TaskUserLogon.Password
                    },
                    principal.LogonType,
                    principal.RunLevel == TaskRunLevel.Highest,
                    settings.ExecutionTimeLimit,
                    settings.MultipleInstances,
                    settings.StartWhenAvailable,
                    [.. definition.Actions.Select(ScheduledTaskActionDto.From)],
                    [.. definition.Triggers.Select(ScheduledTaskTriggerDto.From)],
                    Utc(task.LastRunTime),
                    task.LastTaskResult,
                    ScheduledTaskService.ResultText(task.LastTaskResult),
                    Utc(task.NextRunTime),
                    task.NumberOfMissedRuns,
                    reason == null,
                    reason);
            }

            public string SearchText() => string.Join(" ",
                [Name, Description, State.ToString(), Enabled ? "enabled" : "disabled",
                 .. Triggers.Select(x => x.Summary), .. Actions.Select(x => $"{x.Program} {x.Arguments} {x.Summary}")]).ToLowerInvariant();
        }

        public record ScheduledTaskActionDto(string Type, string Program, string Arguments, string StartIn, string Summary)
        {
            public static ScheduledTaskActionDto From(Microsoft.Win32.TaskScheduler.Action action) => action is ExecAction exec
                ? new("StartProgram", exec.Path ?? "", exec.Arguments ?? "", exec.WorkingDirectory ?? "", exec.ToString())
                : new(action.ActionType.ToString(), "", "", "", action.ToString());
        }

        // Every field is filled, whatever the trigger type, so a trigger can be sent back unchanged
        public record ScheduledTaskTriggerDto(
            // Null for a trigger type the API can't create or change, such as an event trigger. Type names it
            TaskTriggerKind? Kind,
            string Type,
            string Summary,
            bool Enabled,
            DateTime Start,
            short DaysInterval,
            short WeeksInterval,
            DaysOfTheWeek DaysOfWeek,
            MonthsOfTheYear Months,
            string DaysOfMonth,
            bool LastDayOfMonth,
            string LogonUser,
            TimeSpan Delay,
            TimeSpan RepeatInterval,
            TimeSpan RepeatDuration,
            bool HasExpiry,
            DateTime Expires)
        {
            public static ScheduledTaskTriggerDto From(Trigger trigger)
            {
                var model = TaskTriggerModel.From(trigger);
                return new ScheduledTaskTriggerDto(
                    ScheduledTaskService.TriggerKindOf(trigger),
                    trigger.TriggerType.ToString(),
                    trigger.ToString(),
                    model.Enabled,
                    DateTime.SpecifyKind(model.Start, DateTimeKind.Unspecified),
                    model.DaysInterval,
                    model.WeeksInterval,
                    model.DaysOfWeek,
                    model.Months,
                    model.DaysOfMonth,
                    model.LastDayOfMonth,
                    model.LogonUser,
                    model.Delay,
                    model.RepeatInterval,
                    model.RepeatDuration,
                    model.HasExpiry,
                    DateTime.SpecifyKind(model.Expires, DateTimeKind.Unspecified));
            }
        }
    }
}
