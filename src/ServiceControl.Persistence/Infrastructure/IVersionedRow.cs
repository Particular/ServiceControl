namespace ServiceControl.Persistence.Infrastructure
{
    /// <summary>
    /// A row the API sends to ServicePulse to be rendered. Every field the response shows has to appear in
    /// <c>GetVersionFields</c>, or it can change without the cache tag moving and the client keeps a stale page.
    /// </summary>
    public interface IVersionedRow
    {
        // A method, not a property: We dont want a serialiser to persist computed properties into the document, and these
        // values are derived from the fields beside them.
        object?[] GetVersionFields();
    }
}
