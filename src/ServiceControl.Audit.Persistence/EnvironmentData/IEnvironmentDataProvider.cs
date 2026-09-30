namespace ServiceControl.Audit.Persistence
{
    using System.Collections.Generic;

    /// <summary>
    /// Provides environment data that the primary instance polls over the environment endpoint and
    /// folds into its usage report.
    /// </summary>
    public interface IEnvironmentDataProvider
    {
        IEnumerable<EnvironmentDatum> GetData();
    }
}
