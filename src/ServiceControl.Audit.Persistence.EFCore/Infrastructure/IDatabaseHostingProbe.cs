namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using ServiceControl.Audit.Persistence;

/// <summary>
/// How the database this instance stores its data in is hosted. Served to the primary instance for
/// its usage report, so every value is a fixed classification and never carries a host name,
/// database name or credential. Copied from the primary instance's EF persistence on purpose: the
/// audit persistence stack does not share projects with the primary's.
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

/// <param name="Hosting">One of AzureSql, AzureSqlManagedInstance, AzureSqlEdge, AzurePostgres, AwsRds, GoogleCloudSql, SelfHosted or Unknown.</param>
/// <param name="ServerVersion">The engine major version, or Unknown.</param>
/// <param name="Source">A <see cref="DatabaseHostingSource"/> value.</param>
/// <param name="ServerEdition">The SQL Server edition family: Express, Standard, Enterprise or Other. NotApplicable on the managed Azure services and on engines without editions.</param>
/// <param name="ServiceObjective">The Azure SQL service tier: Basic, Standard, Premium, GeneralPurpose, BusinessCritical, Hyperscale, ElasticPool or Other. NotApplicable everywhere else.</param>
public record DatabaseHosting(string Hosting, string ServerVersion, string Source, string ServerEdition = DatabaseHosting.NotApplicable, string ServiceObjective = DatabaseHosting.NotApplicable)
{
    public const string NotApplicable = "NotApplicable";

    /// <summary>Nothing was available to classify the host with. A determination, not a failure.</summary>
    public static readonly DatabaseHosting Unclassified = new(DatabaseHostClassifier.Unknown, DatabaseHostClassifier.Unknown, DatabaseHostingSource.None);
}
