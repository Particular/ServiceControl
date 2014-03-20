namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Xml.Linq;
    using System.Xml.XPath;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.SignalR;

    [TestFixture]
    public abstract class AcceptanceTest
    {
        [SetUp]
        public void SetUp()
        {
            ravenPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            port = FindAvailablePort(33333);

            pathToAppConfig = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            InitialiseAppConfig();

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
            Delete(ravenPath);
            File.Delete(pathToAppConfig);
        }

        string ravenPath;

        static int FindAvailablePort(int startPort)
        {
            var activeTcpListeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpListeners();

            for (var port = startPort; port < startPort + 1024; port++)
            {
                var portCopy = port;
                if (activeTcpListeners.All(endPoint => endPoint.Port != portCopy))
                {
                    return port;
                }
            }

            return startPort;
        }

        void InitialiseAppConfig()
        {
            XDocument doc;
            using (
                Stream configStream = File.Open(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
                    FileMode.Open))
            {
                doc = XDocument.Load(configStream);
            }

            var appSettingsElement = doc.XPathSelectElement(@"/configuration/appSettings");
            var dbPathElement = appSettingsElement.XPathSelectElement(@"add[@key=""ServiceControl/DbPath""]");
            if (dbPathElement != null)
            {
                dbPathElement.SetAttributeValue("value", ravenPath);
            }
            else
            {
                appSettingsElement.Add(new XElement("add",
                    new XAttribute("key", "ServiceControl/DbPath"), new XAttribute("value", ravenPath)));
            }

            var dbPortElement = appSettingsElement.XPathSelectElement(@"add[@key=""ServiceControl/Port""]");
            if (dbPortElement != null)
            {
                dbPortElement.SetAttributeValue("value", port);
            }
            else
            {
                appSettingsElement.Add(new XElement("add",
                    new XAttribute("key", "ServiceControl/Port"), new XAttribute("value", port)));
            }

            var syncIndexElement = appSettingsElement.XPathSelectElement(@"add[@key=""ServiceControl/CreateIndexSync""]");
            if (syncIndexElement != null)
            {
                syncIndexElement.SetAttributeValue("value", true);
            }
            else
            {
                appSettingsElement.Add(new XElement("add", new XAttribute("key", "ServiceControl/CreateIndexSync"), new XAttribute("value", true)));
            }


            doc.Save(pathToAppConfig);
        }

        static void Delete(string path)
        {
            DirectoryInfo emptyTempDirectory = null;

            try
            {
                emptyTempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
                emptyTempDirectory.Create();
                var arguments = string.Format("\"{0}\" \"{1}\" /W:1  /R:1 /FFT /MIR /NFL",
                    emptyTempDirectory.FullName, path.TrimEnd('\\'));
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

        public T Get<T>(string url) where T : class
        {
            HttpWebRequest request;


            if (url.StartsWith("http://"))
            {
                request = (HttpWebRequest)WebRequest.Create(url);
            }
            else
            {
                request = (HttpWebRequest)WebRequest.Create(string.Format("http://localhost:{0}{1}", port, url));
            }



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

            if (response == null)
            {
                Thread.Sleep(1000);
                return null;
            }

            Console.Out.WriteLine(" - {0}", (int)response.StatusCode);

            //for now
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                Thread.Sleep(1000);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(String.Format("Call failed: {0} - {1}", (int)response.StatusCode,
                    response.StatusDescription));
            }

            using (var stream = response.GetResponseStream())
            {
                var serializer = JsonSerializer.Create(serializerSettings);

                return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
            }
        }

        protected bool TryGet<T>(string url, out T response, Predicate<T> condition = null) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            response = Get<T>(url);


            if (response == null || !condition(response))
            {
                Thread.Sleep(1000);
                return false;
            }

            return true;
        }
        protected bool TryGetMany<T>(string url, out List<T> response, Predicate<T> condition = null) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            response = Get<List<T>>(url);

            if (response == null || !response.Any(m => condition(m)))
            {
                Thread.Sleep(1000);
                return false;
            }

            return true;
        }

        protected byte[] DownloadData(string url)
        {
            using (var client = new WebClient())
            {
                var urlToMessageBody = url;
                if (!url.StartsWith("http"))
                {
                    urlToMessageBody = string.Format("http://localhost:{0}/api{1}", port, url);
                }

                Console.Out.Write(urlToMessageBody);

                return client.DownloadData(urlToMessageBody);
            }
        }

        protected bool TryGetSingle<T>(string url, out T item, Predicate<T> condition = null) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = Get<List<T>>(url);

            if (response != null)
            {
                var items = response.Where(i => condition(i)).ToList();

                if (items.Count() > 1)
                {
                    throw new InvalidOperationException("More than one matching element found");
                }

                item = items.SingleOrDefault();

            }
            else
            {
                item = null;
            }

            if (item == null)
            {
                Thread.Sleep(1000);

                return false;
            }

            return true;
        }

        public void Post<T>(string url, T payload = null) where T : class
        {
            var request = (HttpWebRequest)WebRequest.Create(string.Format("http://localhost:{0}{1}", port, url));

            request.ContentType = "application/json";
            request.Method = "POST";

            var json = JsonConvert.SerializeObject(payload, serializerSettings);
            request.ContentLength = json.Length;

            Console.Out.Write(request.RequestUri);

            using (var stream = request.GetRequestStream())
            {
                using (var sw = new StreamWriter(stream))
                {
                    sw.Write(json);
                }
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

            Console.Out.WriteLine(" - {0}", (int)response.StatusCode);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Accepted)
            {
                string body;
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8")))
                {
                    body = reader.ReadToEnd();
                }
                throw new InvalidOperationException(String.Format("Call failed: {0} - {1} - {2}",
                    (int)response.StatusCode, response.StatusDescription, body));
            }
        }

        static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new UnderscoreMappingResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            Converters =
            {
                new IsoDateTimeConverter {DateTimeStyles = DateTimeStyles.RoundtripKind},
                new StringEnumConverter {CamelCaseText = true},

            }
        };

        int port;
        string pathToAppConfig;

        public string PathToAppConfig
        {
            get { return pathToAppConfig; }
        }
    }
}