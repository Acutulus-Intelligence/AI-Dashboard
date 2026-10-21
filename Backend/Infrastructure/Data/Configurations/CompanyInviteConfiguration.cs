using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class CompanyInviteConfiguration : IEntityTypeConfiguration<CompanyInvite>
{
    public void Configure(EntityTypeBuilder<CompanyInvite> builder)
    {
        builder.ToTable("CompanyInvites");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(ci => ci.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(ci => ci.TokenHash)
            .IsUnique();

        builder.HasIndex(ci => new { ci.CompanyId, ci.Email })
            .IsUnique();

        builder.Property(ci => ci.DeliveryMethod)
            .HasConversion<int>();

        builder.Property(ci => ci.RowVersion)
            .IsRowVersion();

        builder.HasOne(ci => ci.Company)
            .WithMany()
            .HasForeignKey(ci => ci.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ci => ci.Role)
            .WithMany()
            .HasForeignKey(ci => ci.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
