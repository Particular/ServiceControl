namespace ServiceControlInstaller.Engine.Validation
{
    public interface IContainQueueNames
    {
        string ErrorQueue { get; set; }
        string AuditQueue { get; set; }
        string ErrorLogQueue { get; set; }
        string AuditLogQueue { get; set; }
    }
}
