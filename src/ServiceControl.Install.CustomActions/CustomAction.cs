namespace ServiceControl.Install.CustomActions
{
    using System;
    using Microsoft.Deployment.WindowsInstaller;

    public class CustomActions
    {
        /// <summary>
        /// This custom action will test if a port number is in the range 0 to 49152. Sets VALID_PORT to TRUE/FALSE
        /// </summary>
        [CustomAction()]
        public static ActionResult CheckValidPort(Session session)
        {
            try
            {
                Log(session, "Start custom action CheckValidPort");
                if (!session.CustomActionData.ContainsKey("PORT"))
                {
                    Log(session, "CheckValidPort custom action requires a port variable be passed to it in the from PORT=xxxx");
                    return ActionResult.Failure;
                }
                var port = session.CustomActionData["PORT"];
                UInt16 portNumber;
                if (UInt16.TryParse(port, out portNumber))
                {
                    // Port numbder 49152 and above should not be used http://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.xhtml
                    if (portNumber < 49152)
                    {
                        session.Set("VALID_PORT", "TRUE");
                        return ActionResult.Success;
                    }
                }
                session.Set("VALID_PORT", "FALSE");
                return ActionResult.Success;
            }
            finally
            {
                Log(session, "End custom action CheckValidPort");
            }
        }
        
        static void Log(Session session, string message)
        {
            LogAction(session, message);
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

