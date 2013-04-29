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
        public static T Get<T>(string url) where T : class
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

            Console.Out.WriteLine(" - {0}", response.StatusCode);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException("Call failed: " +response.StatusCode + " - " + response.StatusDescription);



            using (var stream = response.GetResponseStream())
            {
                var serializer = JsonSerializer.Create(serializerSettings);

                return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
            }
        }

        public static void Post<T>(string url, T payload) where T : class
        {
            var request = (HttpWebRequest)HttpWebRequest.Create("http://localhost:8888" + url);

            request.ContentType = "application/json";
            request.Method = "POST";

            var json = JsonConvert.SerializeObject(payload, serializerSettings);
            request.ContentLength = json.Length;

            using (var stream = request.GetRequestStream())
            using (var sw = new StreamWriter(stream))
            {
              
                sw.Write(json);
            }


            HttpWebResponse response;

            try
            {
                response = request.GetResponse() as HttpWebResponse;
            }
            catch (WebException ex)
            {
                response = ex.Response as HttpWebResponse;
            }


            Console.Out.WriteLine(" - {0}", response.StatusCode);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException("Call failed: " + response.StatusCode + " - " + response.StatusDescription);
        }

        static JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new UnderscoreMappingResolver(),
            Converters = { new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.RoundtripKind } }
        };

            
    }
}