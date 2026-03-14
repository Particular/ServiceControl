namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

public class MessageBodyFileResult
{
    public Stream Stream { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public int BodySize { get; set; }
    public string Etag { get; set; } = null!;
}
