namespace ServiceControl.Infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

/// <summary>
/// Bounds a data store query to one wall-clock deadline. Cancelling the client-side call aborts the request to the
/// database server, which terminates the query server-side; a server-side query timeout alone is not enough, as
/// RavenDB renews its deadline while a query keeps making progress and a single query can then run for hours.
/// </summary>
public static class QueryTimeLimit
{
    public const string SettingName = "QueryTimeoutInSeconds";
    public const int DefaultSeconds = 60;
    public const int MaxSeconds = 3600;

    public static readonly TimeSpan Default = TimeSpan.FromSeconds(DefaultSeconds);

    /// <param name="query">The query, which must observe the token it is handed.</param>
    /// <param name="limit">The wall-clock limit for the whole query.</param>
    /// <param name="settingName">The fully qualified setting the timeout message names, e.g. "ServiceControl/QueryTimeoutInSeconds".</param>
    /// <param name="cancellationToken">The caller's token. Its cancellation surfaces as cancellation, not as a timeout.</param>
    public static async Task<T> Run<T>(Func<CancellationToken, Task<T>> query, TimeSpan limit, string settingName, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(limit);

        try
        {
            return await query(deadline.Token).ConfigureAwait(false);
        }
        // Once the deadline has fired, whatever the provider raises is the cancellation: RavenDB and Npgsql raise
        // OperationCanceledException, Microsoft.Data.SqlClient raises SqlException("Operation cancelled by user").
#pragma warning disable PS0019 // Catching Exception is the point: the filter attributes it to the deadline, not to the exception type
        catch (Exception e) when (deadline.Token.IsCancellationRequested)
#pragma warning restore PS0019
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // The caller cancelled (e.g. the HTTP request was aborted), not the query time limit
                if (e is OperationCanceledException)
                {
                    throw;
                }

                throw new OperationCanceledException("The query was cancelled by the caller.", e, cancellationToken);
            }

            throw Timeout(e, limit, settingName);
        }
    }

    static TimeoutException Timeout(Exception cause, TimeSpan limit, string settingName) =>
        new($"The query did not complete within the allowed query time of {limit.TotalSeconds:0} seconds and was cancelled. The '{settingName}' setting can be used to change the allowed database query time.", cause);

    public static TimeSpan Read(SettingsRootNamespace settingsRootNamespace, ILogger logger) =>
        Validate(SettingsReader.Read(settingsRootNamespace, SettingName, DefaultSeconds), $"{settingsRootNamespace}/{SettingName}", logger);

    public static TimeSpan Validate(int seconds, string settingName, ILogger logger)
    {
        if (seconds <= 0)
        {
            logger.LogError("{SettingName} must be greater than zero. Defaulting to {QueryTimeoutInSecondsDefault}", settingName, DefaultSeconds);
            return Default;
        }

        if (seconds > MaxSeconds)
        {
            logger.LogError("{SettingName} cannot be larger than {MaxQueryTimeoutInSeconds}. Defaulting to {QueryTimeoutInSecondsDefault}", settingName, MaxSeconds, DefaultSeconds);
            return Default;
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
