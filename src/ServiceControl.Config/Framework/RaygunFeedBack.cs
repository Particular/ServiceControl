namespace ServiceControl.Config.Framework
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
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

        public Task SendFeedBack(string emailAddress, string message, bool includeSystemInfo)
        {
            var raygunMessage = RaygunMessageBuilder.New(new RaygunSettings())
                .SetUser(new RaygunIdentifierMessage(trackingId.BareString())
                {
                    Email = emailAddress,
                    UUID = trackingId.BareString()
                })
                .SetVersion(Version)
                .SetExceptionDetails(new Feedback(message));

            if (includeSystemInfo)
            {
                raygunMessage.SetMachineName(Environment.MachineName);
                raygunMessage.SetEnvironmentDetails();
            }

            var m = raygunMessage.Build();
            return raygunClient.Send(m);
        }

        public Task SendException(Exception ex, bool includeSystemInfo)
        {
            var raygunMessage = RaygunMessageBuilder.New(new RaygunSettings())
                .SetUser(new RaygunIdentifierMessage(trackingId.BareString())
                {
                    UUID = trackingId.BareString()
                })
                .SetVersion(Version)
                .SetExceptionDetails(ex);

            if (includeSystemInfo)
            {
                raygunMessage.SetMachineName(Environment.MachineName);
                raygunMessage.SetEnvironmentDetails();
            }

            var m = raygunMessage.Build();
            return raygunClient.Send(m);
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