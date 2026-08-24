#nullable enable
namespace ServiceControl.Persistence
{
    using System.Collections.Generic;

    /// <summary>
    /// What staging a message for retry needs: the headers of the attempt being retried, where it was
    /// failing, and how many times staging it has already failed.
    /// </summary>
    /// <param name="Id">The id the staged message is sent under, which is the persister's own id for the failed message.</param>
    /// <param name="AttemptMessageId">The message id of the attempt being retried, kept as a header so the retry can be correlated to it.</param>
    public record StagingMessage(
        string Id,
        string UniqueMessageId,
        string AttemptMessageId,
        string FailingEndpointAddress,
        Dictionary<string, string> Headers,
        int StageAttempts);
}
