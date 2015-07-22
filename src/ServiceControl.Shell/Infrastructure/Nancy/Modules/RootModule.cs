namespace ServiceBus.Management.Infrastructure.Nancy.Modules
{
    using System.IO;
    using Particular.ServiceControl.Licensing;
    using Settings;
    using global::Nancy;

    public class RootModule : BaseModule
    {
        public RootModule()
        {
            Get["/"] = parameters =>
            {
                var model = new RootUrls
                {
                    EndpointsUrl = BaseUrl + "/endpoints",
                    SagasUrl = BaseUrl + "/sagas",
                    ErrorsUrl = BaseUrl + "/errors/{?page,per_page,direction,sort}",
                    EndpointsErrorUrl = BaseUrl + "/endpoints/{name}/errors/{?page,per_page,direction,sort}",
                    MessageSearchUrl =
                        BaseUrl + "/messages/search/{keyword}/{?page,per_page,direction,sort}",
                    EndpointsMessageSearchUrl =
                        BaseUrl +
                        "/endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
                    EndpointsMessagesUrl =
                        BaseUrl + "/endpoints/{name}/messages/{?page,per_page,direction,sort}",
                    RecoverabilityGroupUrl = BaseUrl + "/recoverability/groups",
                    RecoverabilityGroupErrorsUrl = BaseUrl + "/recoverability/groups/{groupId}/errors/{?page,per_page,direction,sort}",
                    Name = SettingsReader<string>.Read("Name", "ServiceControl"),
                    Description = SettingsReader<string>.Read("Description", "The management backend for the Particular Service Platform"),
                    LicenseStatus = License.IsValid ? "valid" : "invalid",
                    InstanceInfo = BaseUrl + "/instance-info"
                };

                return Negotiate
                    .WithModel(model);
            };

            Get["/instance-info"] = p => Negotiate
                    .WithModel(new
                                  {
                                      WindowsService = Settings.ServiceName,
                                      LogfilePath = Path.Combine(Settings.LogPath, "logfile.txt"),
                                      Settings.TransportType,
                                      RavenDBPath = Settings.DbPath
                                  });
        }

        public ActiveLicense License { get; set; }

        public class RootUrls
        {
            public string Description { get; set; }
            public string EndpointsErrorUrl { get; set; }
            public string EndpointsMessageSearchUrl { get; set; }
            public string EndpointsMessagesUrl { get; set; }
            public string EndpointsUrl { get; set; }
            public string ErrorsUrl { get; set; }
            public string InstanceInfo { get; set; }
            public string MessageSearchUrl { get; set; }
            public string LicenseStatus { get; set; }
            public string Name { get; set; }
            public string SagasUrl { get; set; }
            public string RecoverabilityGroupUrl { get; set; }
            public string RecoverabilityGroupErrorsUrl { get; set; }
        }
    }
}