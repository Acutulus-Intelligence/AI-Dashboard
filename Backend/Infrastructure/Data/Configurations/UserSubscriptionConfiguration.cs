using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("UserSubscriptions");

        builder.HasKey(us => us.Id);

        builder.Property(us => us.Price)
            .HasPrecision(18, 2);

        builder.HasOne(us => us.User)
            .WithMany()
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(us => us.Plan)
            .WithMany(sp => sp.UserSubscriptions)
            .HasForeignKey(us => us.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
