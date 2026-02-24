using AsyncAwaitBestPractices;
using IIGO.Authorization;
using IIGO.Components;
using IIGO.Components.Account;
using IIGO.Data;
using IIGO.Services;
using IIGO.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
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
            if (args.Length > 0 && args.Contains("CreateLog"))
            {
                Console.WriteLine("CreateLog argument found");
                Console.WriteLine($"Checking existence of {Constants.EventLogSource}: {EventLog.SourceExists(Constants.EventLogSource)}");
                if (EventLog.SourceExists(Constants.EventLogSource))
                {
                    var source = EventLog.LogNameFromSourceName(Constants.EventLogSource, ".");
                    Console.WriteLine($"LogNameFromSourceName: {source}");
                    if (source == "Application")
                    {
                        EventLog.DeleteEventSource(Constants.EventLogSource);
                        Console.WriteLine($"Deleted Source: {Constants.EventLogSource}");
                        EventLog.CreateEventSource(Constants.EventLogSource, Constants.EventLogName);
                        Console.WriteLine($"Created Source: {Constants.EventLogSource} on {Constants.EventLogName}");
                    }
                }
                return;
            }

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

            builder.Services.AddBlazorBootstrap();

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

            builder.Services.AddAuthorization(options =>
            {
                foreach (var feature in Feature.All)
                {
                    options.AddPolicy(feature, policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.Requirements.Add(new FeatureRequirement(feature));
                    });
                }
            });
            builder.Services.AddSingleton<IAuthorizationHandler, FeatureAuthorizationHandler>();
            builder.Services.AddScoped<RolePermissionService>();

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
            builder.Services.AddScoped<IISService>();

            builder.Services.AddTransient<ServiceResolver>(serviceProvider => serviceTypeName =>
            {
                var services = serviceProvider.GetServices<IMessengerService>().ToList();
                return services.First(x => x.ServiceName == serviceTypeName);
            });

            var app = builder.Build();

            app.Logger.LogInformation(new EventId(1000), "Application Starting");
            EventLog.WriteEntry(Constants.EventLogSource, $"Attempting to connect to database using connection string: {connectionString}", EventLogEntryType.Information, 1001);
            EventLog.WriteEntry(Constants.EventLogSource, $"Transformed DataSource: {conn.DataSource}", EventLogEntryType.Information, 1001);
            app.Logger.LogInformation(new EventId(1001), "Attempting to connect to database using connection string: {connectionString}", connectionString);
            app.Logger.LogInformation(new EventId(1001), "Transformed DataSource: {connDataSource}", conn.DataSource);

            bool validLicense = LicenseKeyService.ValidateLicense(out var licenseKey);
            EventLog.WriteEntry(Constants.EventLogSource, $"License Valid: {validLicense}", EventLogEntryType.Information, 1002);
            app.Logger.LogInformation(new EventId(1002), "License Valid: {validLicense}", validLicense);
            if (validLicense)
            {
                EventLog.WriteEntry(Constants.EventLogSource, $"License Information: {licenseKey}", EventLogEntryType.Information, 1002);
                app.Logger.LogInformation(new EventId(1002), "License Information: {licenseKey}", licenseKey!.ToString());
            }
            else
            {
                app.Logger.LogWarning(new EventId(2001), "License Invalid: {licenseKey}", licenseKey?.ToString() ?? "N/A");
            }

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

                if (await roleManager.FindByNameAsync("Manager") == null)
                {
                    var result = await roleManager.CreateAsync(new IdentityRole { Name = "Manager" });
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

                if (await manager.FindByNameAsync("manager") == null)
                {
                    var user = new ApplicationUser { UserName = "manager", Email = "manager@example.com", LockoutEnabled = false, EmailConfirmed = true };
                    var result = await manager.CreateAsync(user, "IIGOManager#10");
                    if (result.Succeeded)
                    {
                        user = await manager.FindByNameAsync("manager");
                        await manager.AddToRoleAsync(user!, "Manager");
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

                if (await manager.FindByNameAsync("manager") is ApplicationUser managerUser)
                {
                    if (managerUser != null)
                    {
                        if (!await manager.IsInRoleAsync(managerUser, "Manager"))
                        {
                            await manager.AddToRoleAsync(managerUser, "Administrator");
                        }
                    }
                }

                // Seed default Manager role permissions
                var managerRole = await roleManager.FindByNameAsync("Manager");
                if (managerRole != null)
                {
                    using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    if (!dbContext.RolePermission.Any(rp => rp.RoleId == managerRole.Id))
                    {
                        var defaultManagerFeatures = new[]
                        {
                            Feature.ViewWebsites,
                            Feature.ViewAppPools,
                            Feature.ViewFirewallRules,
                            Feature.ViewScheduledTasks,
                            Feature.ViewServices,
                            Feature.ViewRoles
                        };
                        foreach (var feature in defaultManagerFeatures)
                        {
                            dbContext.RolePermission.Add(new RolePermission { RoleId = managerRole.Id, Feature = feature });
                        }
                        await dbContext.SaveChangesAsync();
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
