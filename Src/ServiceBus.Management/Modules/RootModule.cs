namespace ServiceBus.Management.Modules
{
    using System.IO;
    using global::Nancy;

    public class RootModule : BaseModule
    {
        static RootModule()
        {
            var fileInfo = new FileInfo(typeof(RootModule).Assembly.Location);
            var lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;

            CurrentEtag = string.Concat("\"", lastWriteTimeUtc.Ticks.ToString("x"), "\"");
            CurrentLastModified = lastWriteTimeUtc.ToString("R");
        }

        public RootModule()
        {
            Get["/"] = parameters =>
            {
                var model = new RootUrls
                {
                    AuditUrl = BaseUrl + "/audit/{?page,per_page,direction,sort}",
                    EndpointsAuditUrl = BaseUrl + "/endpoints/{name}/audit/{?page,per_page,direction,sort}",
                    EndpointsUrl = BaseUrl + "/endpoints",
                    ErrorsUrl = BaseUrl + "/errors/{?page,per_page,direction,sort}",
                    EndpointsErrorUrl = BaseUrl + "/endpoints/{name}/errors/{?page,per_page,direction,sort}",
                    MessageSearchUrl =
                        BaseUrl + "/messages/search/{keyword}/{?page,per_page,direction,sort}",
                    EndpointsMessageSearchUrl =
                        BaseUrl +
                        "/endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
                    EndpointsMessagesUrl =
                        BaseUrl + "/endpoints/{name}/messages/{?page,per_page,direction,sort}",
                    Name = SettingsReader<string>.Read("Name", "Particular Management"),
                    Description = SettingsReader<string>.Read("Description", "Description for Particular Management"),
                };


                return Negotiate
                    //.WithMediaRangeModel(MediaRange.FromString(@"application/vnd.particular-v1"), new RootUrls{
                    //        AuditUrl = baseUrl + "/audit/{?page,per_page,direction,sort}",
                    //        EndpointsAuditUrl = baseUrl + "/endpoints/{name}/audit/{?page,per_page,direction,sort}",
                    //    })
                    //.WithMediaRangeModel(MediaRange.FromString(@"application/vnd.particular-v2"), new RootUrls
                    //    {
                    //        AuditUrl = baseUrl + "/audit/{?page,per_page,direction,sort}",
                    //        EndpointsAuditUrl = baseUrl + "/endpoints/{name}/audit/{?page,per_page,direction,sort}",
                    //    })
                    .WithModel(model)
                    .WithHeader("ETag", CurrentEtag)
                    .WithHeader("Last-Modified", CurrentLastModified)
                    .WithHeader("Cache-Control", "private, max-age=0, must-revalidate");
            };
        }

        static readonly string CurrentEtag;
        static readonly string CurrentLastModified;

        public class RootUrls
        {
            public string AuditUrl { get; set; }
            public string EndpointsAuditUrl { get; set; }
            public string EndpointsUrl { get; set; }
            public string ErrorsUrl { get; set; }
            public string EndpointsErrorUrl { get; set; }
            public string MessageSearchUrl { get; set; }
            public string EndpointsMessageSearchUrl { get; set; }
            public string EndpointsMessagesUrl { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }
    }
}