using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class SavedChartConfiguration : IEntityTypeConfiguration<SavedChart>
{
    public void Configure(EntityTypeBuilder<SavedChart> builder)
    {
        builder.ToTable("saved_charts");

        builder.HasKey(sc => sc.Id);

        builder.Property(sc => sc.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(sc => sc.ChartType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(sc => sc.XAxis).HasMaxLength(200);

        builder.Property(sc => sc.YAxis)
            .HasColumnType("text[]");

        builder.Property(sc => sc.Aggregation)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(sc => sc.SqlQuery)
            .IsRequired();

        builder.Property(sc => sc.TableName).HasMaxLength(200);

        builder.HasOne(sc => sc.User)
            .WithMany()
            .HasForeignKey(sc => sc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(sc => new { sc.UserId, sc.Title }).IsUnique();
    }
}
