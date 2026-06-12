using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class DashboardConfiguration : IEntityTypeConfiguration<Dashboard>
{
    public void Configure(EntityTypeBuilder<Dashboard> builder)
    {
        builder.ToTable("Dashboards");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.AllowedRoles)
            .HasColumnType("text[]");

        builder.HasMany(d => d.Users)
            .WithMany(u => u.Dashboards);
    }
}
