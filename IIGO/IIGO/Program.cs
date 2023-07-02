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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace IIGO
{
    public class Program
    {
        public delegate IMessengerService ServiceResolver(string serviceType);

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseWindowsService();

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));
#if DEBUG
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
#endif
            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
            builder.Services.AddSingleton<WeatherForecastService>();
            builder.Services.AddSingleton<IISService>();
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

                if (manager.FindByNameAsync("admin").GetAwaiter().GetResult() == null)
                {
                    var user = new IdentityUser { UserName = "admin", Email = "admin@example.com", LockoutEnabled = false, EmailConfirmed = true };
                    var result = manager.CreateAsync(user, "IIGOAdmin#10").GetAwaiter().GetResult();
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