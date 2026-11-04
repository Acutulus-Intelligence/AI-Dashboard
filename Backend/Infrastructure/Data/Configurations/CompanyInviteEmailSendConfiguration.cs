using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class CompanyInviteEmailSendConfiguration : IEntityTypeConfiguration<CompanyInviteEmailSend>
{
    public void Configure(EntityTypeBuilder<CompanyInviteEmailSend> builder)
    {
        builder.ToTable("CompanyInviteEmailSends");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.RecipientEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(s => new { s.CompanyId, s.SentAt });

        builder.HasOne(s => s.Company)
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
