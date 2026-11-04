using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Application.Interfaces;

public interface IApplicationDbContext
{
    DatabaseFacade Database { get; }

    DbSet<Company> Companies { get; }
    DbSet<CompanyInvite> CompanyInvites { get; }
    DbSet<CompanyInviteEmailSend> CompanyInviteEmailSends { get; }
    DbSet<CompanyRole> CompanyRoles { get; }
    DbSet<CompanySubscription> CompanySubscriptions { get; }
    DbSet<Dashboard> Dashboards { get; }
    DbSet<DashboardWidget> DashboardWidgets { get; }
    DbSet<ExternalConnection> ExternalConnections { get; }
    DbSet<SavedChart> SavedCharts { get; }
    DbSet<ChartFolder> ChartFolders { get; }
    DbSet<SavedDataset> SavedDatasets { get; }
    DbSet<DataCollection> DataCollections { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<UserSubscription> UserSubscriptions { get; }
    DbSet<CardFingerprint> CardFingerprints { get; }
    DbSet<TwoFactorRecoveryCode> TwoFactorRecoveryCodes { get; }
    DbSet<User> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
