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

        builder.Property(ec => ec.Visibility)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(ec => ec.AllowedRoleIds)
            .HasColumnType("uuid[]");

        builder.HasOne(ec => ec.User)
            .WithMany()
            .HasForeignKey(ec => ec.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ec => ec.Company)
            .WithMany()
            .HasForeignKey(ec => ec.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ec => new { ec.UserId, ec.Name })
            .IsUnique()
            .HasFilter("\"CompanyId\" IS NULL OR \"Visibility\" = 'Private'");

        builder.HasIndex(ec => new { ec.CompanyId, ec.Name })
            .IsUnique()
            .HasFilter("\"CompanyId\" IS NOT NULL AND \"Visibility\" <> 'Private'");
    }
}
