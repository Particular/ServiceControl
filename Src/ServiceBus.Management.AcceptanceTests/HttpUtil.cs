namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using Api.Nancy;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class HttpUtil
    {
        protected T ApiCall<T>(string url) where T:class 
        {
            var request = (HttpWebRequest)HttpWebRequest.Create("http://localhost:8888" + url);

            request.ContentType = "application/json";

            request.Accept = "application/json";
            Console.Out.Write(request.RequestUri);

            HttpWebResponse response;

            try
            {
                response = request.GetResponse() as HttpWebResponse;
            }
            catch (WebException ex)
            {
                response = ex.Response as HttpWebResponse;
            }


            //for now
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Console.Out.WriteLine(" - 404");
                return null;
            }

            Console.Out.WriteLine(" - {0}",response.StatusCode);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException("Call failed - " + response.StatusDescription);

           

            using (var stream = response.GetResponseStream())
            {
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new UnderscoreMappingResolver(),
                    Converters = { new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.RoundtripKind } }
                };
                var serializer = JsonSerializer.Create(serializerSettings);

                return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
            }
        }
    }
}