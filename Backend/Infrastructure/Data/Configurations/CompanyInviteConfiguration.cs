using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class CompanyInviteConfiguration : IEntityTypeConfiguration<CompanyInvite>
{
    public void Configure(EntityTypeBuilder<CompanyInvite> builder)
    {
        builder.ToTable("company_invites");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(ci => ci.Token)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(ci => ci.Token)
            .IsUnique();

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
