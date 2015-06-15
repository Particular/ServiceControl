namespace ServiceControl.Recoverability.Groups.Groupers
{
    public interface IGroupIdGenerator
    {
        string GenerateId(string groupType, string groupName);
    }
}