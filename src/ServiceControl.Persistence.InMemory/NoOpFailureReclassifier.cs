namespace ServiceControl.Persistence.InMemory
{
    using System.Threading.Tasks;

    class NoOpFailureReclassifier : IReclassifyFailedMessages
    {
        public Task<int> ReclassifyFailedMessages(bool force) => Task.FromResult(0);
    }
}
