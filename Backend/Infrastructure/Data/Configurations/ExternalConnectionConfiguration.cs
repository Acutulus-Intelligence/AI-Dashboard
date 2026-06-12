using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class ExternalConnectionConfiguration : IEntityTypeConfiguration<ExternalConnection>
{
    public void Configure(EntityTypeBuilder<ExternalConnection> builder)
    {
        builder.ToTable("external_connections");

        builder.HasKey(ec => ec.Id);

        builder.Property(ec => ec.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ec => ec.DbProvider)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(ec => ec.EncryptedConnectionString)
            .IsRequired();

        builder.Property(ec => ec.IsVerified)
            .IsRequired();

        builder.HasOne(ec => ec.User)
            .WithMany()
            .HasForeignKey(ec => ec.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ec => new { ec.UserId, ec.Name })
            .IsUnique();
    }
}
