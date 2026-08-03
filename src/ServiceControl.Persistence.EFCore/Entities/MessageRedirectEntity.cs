namespace ServiceControl.Persistence.EFCore.Entities;

public class MessageRedirectEntity
{
    public required string FromPhysicalAddress { get; set; }

    public required string ToPhysicalAddress { get; set; }

    public DateTime LastModified { get; set; }
}
