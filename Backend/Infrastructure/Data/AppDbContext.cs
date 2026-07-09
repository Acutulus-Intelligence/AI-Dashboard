using Application.Interfaces;
using Domain.Models;
using Infrastructure.Data.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid,
        IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>,
        IApplicationDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<CompanyInvite> CompanyInvites => Set<CompanyInvite>();
        public DbSet<CompanyRole> CompanyRoles => Set<CompanyRole>();
        public DbSet<Dashboard> Dashboards => Set<Dashboard>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<ExternalConnection> ExternalConnections => Set<ExternalConnection>();
        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<CompanySubscription> CompanySubscriptions => Set<CompanySubscription>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfiguration(new CompanyConfiguration());
            builder.ApplyConfiguration(new CompanyInviteConfiguration());
            builder.ApplyConfiguration(new CompanyRoleConfiguration());
            builder.ApplyConfiguration(new DashboardConfiguration());
            builder.ApplyConfiguration(new RefreshTokenConfiguration());
            builder.ApplyConfiguration(new ExternalConnectionConfiguration());
            builder.ApplyConfiguration(new SubscriptionPlanConfiguration());
            builder.ApplyConfiguration(new UserSubscriptionConfiguration());
            builder.ApplyConfiguration(new CompanySubscriptionConfiguration());

            builder.Entity<Company>(entity =>
            {
                entity.Property(c => c.Roles)
                    .HasColumnType("text[]");

                entity.HasMany(c => c.Users)
                    .WithOne(u => u.Company)
                    .HasForeignKey(u => u.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Dashboards)
                    .WithOne(d => d.Company)
                    .HasForeignKey(d => d.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Dashboard>(entity =>
            {
                entity.Property(d => d.AllowedRoles)
                    .HasColumnType("text[]");

                entity.HasMany(d => d.Users)
                    .WithMany(u => u.Dashboards);
            });
        }
    }
}
