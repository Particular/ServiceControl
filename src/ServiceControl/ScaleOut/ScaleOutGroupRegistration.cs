namespace ServiceControl.MessageFailures.Api
{
    using System;

    public class ScaleOutGroupRegistration
    {
        readonly string groupId;
        readonly string address;

        public ScaleOutGroupRegistration(string groupId, string address)
        {
            this.groupId = groupId;
            this.address = address;
            Status = ScaleOutGroupRegistrationStatus.Connected;
            Id = String.Format("ScaleOutGroupRegistrations/{0}/{1}", groupId, address);
        }

        public string Id { get; set; }

        public string GroupId
        {
            get { return groupId; }
        }

        public string Address
        {
            get { return address; }
        }

        public ScaleOutGroupRegistrationStatus Status { get; set; }
    }
}