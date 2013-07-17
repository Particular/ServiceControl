namespace ServiceBus.Management.Modules
{
    using System.IO;
    using Nancy;

    public class RootModule : BaseModule
    {
        static readonly string CurrentEtag;
        static readonly string CurrentLastModified;
        
        static RootModule()
        {
            var fi = new FileInfo(typeof(RootModule).Assembly.Location);
            var lastWriteTimeUtc = fi.LastWriteTimeUtc;

            CurrentEtag = string.Concat("\"", lastWriteTimeUtc.Ticks.ToString("x"), "\"");
            CurrentLastModified = lastWriteTimeUtc.ToString("R");
        }

        public RootModule()
        {
            Get["/"] = parameters =>
                {
                    string baseUrl = Request.Url.SiteBase + Request.Url.BasePath;

                    return Negotiate
                        //.WithMediaRangeModel(MediaRange.FromString(@"application/vnd.particular-v1"), new RootUrls{
                        //        AuditUrl = baseUrl + "/audit/{?page,per_page,direction,sort}",
                        //        EndpointsAuditUrl = baseUrl + "/endpoints/{name}/audit/{?page,per_page,direction,sort}",
                        //        ConversationUrl = baseUrl + "/conversations/{id}",
                        //    })
                        //.WithMediaRangeModel(MediaRange.FromString(@"application/vnd.particular-v2"), new RootUrls
                        //    {
                        //        AuditUrl = baseUrl + "/audit/{?page,per_page,direction,sort}",
                        //        EndpointsAuditUrl = baseUrl + "/endpoints/{name}/audit/{?page,per_page,direction,sort}",
                        //        ConversationUrl = baseUrl + "/conversations/{id}",
                        //    })
                        .WithModel(new RootUrls
                            {
                                AuditUrl = baseUrl + "/audit/{?page,per_page,direction,sort}",
                                EndpointsAuditUrl = baseUrl + "/endpoints/{name}/audit/{?page,per_page,direction,sort}",
                                ConversationUrl = baseUrl + "/conversations/{id}",
                                EndpointsUrl = baseUrl + "/endpoints",
                                ErrorsUrl = baseUrl + "/errors/{?page,per_page,direction,sort}",
                                EndpointsErrorUrl = baseUrl + "/endpoints/{name}/errors/{?page,per_page,direction,sort}",
                                MessageSearchUrl =
                                    baseUrl + "/messages/search/{keyword}/{?page,per_page,direction,sort}",
                                EndpointsMessageSearchUrl =
                                    baseUrl +
                                    "/endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
                                EndpointsMessagesUrl =
                                    baseUrl + "/endpoints/{name}/messages/{?page,per_page,direction,sort}",
                            })
                        .WithHeader("ETag", CurrentEtag)
                        .WithHeader("Last-Modified", CurrentLastModified)
                        .WithHeader("Cache-Control", "private, max-age=0, must-revalidate");
                };
        }

        public class RootUrls
        {
            public string AuditUrl { get; set; }
            public string EndpointsAuditUrl { get; set; }
            public string ConversationUrl { get; set; }
            public string EndpointsUrl { get; set; }
            public string ErrorsUrl { get; set; }
            public string EndpointsErrorUrl { get; set; }
            public string MessageSearchUrl { get; set; }
            public string EndpointsMessageSearchUrl { get; set; }
            public string EndpointsMessagesUrl { get; set; }
        }
    }
}