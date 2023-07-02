using Discord;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IIGO.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ConfigSetting> ConfigSetting { get; set; }
        public DbSet<AppPoolMonitoring> AppPoolMonitoring { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ConfigSetting>()
                .HasIndex(e => e.SettingName)
                .IsUnique();
        }
    }
}