namespace ServiceControlInstaller.Engine.Instances
{
    using System.ComponentModel;

    public enum InstanceType
    {
        [Description("ServiceControl Instance")]
        ServiceControl,

        [Description("Monitoring Instance")]
        Monitoring,

        [Description("Audit Instance")]
        ServiceControlAudit
    }
}