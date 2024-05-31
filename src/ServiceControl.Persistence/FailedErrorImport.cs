namespace ServiceControl.Operations
{
    using System;

    public class FailedErrorImport
    {
        public string Id { get; set; }
        public FailedTransportMessage Message { get; set; }
        public string ExceptionInfo { get; set; }

        public static string MakeDocumentId(Guid id) => $"FailedErrorImports/{id}";
    }
}