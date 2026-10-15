using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.DeactivatedAt);

        builder.Property(u => u.DeletionScheduledAt);

        builder.HasIndex(u => u.DeletionScheduledAt);
    }
}
