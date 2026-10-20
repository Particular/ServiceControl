namespace ServiceControl.Config.UI.InstanceAdd
{
    using ServiceControlInstaller.Engine.Instances;

    public class MessageBodyStorageOption
    {
        public string Name { get; set; }
        public MessageBodyStorageType Value { get; set; }
    }

    public class AzureAuthenticationOption
    {
        public string Name { get; set; }
        public bool UsesManagedIdentity { get; set; }
    }
}
