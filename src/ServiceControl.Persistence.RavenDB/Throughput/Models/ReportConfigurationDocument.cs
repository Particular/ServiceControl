#nullable enable
namespace ServiceControl.Persistence.RavenDB.Throughput.Models;

using System.Collections.Generic;

class ReportConfigurationDocument
{
    public List<string> MaskedStrings { get; set; } = [];
}