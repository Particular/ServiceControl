namespace ServiceControl.Config.Framework
{
    using System;
    using System.IO;
    using Extensions;
    using Mindscape.Raygun4Net;

    public class RaygunFeedback : RaygunReporter
    {
        public RaygunFeedback()
        {
            InitializeTrackingId();
        }

        void InitializeTrackingId()
        {
            var trackerlocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Particular");
            if (!Directory.Exists(trackerlocation))
            {
                Directory.CreateDirectory(trackerlocation);
            }

            var trackingFile = new FileInfo(Path.Combine(trackerlocation, ".feedbackid"));
            if (!trackingFile.Exists || !Guid.TryParse(File.ReadAllText(trackingFile.FullName), out trackingId))
            {
                File.WriteAllText(trackingFile.FullName, trackingId.BareString());
            }
        }

        public void SendFeedBack(string emailAddress, string message, bool includeSystemInfo)
        {
            raygunClient.UserInfo = new RaygunIdentifierMessage(trackingId.BareString())
            {
                Email = emailAddress,
                UUID = trackingId.BareString()
            };

            var raygunMessage = RaygunMessageBuilder.New(new RaygunSettings())
                .SetUser(raygunClient.UserInfo)
                .SetVersion(Version)
                .SetExceptionDetails(new Feedback(message));

            if (includeSystemInfo)
            {
                raygunMessage.SetMachineName(Environment.MachineName);
                raygunMessage.SetEnvironmentDetails();
            }

            var m = raygunMessage.Build();
            raygunClient.Send(m).GetAwaiter().GetResult();
        }

        public void SendException(Exception ex, bool includeSystemInfo)
        {
            raygunClient.UserInfo = new RaygunIdentifierMessage(trackingId.BareString())
            {
                UUID = trackingId.BareString()
            };

            var raygunMessage = RaygunMessageBuilder.New(new RaygunSettings())
                .SetUser(raygunClient.UserInfo)
                .SetVersion(Version)
                .SetExceptionDetails(ex);

            if (includeSystemInfo)
            {
                raygunMessage.SetMachineName(Environment.MachineName);
                raygunMessage.SetEnvironmentDetails();
            }

            var m = raygunMessage.Build();
            raygunClient.Send(m).GetAwaiter().GetResult();
        }

        Guid trackingId = Guid.NewGuid();
    }

    class Feedback : Exception
    {
        public Feedback(string message) : base(message)
        {
        }
    }
}