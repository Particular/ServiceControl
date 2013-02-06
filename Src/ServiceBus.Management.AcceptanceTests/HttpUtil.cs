namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Net;

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


            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException("Call failed - " + response.StatusDescription);

            Console.Out.WriteLine(" - 200");

            using (var stream = response.GetResponseStream())
            {
                var body = new StreamReader(stream).ReadToEnd();

                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(body);
            }
        }
    }
}