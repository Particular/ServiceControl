namespace ServiceControl.Install.CustomActions
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using System.Xml.XPath;
    using Microsoft.Deployment.WindowsInstaller;
    using Microsoft.Win32;

    public class CustomActions
    {
        /// <summary>
        /// This custom action will test if a port number is in the range 0 to 49152. Sets VALID_PORT to TRUE/FALSE
        /// </summary>
        [CustomAction]
        public static ActionResult CheckValidPort(Session session)
        {
            if (!session.CustomActionData.ContainsKey("PORT"))
            {
                Log(session, "CheckValidPort custom action requires a port variable be passed to it in the from PORT=xxxx");
                return ActionResult.Failure;
            }
            var port = session.CustomActionData["PORT"];
            UInt16 portNumber;
            if (UInt16.TryParse(port, out portNumber))
            {
                // Port number 49152 and above should not be used http://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.xhtml
                if (portNumber < 49152)
                {
                    session.Set("VALID_PORT", "TRUE");
                    return ActionResult.Success;
                }
            }
            session.Set("VALID_PORT", "FALSE");
            return ActionResult.Success;
        }
        
        [CustomAction]
        public static ActionResult ReadForwardAuditMessagesFromConfig(Session session)
        {
            try
            {
                if (session.CustomActionData.Keys.Count != 1)
                {
                    Log(session, "ReadForwardAuditMessagesFromConfig custom action requires a single property name to be passed in the CustomActionData.  The result will passed to that property");
                    return ActionResult.Failure;
                }

                var outputProperty = session.CustomActionData.Keys.First();

                const string ServiceControlRegKey = @"SOFTWARE\ParticularSoftware\ServiceControl";
                var targetPath = session.Get("APPDIR");
                var configPath = Path.Combine(targetPath, @"ServiceControl.exe.config");
                var entryValue = "null";

                // Try to get value from existing config
                if (File.Exists(configPath))
                {
                    var configDoc = XDocument.Load(configPath);
                    var entry = configDoc.XPathSelectElement(@"/configuration/appSettings/add[@key='ServiceControl/ForwardAuditMessages']");
                    entryValue = (entry != null) ? entry.Attribute("value").Value : "null";
                }

                // Fallback to getting value from registry.
                if (!String.IsNullOrWhiteSpace(entryValue))
                {
                    var key = Registry.LocalMachine.OpenSubKey(ServiceControlRegKey, RegistryKeyPermissionCheck.Default);
                    if (key != null)
                    {
                        entryValue = (string) key.GetValue("ForwardAuditMessages", "null");
                    }
                }

                entryValue = entryValue.ToLower();
                switch (entryValue)
                {
                    case "true"  :
                    case "false" :
                        session.Set(outputProperty, entryValue);
                        break;
                    default:
                        session.Set(outputProperty,"null");
                        break;
                }
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                Log(session, "ReadForwardAuditMessagesFromConfig failed - {0}", ex);
                return ActionResult.Failure;
            }
        }
        
        [CustomAction]
        public static ActionResult ValidateForwardAuditMessages(Session session)
        {
            var forwardAuditMessages = session.Get("FORWARDAUDITMESSAGES");
            switch (forwardAuditMessages)
            {
                case "true":
                case "false":
                    return ActionResult.Success;
            }
            Log(session, "A required settings has not been provided. ForwardAuditMessages must be explicitly set to true or false when installing via unattended mode. e.g. 'Particular.ServiceControl.exe /quiet ForwardAuditMessages=false'");
            return ActionResult.Failure;
            
        }

        static void Log(Session session, string message, params object[] args)
        {
            LogAction(session, string.Format(message, args));
        }

        public static Action<Session, string> LogAction = (s, m) => s.Log(m);

        public static Func<Session, string, string> GetAction = (s, key) => s[key];

        public static Action<Session, string, string> SetAction = (s, key, value) => s[key] = value;
    }

    public static class SessionExtensions
    {
        public static string Get(this Session session, string key)
        {
            return CustomActions.GetAction(session, key);
        }

        public static void Set(this Session session, string key, string value)
        {
            CustomActions.SetAction(session, key, value);
        }
    }
}

