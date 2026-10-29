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
using System.Threading.RateLimiting;

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

            // API controllers (IIGO.Api), protected by keys from the API Keys page
            builder.Services.AddControllers();

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
            var fullPath = Path.GetDirectoryName(conn.DataSource);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath!);
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(conn.ConnectionString));
#if DEBUG
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
#endif

            builder.Services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = true;

                    // Lock accounts after repeated failed sign-ins (Login.razor passes lockoutOnFailure: true)
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
                .AddDefaultTokenProviders();
            builder.Services.Configure<DataProtectionTokenProviderOptions>(opt => opt.TokenLifespan = TimeSpan.FromHours(2));

            // The panel is meant to be reachable from the Internet, so the sign-in cookie is only ever sent over HTTPS,
            // never with cross-site requests, and a session ends after 8 hours without activity
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

            // Per-address limits on sign-in attempts and API calls. Behind a reverse proxy, add the
            // forwarded headers middleware so RemoteIpAddress is the client rather than the proxy.
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var path = context.Request.Path;

                    if (HttpMethods.IsPost(context.Request.Method) && path.StartsWithSegments("/Account"))
                        return RateLimitPartition.GetFixedWindowLimiter($"account:{address}", _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });

                    if (path.StartsWithSegments("/api"))
                        return RateLimitPartition.GetFixedWindowLimiter($"api:{address}", _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 60,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });

                    return RateLimitPartition.GetNoLimiter("none");
                });
                options.OnRejected = (context, _) =>
                {
                    context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                        .LogWarning("Rate limit reached for {RemoteIp} on {Path}", context.HttpContext.Connection.RemoteIpAddress, context.HttpContext.Request.Path);
                    return ValueTask.CompletedTask;
                };
            });

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
            builder.Services.AddScoped<ApiKeyService>();
            builder.Services.AddScoped<UserManagementService>();

            builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

            builder.Services.AddScoped<ConfigurationService>();
            builder.Services.AddScoped<MonitorHistoryService>();
            // Singletons so pages can read their status, and hosted services so they run in the background
            builder.Services.AddSingleton<AppPoolMonitorService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<AppPoolMonitorService>());
            builder.Services.AddSingleton<CertificateMonitorService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<CertificateMonitorService>());
            //builder.Services.AddSingleton<IISService>();
            builder.Services.AddSingleton<WindowsUpdateService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<WindowsUpdateService>());
            builder.Services.AddScoped<IMessengerService, DiscordService>();
            builder.Services.AddScoped<IMessengerService, GoogleChatService>();
            builder.Services.AddScoped<IMessengerService, PostmarkService>();
            builder.Services.AddScoped<IMessengerService, SendGridService>();
            builder.Services.AddScoped<IMessengerService, SESService>();
            builder.Services.AddScoped<IMessengerService, SlackService>();
            builder.Services.AddScoped<IMessengerService, SMTPService>();
            builder.Services.AddScoped<IMessengerService, SNSService>();
            builder.Services.AddScoped<IISService>();
            builder.Services.AddScoped<FirewallService>();

            builder.Services.AddTransient<ServiceResolver>(serviceProvider => serviceTypeName =>
            {
                var services = serviceProvider.GetServices<IMessengerService>().ToList();
                return services.First(x => x.ServiceIdentifier == serviceTypeName);
            });

            var app = builder.Build();

            if (app.Logger.IsEnabled(LogLevel.Information))
            {
                app.Logger.LogInformation(new EventId(1000), "Application Starting");
            }
            if (app.Logger.IsEnabled(LogLevel.Information))
            {
                app.Logger.LogInformation(new EventId(1001), "Attempting to connect to database using connection string: {connectionString}", connectionString);
                app.Logger.LogInformation(new EventId(1001), "Transformed DataSource: {connDataSource}", conn.DataSource);
            }

            bool validLicense = LicenseKeyService.ValidateLicense(out var licenseKey);
            if (app.Logger.IsEnabled(LogLevel.Information))
            {
                app.Logger.LogInformation(new EventId(1002), "License Valid: {validLicense}", validLicense);
            }
            if (validLicense)
            {
                if (app.Logger.IsEnabled(LogLevel.Information))
                {
                    app.Logger.LogInformation(new EventId(1002), "License Information: {licenseKey}", licenseKey?.ToString() ?? "N/A");
                }
            }
            else
            {
                if (app.Logger.IsEnabled(LogLevel.Warning))
                {
                    app.Logger.LogWarning(new EventId(2001), "License Invalid: {licenseKey}", licenseKey?.ToString() ?? "N/A");
                }
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
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.UseMiddleware<MustChangePasswordMiddleware>();

            // The sign-in cookie is marked Secure, so browsers only send it over HTTPS
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                if (!app.Urls.Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    app.Logger.LogError(new EventId(2002),
                        "No HTTPS endpoint is configured (listening on {Urls}). Sign-in will not work over plain HTTP. " +
                        "Add a Kestrel HTTPS endpoint with a certificate in appsettings.json, or put the panel behind a reverse proxy that terminates TLS.",
                        string.Join(", ", app.Urls));
                }
            });

            app.MapControllers();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // Add additional endpoints required by the Identity /Account Razor components.
            app.MapAdditionalIdentityEndpoints();

            app.Run();
        }

        const string DefaultAdminPassword = "IIGOAdmin#10";

        /// <summary>
        /// Flags a user whose password is still the one from the README, so they have to change it at the next sign-in
        /// </summary>
        private static async Task RequirePasswordChangeIfDefault(WebApplication app, UserManager<ApplicationUser> manager, string userName, string defaultPassword)
        {
            var user = await manager.FindByNameAsync(userName);
            if (user == null || user.MustChangePassword || !await manager.CheckPasswordAsync(user, defaultPassword))
                return;

            user.MustChangePassword = true;
            user.LockoutEnabled = true;
            await manager.UpdateAsync(user);
            app.Logger.LogWarning(new EventId(2005), "User '{UserName}' still has the default password and must change it at the next sign-in.", userName);
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

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "AppPoolMonitorEnabled") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "AppPoolMonitorEnabled", SettingValue = "true" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "AppPoolMonitorInterval") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "AppPoolMonitorInterval", SettingValue = "2" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "AppPoolMonitorExclusions") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "AppPoolMonitorExclusions", SettingValue = "" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "CertificateMonitorEnabled") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "CertificateMonitorEnabled", SettingValue = "true" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "CertificateMonitorInterval") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "CertificateMonitorInterval", SettingValue = "10" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "CertificateExpirationThreshold") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "CertificateExpirationThreshold", SettingValue = "30" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "CertificateNotificationFrequency") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "CertificateNotificationFrequency", SettingValue = "24" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "CertificateNotificationEmail") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "CertificateNotificationEmail", SettingValue = "admin@example.com" });

                    if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == "CertificateHistoryLength") == null)
                        context.ConfigSetting.Add(new ConfigSetting { SettingName = "CertificateHistoryLength", SettingValue = "365" });

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

                // The default administrator only exists on a brand new database. Deleting it later is final,
                // and it has to pick its own password before the panel can be used.
                if (!await manager.Users.AnyAsync())
                {
                    var user = new ApplicationUser { UserName = "admin", Email = "admin@example.com", EmailConfirmed = true, MustChangePassword = true };
                    var result = await manager.CreateAsync(user, DefaultAdminPassword);
                    if (result.Succeeded)
                    {
                        await manager.AddToRoleAsync(user, "Administrator");
                        app.Logger.LogWarning(new EventId(2003), "Created the default 'admin' user. The password must be changed at first sign-in.");
                    }
                    else
                    {
                        app.Logger.LogError(new EventId(2004), "Could not create the default 'admin' user: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
                    }
                }

                // Databases from earlier versions may still hold the documented default passwords
                await RequirePasswordChangeIfDefault(app, manager, "admin", DefaultAdminPassword);
                await RequirePasswordChangeIfDefault(app, manager, "manager", "IIGOManager#10");

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
