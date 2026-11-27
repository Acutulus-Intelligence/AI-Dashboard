namespace Infrastructure.ExternalDb;

public class ExternalDbSettings
{
    public int PreviewMaxRows { get; set; } = 10;
    public int QueryMaxRows { get; set; } = 10_000;
    public long QueryMaxBytes { get; set; } = 10 * 1024 * 1024;
    public int QueryTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Allows loopback and private network hosts. Off by default so a user cannot
    /// use a connection string to reach internal services (SSRF). Enable only for
    /// self-hosted/on-prem deployments and pair it with <see cref="AllowedHosts"/>.
    /// </summary>
    public bool AllowPrivateHosts { get; set; }

    /// <summary>Exact hostnames or IPs that bypass the private-address check.</summary>
    public string[] AllowedHosts { get; set; } = [];

    /// <summary>
    /// Requires TLS for stored database connections. Disable only for local
    /// development or trusted private networks; explicit disable settings are
    /// rejected even when this is false.
    /// </summary>
    public bool RequireTls { get; set; } = true;

    /// <summary>
    /// Verifies the server certificate chain (and hostname where the provider
    /// supports it). Disable only for self-signed certificates in trusted
    /// environments.
    /// </summary>
    public bool VerifyServerCertificate { get; set; } = true;

    /// <summary>When true, ask SQL Server for read-only intent (AG read routing).</summary>
    public bool SqlServerReadOnlyIntent { get; set; }

    public string[] BlockedHosts { get; set; } =
    [
        "169.254.169.254",
        "metadata.google.internal"
    ];
}
