﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_SagaComplete_message_fails : AcceptanceTest
    {
        [Test]
        public async Task No_SagaType_Header_Is_Ok()
        {
            FailedMessageView failure = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<FailedMessageView>("/api/errors/", m => m.Id == c.UniqueMessageId);
                    failure = result;
                    return result;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoDelayedRetries(); });

            public class SendFailedMessage : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    context.EndpointNameOfReceivingEndpoint = Conventions.EndpointNamingConvention(typeof(FailureEndpoint));
                    context.MessageId = Guid.NewGuid().ToString();
                    context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, context.EndpointNameOfReceivingEndpoint).ToString();

                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = context.MessageId,
                        [Headers.ProcessingEndpoint] = context.EndpointNameOfReceivingEndpoint,
                        ["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z",
                        ["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage",
                        ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                        ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                        ["NServiceBus.ExceptionInfo.StackTrace"] = string.Empty,
                        ["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(FailureEndpoint)),
                        ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z",
                        [Headers.ControlMessageHeader] = bool.TrueString,
                        ["NServiceBus.ClearTimeouts"] = bool.TrueString,
                        ["NServiceBus.SagaId"] = "626f86be-084c-4867-a5fc-a53f0092b299"
                    };

                    var outgoingMessage = new OutgoingMessage(context.MessageId, headers, new byte[0]);

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId { get; set; }
        }
    }
}