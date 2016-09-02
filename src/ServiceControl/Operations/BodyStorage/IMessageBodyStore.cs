namespace ServiceControl.Operations.BodyStorage
{
    public interface IMessageBodyStore
    {
        ClaimsCheck Store(IMessageBody messageBody, IMessageBodyStoragePolicy messageStoragePolicy);
        bool TryGet(string messageId, out IMessageBody messageBody);
    }
}