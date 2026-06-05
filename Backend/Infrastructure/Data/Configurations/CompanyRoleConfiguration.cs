using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class CompanyRoleConfiguration : IEntityTypeConfiguration<CompanyRole>
{
    public void Configure(EntityTypeBuilder<CompanyRole> builder)
    {
        builder.ToTable("company_roles");

        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(cr => cr.AllowedTables)
            .HasColumnType("text[]");

        builder.HasOne(cr => cr.Company)
            .WithMany(c => c.CompanyRoles)
            .HasForeignKey(cr => cr.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(cr => cr.Users)
            .WithOne(u => u.CompanyRole)
            .HasForeignKey(u => u.CompanyRoleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
