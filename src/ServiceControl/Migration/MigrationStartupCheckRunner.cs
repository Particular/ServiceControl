namespace ServiceControl.Migration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Runs startup checks in order and stops at the first one that refuses.
/// </summary>
static class MigrationStartupCheckRunner
{
    /// <summary>
    /// Runs each check in turn, and wraps whatever a failing one throws in a message naming the check and
    /// saying that nothing has been copied.
    /// </summary>
    /// <exception cref="Exception">A check failed. The check's own message is kept, and its exception is the inner one.</exception>
    public static async Task Run(IReadOnlyList<IMigrationStartupCheck> checks, CancellationToken cancellationToken = default)
    {
        foreach (var check in checks)
        {
            try
            {
                await check.Run(cancellationToken);
            }
            // A shutdown is not a check failing, and saying it was would send the customer after the wrong thing.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new Exception(
                    $"Migration startup check '{check.Name}' failed, so ServiceControl will not start and nothing has been copied. {exception.Message}", exception);
            }
        }
    }
}
