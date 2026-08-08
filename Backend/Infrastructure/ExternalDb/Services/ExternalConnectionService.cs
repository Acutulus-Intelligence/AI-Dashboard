using Application.Common.Exceptions;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using Npgsql;

namespace Infrastructure.ExternalDb.Services;

public class ExternalConnectionService : IExternalConnectionService
{
    private const int MaxCompanyConnections = 5;
    private const int MaxIndividualConnections = 1;

    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ExternalDbSettings _settings;
    private readonly ILogger<ExternalConnectionService> _logger;
    private readonly IConnectionAccessService _access;

    public ExternalConnectionService(
        AppDbContext db,
        IEncryptionService encryption,
        IOptions<ExternalDbSettings> settings,
        ILogger<ExternalConnectionService> logger,
        IConnectionAccessService access)
    {
        _db = db;
        _encryption = encryption;
        _settings = settings.Value;
        _logger = logger;
        _access = access;
    }

    public async Task<ConnectionResponse> CreateAsync(Guid userId, CreateConnectionRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        if (!await _access.HasConnectionManagePermissionAsync(userId, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage connections.");

        if (!ConnectionStringParser.TryParse(request.ConnectionString, out var parsed, out var parseError))
            throw new ArgumentException(parseError);

        if (request.DbProvider != DbProvider.Sqlite)
        {
            if (parsed.Provider is null)
                throw new ArgumentException("Could not detect the database provider from this connection string.");

            if (parsed.Provider != request.DbProvider)
                throw new ArgumentException($"The connection string does not match the selected database provider ({parsed.Provider}).");

            if (HostBlocklist.IsBlocked(parsed.Host, _settings.BlockedHosts))
                throw new ArgumentException("This host is not allowed.");
        }

        var companyId = user.CompanyId;
        var visibility = request.Visibility;
        var allowedRoleIds = request.AllowedRoleIds ?? [];

        if (user.UserType != UserType.Company || companyId is null)
        {
            companyId = null;
            visibility = ConnectionVisibility.Private;
            allowedRoleIds = [];

            var count = await _db.ExternalConnections
                .CountAsync(ec => ec.UserId == userId && ec.CompanyId == null, ct);
            if (count >= MaxIndividualConnections)
                throw new ConflictException(
                    $"You have reached the limit of {MaxIndividualConnections} database connection.",
                    "connection_limit_reached");
        }
        else
        {
            if (visibility == ConnectionVisibility.Roles)
            {
                if (allowedRoleIds.Count == 0)
                    throw new InvalidOperationException("Select at least one role to share this connection with.");

                var validIds = await _db.CompanyRoles
                    .Where(r => r.CompanyId == companyId.Value)
                    .Select(r => r.Id)
                    .ToListAsync(ct);

                if (allowedRoleIds.Except(validIds).Any())
                    throw new InvalidOperationException("One or more selected roles do not belong to your company.");
            }
            else
            {
                allowedRoleIds = [];
            }

            var count = await _db.ExternalConnections
                .CountAsync(ec => ec.CompanyId == companyId, ct);
            if (count >= MaxCompanyConnections)
                throw new ConflictException(
                    $"Your company has reached the limit of {MaxCompanyConnections} database connections.",
                    "connection_limit_reached");

            await EnsureNameUniqueInCompanyAsync(companyId.Value, request.Name, null, visibility, ct);
        }

        var connectionString = request.ConnectionString;

        var connection = new ExternalConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = companyId,
            Name = request.Name,
            DbProvider = request.DbProvider,
            EncryptedConnectionString = _encryption.Encrypt(connectionString),
            IsVerified = false,
            Visibility = visibility,
            AllowedRoleIds = allowedRoleIds
        };

        _db.ExternalConnections.Add(connection);
        await _db.SaveChangesAsync(ct);

        return MapResponse(connection);
    }

    public async Task<List<ConnectionResponse>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.CompanyRole)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var visible = await _db.ExternalConnections
            .AsNoTracking()
            .Where(ec => ec.CompanyId == null && ec.UserId == userId)
            .ToListAsync(ct);

