using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class CardFingerprintConfiguration : IEntityTypeConfiguration<CardFingerprint>
{
    public void Configure(EntityTypeBuilder<CardFingerprint> builder)
    {
        builder.ToTable("CardFingerprints");

        builder.HasKey(cf => cf.Id);

        builder.HasIndex(cf => cf.Fingerprint);

        builder.HasOne(cf => cf.User)
            .WithMany()
            .HasForeignKey(cf => cf.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
