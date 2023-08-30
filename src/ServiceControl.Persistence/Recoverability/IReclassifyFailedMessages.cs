namespace ServiceControl.Persistence
{
    using System.Threading.Tasks;

    /// <summary>
    /// Only RavenDB 3.5 should need a working implementation of this interface because reclassification is only valid
    /// for older data stored in previous versions of ServiceControl. Newer instances that start with other persistence
    /// should never need to do this, and can be implemented as a no-op.
    /// </summary>
    public interface IReclassifyFailedMessages
    {
        Task<int> ReclassifyFailedMessages(bool force);
    }
}
