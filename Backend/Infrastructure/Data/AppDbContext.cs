using System.Reflection;
using Domain.Models;
using Infrastructure.Data.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid, IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Dashboard> Dashboards => Set<Dashboard>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfiguration(new RefreshTokenConfiguration());

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
