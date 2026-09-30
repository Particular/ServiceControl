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

/// <param name="Hosting">One of AzureSql, AzureSqlManagedInstance, AzureSqlEdge, AzurePostgres, AzureMySql, AwsRds, GoogleCloudSql, RavenCloud, SelfHosted or Unknown.</param>
/// <param name="ServerVersion">The engine major version, or Unknown.</param>
/// <param name="Source">A <see cref="DatabaseHostingSource"/> value.</param>
/// <param name="ServerEdition">The engine's own edition designation, such as SQL Server's Edition property. NotApplicable where the engine has no editions.</param>
/// <param name="ServiceObjective">The Azure SQL service objective, such as S0 or GP_Gen5_2. NotApplicable everywhere else.</param>
public record DatabaseHosting(string Hosting, string ServerVersion, string Source, string ServerEdition = DatabaseHosting.NotApplicable, string ServiceObjective = DatabaseHosting.NotApplicable)
{
    public const string NotApplicable = "NotApplicable";

    /// <summary>Nothing was available to classify the host with. A determination, not a failure.</summary>
    public static readonly DatabaseHosting Unclassified = new(DatabaseHostClassifier.Unknown, DatabaseHostClassifier.Unknown, DatabaseHostingSource.None);
}
