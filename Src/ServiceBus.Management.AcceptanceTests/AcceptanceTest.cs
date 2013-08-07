namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using Api.Nancy;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [TestFixture]
    public abstract class AcceptanceTest
    {
        protected string RavenPath;

        [SetUp]
        public void SetUp()
        {
            RavenPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            Conventions.EndpointNamingConvention = t =>
            {
                var baseNs = typeof(AcceptanceTest).Namespace;
                var testName = GetType().Name;
                return t.FullName.Replace(baseNs + ".", String.Empty).Replace(testName + "+", String.Empty);
            };
        }

        [TearDown]
        public void Cleanup()
        {
            Delete(RavenPath);
        }

        public void Delete(string path)
        {
            DirectoryInfo emptyTempDirectory = null;

            try
            {
                emptyTempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
                emptyTempDirectory.Create();
                var arguments = string.Format("\"{0}\" \"{1}\" /W:1  /R:1 /FFT /MIR /NFL", emptyTempDirectory.FullName, path.TrimEnd('\\'));
                using (var process = Process.Start(new ProcessStartInfo("robocopy")
                {
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                }))
                {
                    process.WaitForExit();
                }

                using (var windowsIdentity = WindowsIdentity.GetCurrent())
                {
                    var directorySecurity = new DirectorySecurity();
                    directorySecurity.SetOwner(windowsIdentity.User);
                    Directory.SetAccessControl(path, directorySecurity);
                }

                Directory.Delete(path);
            }
            finally
            {
                if (emptyTempDirectory != null)
                {
                    emptyTempDirectory.Delete();
                }
            }
        }

        public static T Get<T>(string url) where T : class
        {
            var request = (HttpWebRequest)HttpWebRequest.Create("http://localhost:33333" + url);

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
            {
                throw new InvalidOperationException("Call failed: " + response.StatusCode.GetHashCode() + " - " + response.StatusDescription);
            }

            using (var stream = response.GetResponseStream())
            {
                var serializer = JsonSerializer.Create(serializerSettings);

                return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
            }
        }
    
        public static void Post<T>(string url, T payload = null) where T : class
        {
            var request = (HttpWebRequest)HttpWebRequest.Create("http://localhost:33333" + url);

            request.ContentType = "application/json";
            request.Method = "POST";

            var json = JsonConvert.SerializeObject(payload, serializerSettings);
            request.ContentLength = json.Length;

            Console.Out.Write(request.RequestUri);

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

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Accepted)
                throw new InvalidOperationException("Call failed: " + response.StatusCode.GetHashCode() + " - " + response.StatusDescription);
        }

        static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new UnderscoreMappingResolver(),
            Converters = { new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.RoundtripKind } }
        };
    }
}