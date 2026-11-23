using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class ProcessedStripeEventConfiguration : IEntityTypeConfiguration<ProcessedStripeEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedStripeEvent> builder)
    {
        builder.ToTable("ProcessedStripeEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasMaxLength(255);
        builder.Property(e => e.Type).HasMaxLength(255);

        builder.HasIndex(e => e.ProcessedAt);
    }
}
