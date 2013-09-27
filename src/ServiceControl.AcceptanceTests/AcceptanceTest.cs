namespace ServiceBus.Management.AcceptanceTests
{
    using System;
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
            var request = (HttpWebRequest) WebRequest.Create(string.Format("http://localhost:{0}{1}", port, url));

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

            Console.Out.WriteLine(" - {0}", (int) response.StatusCode);

            //for now
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Thread.Sleep(1000);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(String.Format("Call failed: {0} - {1}", (int) response.StatusCode,
                    response.StatusDescription));
            }

            using (var stream = response.GetResponseStream())
            {
                var serializer = JsonSerializer.Create(serializerSettings);

                return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
            }
        }

        public void Post<T>(string url, T payload = null) where T : class
        {
            var request = (HttpWebRequest) WebRequest.Create(string.Format("http://localhost:{0}{1}", port, url));

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

            Console.Out.WriteLine(" - {0}", (int) response.StatusCode);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Accepted)
            {
                string body;
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8")))
                {
                    body = reader.ReadToEnd();
                }
                throw new InvalidOperationException(String.Format("Call failed: {0} - {1} - {2}",
                    (int) response.StatusCode, response.StatusDescription, body));
            }
        }

        static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new UnderscoreMappingResolver(),
            Converters = {new IsoDateTimeConverter {DateTimeStyles = DateTimeStyles.RoundtripKind}}
        };

        int port;
        string pathToAppConfig;

        public string PathToAppConfig
        {
            get { return pathToAppConfig; }
        }
    }
}