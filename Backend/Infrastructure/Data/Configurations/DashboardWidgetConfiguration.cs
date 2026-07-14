using Domain.Models;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class DashboardWidgetConfiguration : IEntityTypeConfiguration<DashboardWidget>
{
    public void Configure(EntityTypeBuilder<DashboardWidget> builder)
    {
        builder.ToTable("dashboard_widgets");

        builder.HasKey(dw => dw.Id);

        builder.Property(dw => dw.WidgetType)
            .HasConversion<int>()
            .HasDefaultValue(WidgetType.Chart);

        builder.Property(dw => dw.TextContent)
            .HasMaxLength(5000);

        builder.Property(dw => dw.TextVariant)
            .HasConversion<int>();

        builder.Property(dw => dw.TextHorizontalAlign)
            .HasConversion<int>();

        builder.Property(dw => dw.TextVerticalAlign)
            .HasConversion<int>();

        builder.HasOne(dw => dw.Dashboard)
            .WithMany(d => d.Widgets)
            .HasForeignKey(dw => dw.DashboardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dw => dw.SavedChart)
            .WithMany()
            .HasForeignKey(dw => dw.SavedChartId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
