using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class DashboardWidgetConfiguration : IEntityTypeConfiguration<DashboardWidget>
{
    public void Configure(EntityTypeBuilder<DashboardWidget> builder)
    {
        builder.ToTable("dashboard_widgets");

        builder.HasKey(dw => dw.Id);

        builder.HasOne(dw => dw.Dashboard)
            .WithMany(d => d.Widgets)
            .HasForeignKey(dw => dw.DashboardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dw => dw.SavedChart)
            .WithMany()
            .HasForeignKey(dw => dw.SavedChartId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
