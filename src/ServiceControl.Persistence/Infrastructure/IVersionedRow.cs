namespace ServiceControl.Persistence.Infrastructure
{
    /// <summary>
    /// A row the API sends to ServicePulse to be rendered. Every field the response shows has to appear in <c>VersionFields</c>, or it
    /// can change without the cache tag moving and the client keeps a stale page.
    /// </summary>
    public interface IVersionedRow
    {
        object?[] VersionFields { get; }
    }
}
