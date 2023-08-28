using Discord;
using IIGO.Areas.Identity;
using IIGO.Data;
using IIGO.Services;
using IIGO.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;

namespace IIGO
{
    public class Program
    {
        internal delegate IMessengerService ServiceResolver(string serviceType);

        public static void Main(string[] args)
        {
            if (EventLog.SourceExists(Constants.EventLogSource))
            {
                var source = EventLog.LogNameFromSourceName(Constants.EventLogSource, ".");
                if (source == "Application")
                {
                    EventLog.DeleteEventSource(Constants.EventLogSource);
                    EventLog.CreateEventSource(Constants.EventLogSource, Constants.EventLogName);
                    return;
                }
            }
            //if (!EventLog.SourceExists(Constants.EventLogSource))
            //    EventLog.CreateEventSource(Constants.EventLogSource, Constants.EventLogName);

            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.AddEventLog(eventLogSettings =>
            {
                eventLogSettings.SourceName = Constants.EventLogSource;
                eventLogSettings.LogName = Constants.EventLogName;
            });
            builder.Host.UseWindowsService(options =>
            {
                options.ServiceName = "IIGO Panel Service";
            });

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            var conn = new SqliteConnectionStringBuilder(connectionString);
            conn.DataSource = AppDomain.CurrentDomain.MapPath(conn.DataSource);
            EventLog.WriteEntry(Constants.EventLogSource, $"Attempting to connect to database using connection string: {connectionString}", EventLogEntryType.Information, 1000);
            EventLog.WriteEntry(Constants.EventLogSource, $"Transformed DataSource: {conn.DataSource}", EventLogEntryType.Information, 1000);
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(conn.ConnectionString));
#if DEBUG
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
#endif
            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
            builder.Services.AddScoped<ConfigurationService>();
            //builder.Services.AddSingleton<IISService>();
            builder.Services.AddSingleton<WindowsUpdateService>();

            builder.Services.AddScoped<IMessengerService, DiscordService>();
            builder.Services.AddScoped<IMessengerService, GoogleChatService>();
            builder.Services.AddScoped<IMessengerService, PostmarkService>();
            builder.Services.AddScoped<IMessengerService, SendGridService>();
            builder.Services.AddScoped<IMessengerService, SESService>();
            builder.Services.AddScoped<IMessengerService, SlackService>();
            builder.Services.AddScoped<IMessengerService, SMTPService>();
            builder.Services.AddScoped<IMessengerService, SNSService>();

            builder.Services.AddTransient<ServiceResolver>(serviceProvider => serviceTypeName =>
            {
                var services = serviceProvider.GetServices<IMessengerService>().ToList();
                return services.FirstOrDefault(x => x.ServiceName == serviceTypeName);
            });

            var app = builder.Build();

            UpdateDatabase(app);

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            InstallInitialSettings(app);

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            app.Run();
        }

        private static void UpdateDatabase(WebApplication app)
        {
            using (var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var context = serviceScope.ServiceProvider.GetService<ApplicationDbContext>())
                {
                    context.Database.Migrate();
                }
            }
        }

        private static void InstallInitialSettings(WebApplication app)
        {
            using (var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var context = serviceScope.ServiceProvider.GetService<ApplicationDbContext>())
                {
                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "MessengerService") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "MessengerService", SettingValue = nameof(SMTPService) });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "AppPoolNotificationThreshold") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "AppPoolNotificationThreshold", SettingValue = "5,30" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "AppPoolNotificationEmail") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "AppPoolNotificationEmail", SettingValue = "admin@example.com" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "AppPoolHistoryLength") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "AppPoolHistoryLength", SettingValue = "365" });

                    context.SaveChanges();
                }
            }

            using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                if (roleManager.FindByNameAsync("Administrator").GetAwaiter().GetResult() == null)
                {
                    var result = roleManager.CreateAsync(new IdentityRole { Name = "Administrator" }).GetAwaiter().GetResult();
                }

                if (manager.FindByNameAsync("admin").GetAwaiter().GetResult() == null)
                {
                    var user = new IdentityUser { UserName = "admin", Email = "admin@example.com", LockoutEnabled = false, EmailConfirmed = true };
                    var result = manager.CreateAsync(user, "IIGOAdmin#10").GetAwaiter().GetResult();
                    if (result.Succeeded)
                    {
                        user = manager.FindByNameAsync("admin").GetAwaiter().GetResult();
                        manager.AddToRoleAsync(user, "Administrator");
                    }
                }

                if (manager.FindByNameAsync("admin").GetAwaiter().GetResult() is IdentityUser admin)
                {
                    if (admin != null)
                    {
                        if (!manager.IsInRoleAsync(admin, "Administrator").GetAwaiter().GetResult())
                        {
                            manager.AddToRoleAsync(admin, "Administrator");
                        }
                    }
                }
            }

            using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var services = scope.ServiceProvider.GetServices<IMessengerService>();
                foreach (var service in services)
                {
                    service.Initialize();
                }
            }
        }
    }
}