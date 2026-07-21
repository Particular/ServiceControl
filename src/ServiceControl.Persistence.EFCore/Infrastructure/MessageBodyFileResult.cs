namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.IO;

public class MessageBodyFileResult
{
    public required Stream Stream { get; set; }
    public required string ContentType { get; set; }
    public required int BodySize { get; set; }
    public required string Etag { get; set; }
}