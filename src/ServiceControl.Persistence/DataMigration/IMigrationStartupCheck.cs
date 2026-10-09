namespace ServiceControl.Persistence.DataMigration;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// One check the host runs before the copy starts. The host, the source and the target each contribute their own.
/// </summary>
public interface IMigrationStartupCheck
{
    /// <summary>
    /// What the check is called in the refusal the host prints, so it is phrased to finish the sentence
    /// "Migration startup check '...' failed".
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Runs the check. Throws when it fails, which stops the host before anything is copied.
    /// </summary>
    Task Run(CancellationToken cancellationToken = default);
}