        if (user.CompanyId is not null)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == user.CompanyId.Value, ct);
            var isOwner = company is not null && company.OwnerId == userId;

            var companyConnections = await _db.ExternalConnections
                .AsNoTracking()
                .Where(ec => ec.CompanyId == user.CompanyId)
                .ToListAsync(ct);

            visible.AddRange(companyConnections.Where(ec =>
                ec.UserId == userId ||
                isOwner ||
                ec.Visibility == ConnectionVisibility.Company ||
                (ec.Visibility == ConnectionVisibility.Roles &&
                 ((user.CompanyRoleId.HasValue && ec.AllowedRoleIds.Contains(user.CompanyRoleId.Value)) ||
                  (user.CompanyRole is not null && user.CompanyRole.CanManageConnections)))));
        }

        return visible
            .OrderByDescending(ec => ec.CreatedAt)
            .Select(MapResponse)
            .ToList();
    }

    public async Task<int> GetCompanyConnectionCountAsync(Guid userId, CancellationToken ct = default)
    {
        var companyId = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CompanyId)
            .FirstOrDefaultAsync(ct);

        if (companyId is null)
            return 0;

        return await _db.ExternalConnections
            .CountAsync(ec => ec.CompanyId == companyId, ct);
    }

    public async Task<ConnectionResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var connection = await _access.FindViewableAsync(id, userId, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        return MapResponse(connection);
    }

    public async Task<ConnectionConfigResponse> GetConfigAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var connection = await _db.ExternalConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.Id == id, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        if (!await _access.CanManageAsync(id, userId, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage this connection.");

        var decrypted = _encryption.Decrypt(connection.EncryptedConnectionString);

        return new ConnectionConfigResponse(
            connection.Name,
            connection.DbProvider,
            decrypted,
            connection.Visibility,
            connection.AllowedRoleIds);
    }

    public Task<ParseConnectionStringResponse> ParseConnectionStringAsync(string connectionString, CancellationToken ct = default)
    {
        if (!ConnectionStringParser.TryParse(connectionString, out var parsed, out var error))
            throw new InvalidOperationException(error);

        var port = parsed.Port > 0
            ? parsed.Port
            : parsed.Provider switch
            {
                DbProvider.PostgreSql => 5432,
                DbProvider.MySql => 3306,
                DbProvider.SqlServer => 1433,
                _ => 0
            };

        return Task.FromResult(new ParseConnectionStringResponse(
            parsed.Provider,
            parsed.Host,
            port,
            parsed.Database,
            parsed.Username,
            parsed.Password));
    }

    public async Task<ConnectionResponse> UpdateAsync(Guid id, Guid userId, UpdateConnectionRequest request, CancellationToken ct = default)
    {
        var connection = await _db.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.Id == id, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        if (!await _access.CanManageAsync(id, userId, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage this connection.");

        if (!ConnectionStringParser.TryParse(request.ConnectionString, out var parsed, out var parseError))
            throw new ArgumentException(parseError);

        if (request.DbProvider != DbProvider.Sqlite)
        {
            if (parsed.Provider is null)
                throw new ArgumentException("Could not detect the database provider from this connection string.");

            if (parsed.Provider != request.DbProvider)
                throw new ArgumentException($"The connection string does not match the selected database provider ({parsed.Provider}).");

            if (HostBlocklist.IsBlocked(parsed.Host, _settings.BlockedHosts))
                throw new ArgumentException("This host is not allowed.");
        }

        var visibility = request.Visibility;
        var allowedRoleIds = request.AllowedRoleIds ?? [];

        if (connection.CompanyId is null)
        {
            visibility = ConnectionVisibility.Private;
            allowedRoleIds = [];
        }
        else
        {
            if (visibility == ConnectionVisibility.Roles)
            {
                if (allowedRoleIds.Count == 0)
                    throw new InvalidOperationException("Select at least one role to share this connection with.");

                var validIds = await _db.CompanyRoles
                    .Where(r => r.CompanyId == connection.CompanyId.Value)
                    .Select(r => r.Id)
                    .ToListAsync(ct);

                if (allowedRoleIds.Except(validIds).Any())
                    throw new InvalidOperationException("One or more selected roles do not belong to your company.");
            }
            else
            {
                allowedRoleIds = [];
            }

            await EnsureNameUniqueInCompanyAsync(connection.CompanyId.Value, request.Name, id, visibility, ct);
        }

        var connectionString = request.ConnectionString;

        connection.Name = request.Name;
        connection.DbProvider = request.DbProvider;
        connection.EncryptedConnectionString = _encryption.Encrypt(connectionString);
        connection.Visibility = visibility;
        connection.AllowedRoleIds = allowedRoleIds;
        connection.IsVerified = false;

        await _db.SaveChangesAsync(ct);

        return MapResponse(connection);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var connection = await _db.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.Id == id, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        if (!await _access.CanManageAsync(id, userId, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage this connection.");

        _db.ExternalConnections.Remove(connection);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> TestConnectionAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        if (!await _access.CanViewAsync(id, userId, ct))
            throw new KeyNotFoundException("Connection not found.");

        var connection = await _db.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.Id == id, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        var decrypted = _encryption.Decrypt(connection.EncryptedConnectionString);

        try
        {
            using var conn = CreateDbConnection(connection.DbProvider, decrypted);
            await conn.OpenAsync(ct);
            connection.IsVerified = true;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection verification failed for {ConnectionId} ({ConnectionName})", connection.Id, connection.Name);
            connection.IsVerified = false;
            await _db.SaveChangesAsync(ct);
            return false;
        }
    }

    private async Task EnsureNameUniqueInCompanyAsync(
        Guid companyId, string name, Guid? excludeId, ConnectionVisibility visibility, CancellationToken ct)
    {
        if (visibility == ConnectionVisibility.Private)
            return;

        var trimmed = name.Trim();
        var taken = await _db.ExternalConnections.AnyAsync(
            ec => ec.CompanyId == companyId
                && ec.Visibility != ConnectionVisibility.Private
                && ec.Name == trimmed
                && (!excludeId.HasValue || ec.Id != excludeId.Value),
            ct);

        if (taken)
            throw new ConflictException(
                $"A shared connection named \"{trimmed}\" already exists in your company.",
                "connection_name_conflict");
    }

    private static (string host, int port, string database, string username) ParseConnectionString(DbProvider provider, string connectionString)
    {
        if (provider == DbProvider.PostgreSql)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return (builder.Host ?? "", builder.Port, builder.Database ?? "", builder.Username ?? "");
        }

        if (provider == DbProvider.SqlServer)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var hostPort = (builder.DataSource ?? "").Split(',', 2);
            var host = hostPort[0];
            var port = hostPort.Length == 2 && int.TryParse(hostPort[1], out var parsedPort) ? parsedPort : 0;
            return (host, port, builder.InitialCatalog ?? "", builder.UserID ?? "");
        }

        if (provider == DbProvider.Sqlite)
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            return (string.Empty, 0, builder.DataSource ?? "", string.Empty);
        }

        var mySqlBuilder = new MySqlConnectionStringBuilder(connectionString);
        return (mySqlBuilder.Server ?? "", (int)mySqlBuilder.Port, mySqlBuilder.Database ?? "", mySqlBuilder.UserID ?? "");
    }

    private static System.Data.Common.DbConnection CreateDbConnection(DbProvider provider, string connectionString)
    {
        return provider switch
        {
            DbProvider.PostgreSql => new NpgsqlConnection(connectionString),
            DbProvider.MySql => new MySqlConnection(connectionString),
            DbProvider.SqlServer => new SqlConnection(connectionString),
            DbProvider.Sqlite => new SqliteConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    private ConnectionResponse MapResponse(ExternalConnection connection)
    {
        string host = string.Empty;
        string database = string.Empty;
        try
        {
            var (parsedHost, _, parsedDatabase, _) = ParseConnectionString(
                connection.DbProvider,
                _encryption.Decrypt(connection.EncryptedConnectionString));
            host = parsedHost;
            database = parsedDatabase;
        }
        catch
        {
        }

        return new ConnectionResponse(
            connection.Id,
            connection.Name,
            connection.DbProvider,
            connection.IsVerified,
            connection.CreatedAt,
            connection.UserId,
            connection.Visibility,
            connection.AllowedRoleIds,
            connection.CompanyId,
            host,
            database);
    }
}
