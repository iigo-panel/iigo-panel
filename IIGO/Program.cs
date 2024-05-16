using AsyncAwaitBestPractices;
using IIGO.Components;
using IIGO.Components.Account;
using IIGO.Data;
using IIGO.Services;
using IIGO.Services.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

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

            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.AddEventLog(eventLogSettings =>
            {
                eventLogSettings.SourceName = Constants.EventLogSource;
                eventLogSettings.LogName = Constants.EventLogName;
            });
            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityUserAccessor>();
            builder.Services.AddScoped<IdentityRedirectManager>();
            builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                })
                .AddIdentityCookies();

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

            builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();
            builder.Services.Configure<DataProtectionTokenProviderOptions>(opt => opt.TokenLifespan = TimeSpan.FromHours(2));

            builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

            builder.Services.AddScoped<ConfigurationService>();
            builder.Services.AddScoped<AppPoolMonitorService>();
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
                return services.First(x => x.ServiceName == serviceTypeName);
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

            InstallInitialSettings(app).SafeFireAndForget();

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // Add additional endpoints required by the Identity /Account Razor components.
            app.MapAdditionalIdentityEndpoints();

            app.Run();
        }

        private static void UpdateDatabase(WebApplication app)
        {
            using (var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var context = serviceScope.ServiceProvider.GetService<ApplicationDbContext>()!)
                {
                    context.Database.Migrate();
                }
            }
        }

        private static async Task InstallInitialSettings(WebApplication app)
        {
            using (var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var context = serviceScope.ServiceProvider.GetService<ApplicationDbContext>()!)
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
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                if (await roleManager.FindByNameAsync("Administrator") == null)
                {
                    var result = await roleManager.CreateAsync(new IdentityRole { Name = "Administrator" });
                }

                if (await manager.FindByNameAsync("admin") == null)
                {
                    var user = new ApplicationUser { UserName = "admin", Email = "admin@example.com", LockoutEnabled = false, EmailConfirmed = true };
                    var result = await manager.CreateAsync(user, "IIGOAdmin#10");
                    if (result.Succeeded)
                    {
                        user = await manager.FindByNameAsync("admin");
                        await manager.AddToRoleAsync(user!, "Administrator");
                    }
                }

                if (await manager.FindByNameAsync("admin") is ApplicationUser admin)
                {
                    if (admin != null)
                    {
                        if (!await manager.IsInRoleAsync(admin, "Administrator"))
                        {
                            await manager.AddToRoleAsync(admin, "Administrator");
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
