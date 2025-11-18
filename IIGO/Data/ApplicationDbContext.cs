using Discord;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IIGO.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<ConfigSetting> ConfigSetting { get; set; }
        public DbSet<AppPoolMonitoring> AppPoolMonitoring { get; set; }
        public DbSet<DomainStatus> DomainStatus { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(b =>
            {
                b.ToTable("Users");
            });

            builder.Entity<IdentityUserClaim<string>>(b =>
            {
                b.ToTable("UserClaims");
            });

            builder.Entity<IdentityUserLogin<string>>(b =>
            {
                b.ToTable("UserLogins");
            });

            builder.Entity<IdentityUserToken<string>>(b =>
            {
                b.ToTable("UserTokens");
            });

            builder.Entity<IdentityRole>(b =>
            {
                b.ToTable("Roles");
            });

            builder.Entity<IdentityRoleClaim<string>>(b =>
            {
                b.ToTable("RoleClaims");
            });

            builder.Entity<IdentityUserRole<string>>(b =>
            {
                b.ToTable("UserRoles");
            });

            builder.Entity<ConfigSetting>()
                .HasIndex(e => e.SettingName)
                .IsUnique();

            builder.Entity<DomainStatus>()
                .Property(x => x.LastChecked)
                .HasDefaultValueSql("current_timestamp");
        }
    }
}
