using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using Npgsql;

namespace Infrastructure.ExternalDb.Services;

public class ExternalConnectionService : IExternalConnectionService
{
    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ExternalDbSettings _settings;

    public ExternalConnectionService(AppDbContext db, IEncryptionService encryption, IOptions<ExternalDbSettings> settings)
    {
        _db = db;
        _encryption = encryption;
        _settings = settings.Value;
    }

    public async Task<ConnectionResponse> CreateAsync(Guid userId, CreateConnectionRequest request, CancellationToken ct = default)
    {
        if (HostBlocklist.IsBlocked(request.Host, _settings.BlockedHosts))
            throw new ArgumentException("This host is not allowed.");

        var connectionString = BuildConnectionString(request);

        var connection = new ExternalConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            DbProvider = request.DbProvider,
            EncryptedConnectionString = _encryption.Encrypt(connectionString),
            IsVerified = false
        };

        _db.ExternalConnections.Add(connection);
        await _db.SaveChangesAsync(ct);

        return MapResponse(connection);
    }

    public async Task<List<ConnectionResponse>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.ExternalConnections
            .AsNoTracking()
            .Where(ec => ec.UserId == userId)
            .OrderByDescending(ec => ec.CreatedAt)
            .Select(ec => new ConnectionResponse(ec.Id, ec.Name, ec.DbProvider, ec.IsVerified, ec.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ConnectionResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var connection = await _db.ExternalConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.Id == id && ec.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        return MapResponse(connection);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var connection = await _db.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.Id == id && ec.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Connection not found.");

        _db.ExternalConnections.Remove(connection);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> TestConnectionAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var connection = await _db.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.Id == id && ec.UserId == userId, ct)
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
        catch
        {
            connection.IsVerified = false;
            await _db.SaveChangesAsync(ct);
            return false;
        }
    }

    private static string BuildConnectionString(CreateConnectionRequest request)
    {
        return request.DbProvider switch
        {
            DbProvider.PostgreSql => new NpgsqlConnectionStringBuilder
            {
                Host = request.Host,
                Port = request.Port,
                Database = request.Database,
                Username = request.Username,
                Password = request.Password
            }.ConnectionString,
            DbProvider.MySql => new MySqlConnectionStringBuilder
            {
                Server = request.Host,
                Port = (uint)request.Port,
                Database = request.Database,
                UserID = request.Username,
                Password = request.Password
            }.ConnectionString,
            _ => throw new ArgumentOutOfRangeException(nameof(request.DbProvider))
        };
    }

    private static System.Data.Common.DbConnection CreateDbConnection(DbProvider provider, string connectionString)
    {
        return provider switch
        {
            DbProvider.PostgreSql => new NpgsqlConnection(connectionString),
            DbProvider.MySql => new MySqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    private static ConnectionResponse MapResponse(ExternalConnection connection)
    {
        return new ConnectionResponse(connection.Id, connection.Name, connection.DbProvider, connection.IsVerified, connection.CreatedAt);
    }
}
