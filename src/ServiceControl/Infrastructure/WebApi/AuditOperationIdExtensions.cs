namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using Microsoft.AspNetCore.Mvc;

    static class AuditOperationIdExtensions
    {
        /// <summary>
        /// The audit operation id that ties the synchronous operation audit entry to the asynchronous
        /// per-message entries emitted while the operation is carried out. Reuses ASP.NET Core's
        /// per-request <c>TraceIdentifier</c> so the id also equals the <c>RequestId</c> already attached
        /// to every other log line of the request. Falls back to a GUID when there is no HttpContext
        /// (e.g. unit tests invoking the controller directly).
        /// </summary>
        public static string AuditOperationId(this ControllerBase controller)
        {
            var traceIdentifier = controller.HttpContext?.TraceIdentifier;
            return string.IsNullOrEmpty(traceIdentifier) ? Guid.NewGuid().ToString("N") : traceIdentifier;
        }
    }
}
