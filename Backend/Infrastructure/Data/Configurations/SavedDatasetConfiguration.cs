using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class SavedDatasetConfiguration : IEntityTypeConfiguration<SavedDataset>
{
    public void Configure(EntityTypeBuilder<SavedDataset> builder)
    {
        builder.ToTable("saved_datasets");

        builder.HasKey(ds => ds.Id);

        builder.Property(ds => ds.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ds => ds.TableName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ds => ds.ColumnNames)
            .HasColumnType("text[]");

        builder.Property(ds => ds.ColumnTypes)
            .HasColumnType("text[]");

        builder.Property(ds => ds.RowsJson)
            .IsRequired();

        builder.Property(ds => ds.RowCount)
            .IsRequired();

        builder.HasOne(ds => ds.User)
            .WithMany()
            .HasForeignKey(ds => ds.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ds => new { ds.UserId, ds.Name }).IsUnique();
    }
}