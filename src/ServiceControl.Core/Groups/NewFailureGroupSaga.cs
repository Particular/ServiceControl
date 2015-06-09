
namespace ServiceControl.Groups
{
    using NServiceBus.Saga;
    using ServiceControl.InternalContracts.Messages.MessageFailures;

    public class NewFailureGroupSagaData : ContainSagaData
    {
        [Unique]
        public string FailureGroupId { get; set; }
    }

    public class NewFailureGroupSaga : Saga<NewFailureGroupSagaData>, IAmStartedByMessages<RaiseNewFailureGroupDetectedEvent>
    {
        public void Handle(RaiseNewFailureGroupDetectedEvent message)
        {
            if (string.IsNullOrEmpty(Data.FailureGroupId))
            {
                Data.FailureGroupId = message.GroupId;
                Bus.Publish(new NewFailureGroupDetected
                    {
                        GroupId = message.GroupId,
                        GroupName = message.GroupName
                    });
            }
        }

        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<RaiseNewFailureGroupDetectedEvent>(m => m.GroupId)
                .ToSaga(s => s.FailureGroupId);
        }
    }
}
