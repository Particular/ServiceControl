namespace ServiceControl.Persistence.Tests.RavenDB
{
    using System;
    using Contracts.Operations;
    using MessageFailures;
    using SagaAudit;
    using ServiceControl.Operations;

    static class FailedMessageBuilder
    {
        public static FailedMessage Minimal() => Minimal(_ => { });

        public static FailedMessage Minimal(Action<FailedMessage> customize)
        {
            var message = new FailedMessage
            {
                Id = "1",
                ProcessingAttempts =
                [
                    new FailedMessage.ProcessingAttempt
                    {
                        AttemptedAt = DateTime.Today,
                        MessageMetadata =
                        {
                            ["MessageIntent"] = "Send", ["CriticalTime"] = TimeSpan.FromSeconds(1)
                        }
                    }
                ],
                Status = FailedMessageStatus.Unresolved
            };

            customize(message);

            return message;
        }

        public static FailedMessage Default(Action<FailedMessage> customize)
        {
            var result = new FailedMessage
            {
                Id = "1",
                UniqueMessageId = "a",
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttempts =
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            Headers =
                            {
                                ["HeaderA"]="Wizard",
                                ["HeaderB"]="Cool",
                            },
                            AttemptedAt = DateTime.UtcNow,
                            MessageMetadata =
                            {
                                ["TimeSent"]="2023-09-20T12:00:00",
                                ["MessageId"]="x",
                                ["MessageType"]="MyType",
                                ["SendingEndpoint"]=new EndpointDetails{Host="host", HostId = Guid.NewGuid(), Name="ASender"},
                                ["ReceivingEndpoint"]=new EndpointDetails{Host="host", HostId = Guid.NewGuid(), Name="AReceiver"},
                                ["ConversationId"]="abc",
                                ["MessageIntent"]="Send",
                                ["BodyUrl"]="https://particular.net",
                                ["ContentLength"]=11111,
                                ["InvokedSagas"]=new[]{new SagaInfo{ChangeStatus = "YES!",SagaId = Guid.NewGuid(), SagaType = "XXX.YYY, ASagaType"}},
                                ["OriginatesFromSaga"]=new SagaInfo{ChangeStatus = "YES!",SagaId = Guid.NewGuid(), SagaType = "XXX.YYY, SomeOtherSagaType"},
                                ["CriticalTime"]=TimeSpan.FromSeconds(5),
                                ["ProcessingTime"]=TimeSpan.FromSeconds(5),
                                ["DeliveryTime"]=TimeSpan.FromSeconds(5),
                                ["IsSystemMessage"]=false,
                            },
                            FailureDetails = new FailureDetails()
                        }
                    }
            };

            customize(result);

            return result;
        }
    }

    namespace ObjectExtensions
    {
        static class ExtensionMethods
        {
            public static T CastTo<T>(this object o) => (T)o;
        }
    }
}