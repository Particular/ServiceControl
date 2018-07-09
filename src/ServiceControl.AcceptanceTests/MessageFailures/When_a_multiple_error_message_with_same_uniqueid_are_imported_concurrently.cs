namespace ServiceBus.Management.AcceptanceTests.Error
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;

    class When_a_multiple_error_message_with_same_uniqueid_are_imported_concurrently : AcceptanceTest
    {
        class CounterEnricher : ImportEnricher
        {
            public MyContext Context { get; set; }

            public override Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                string counter;
                if (headers.TryGetValue("Counter", out counter))
                {
                    Context.OnMessage(counter);
                }
                else
                {
                    Console.WriteLine("No Counter header found");
                }

                return Task.FromResult(0);
            }
        }

        [Test]
        public async Task The_import_should_support_it()
        {
            var criticalErrorExecuted = false;
            
            SetSettings = settings =>
            {
                settings.MaximumConcurrencyLevel = 10;
            };
            CustomConfiguration = config =>
            {
                config.DefineCriticalErrorAction(ctx =>
                {
                    criticalErrorExecuted = true;
                    return Task.FromResult(0);
                });
                config.RegisterComponents(c => c.ConfigureComponent<CounterEnricher>(DependencyLifecycle.SingleInstance));
            };

            FailedMessage failure = null;
            await Define<MyContext>()
                .WithEndpoint<SourceEndpoint>()
                .Done(async c =>
                {
                    if (c.UniqueId == null)
                    {
                        return false;
                    }

                    var result = await this.TryGet<FailedMessage>($"/api/errors/{c.UniqueId}", m =>
                    {
                        Console.WriteLine("Processing attempts: " + m.ProcessingAttempts.Count);
                        return m.ProcessingAttempts.Count == 10;
                    });
                    failure = result;
                    return criticalErrorExecuted || result;
                })
                .Run();

            Assert.IsFalse(criticalErrorExecuted);
            Assert.NotNull(failure);
            Assert.AreEqual(10, failure.ProcessingAttempts.Count);
        }

        public class SourceEndpoint : EndpointConfigurationBuilder
        {
            public SourceEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class SendMultipleFailedMessagesWithSameUniqueId : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    var messageId = Guid.NewGuid().ToString();
                    context.UniqueId = DeterministicGuid.MakeId(messageId, "Error.SourceEndpoint").ToString();

                    return new TransportOperations(GetMessages(context.UniqueId).ToArray());
                }

                IEnumerable<TransportOperation> GetMessages(string uniqueId)
                {
                    for (var i = 0; i < 10; i++)
                    {
                        var messageId = Guid.NewGuid().ToString();
                        var headers = new Dictionary<string, string>
                        {
                            [Headers.MessageId] = messageId,
                            ["ServiceControl.Retry.UniqueMessageId"] = uniqueId,
                            [Headers.ProcessingEndpoint] = "Error.SourceEndpoint",
                            ["NServiceBus.ExceptionInfo.ExceptionType"] = typeof(Exception).FullName,
                            ["NServiceBus.ExceptionInfo.Message"] = "Bad thing happened",
                            ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                            ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                            ["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty,
                            ["NServiceBus.FailedQ"] = "Error.SourceEndpoint",
                            ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z",
                            ["Counter"] = i.ToString()
                        };

                        var outgoingMessage = new OutgoingMessage(messageId, headers, new byte[0]);

                        yield return new TransportOperation(outgoingMessage, new UnicastAddressTag("error"));
                    }
                }
            }
        }

        class MyContext : ScenarioContext
        {
            ConcurrentDictionary<string, bool> receivedMessages = new ConcurrentDictionary<string, bool>();

            public void OnMessage(string counter)
            {
                receivedMessages.AddOrUpdate(counter, true, (id, old) => true);
            }

            public string UniqueId { get; set; }
        }
    }
}
