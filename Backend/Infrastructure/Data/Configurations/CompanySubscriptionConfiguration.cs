using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class CompanySubscriptionConfiguration : IEntityTypeConfiguration<CompanySubscription>
{
    public void Configure(EntityTypeBuilder<CompanySubscription> builder)
    {
        builder.ToTable("company_subscriptions");

        builder.HasKey(cs => cs.Id);

        builder.Property(cs => cs.Price)
            .HasPrecision(18, 2);

        builder.HasOne(cs => cs.Company)
            .WithOne()
            .HasForeignKey<CompanySubscription>(cs => cs.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cs => cs.Plan)
            .WithMany(sp => sp.CompanySubscriptions)
            .HasForeignKey(cs => cs.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
