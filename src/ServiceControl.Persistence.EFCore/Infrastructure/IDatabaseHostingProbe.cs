namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence;

/// <summary>
/// How the database this instance stores its data in is hosted. Reported in usage telemetry, so
/// every value is a fixed classification and never carries a host name, database name or credential.
/// </summary>
public interface IDatabaseHostingProbe
{
    /// <summary>
    /// The persistence name as it appears in the persistence manifest, for example SQLServer.
    /// </summary>
    string StorageName { get; }

    /// <summary>
    /// Classifies the database host, asking the server itself where it can. Never throws; a server
    /// that cannot be reached or does not answer is reported as unknown.
    /// </summary>
    Task<DatabaseHosting> Probe(CancellationToken cancellationToken = default);
}

/// <param name="Hosting">One of AzureSql, AzureSqlManagedInstance, AzureSynapse, AzurePostgres, AwsRds, GoogleCloudSql, SelfHosted or Unknown.</param>
/// <param name="ServerVersion">The engine major version, or Unknown.</param>
public record DatabaseHosting(string Hosting, string ServerVersion)
{
    public static readonly DatabaseHosting Unavailable = new(DatabaseHostClassifier.Unknown, DatabaseHostClassifier.Unknown);
}
