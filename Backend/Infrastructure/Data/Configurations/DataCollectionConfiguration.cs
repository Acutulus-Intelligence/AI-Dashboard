using System.Text.Json;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class DataCollectionConfiguration : IEntityTypeConfiguration<DataCollection>
{
    public void Configure(EntityTypeBuilder<DataCollection> builder)
    {
        builder.ToTable("data_collections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.Visibility)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.AllowedRoleIds)
            .HasColumnType("uuid[]");

        builder.HasOne(c => c.Company)
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.CreatedById, c.Name })
            .IsUnique()
            .HasFilter("\"CompanyId\" IS NULL OR \"Visibility\" = 'Private'");

        builder.HasIndex(c => new { c.CompanyId, c.Name })
            .IsUnique()
            .HasFilter("\"CompanyId\" IS NOT NULL AND \"Visibility\" <> 'Private'");
    }
}