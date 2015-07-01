namespace ServiceControl.Migrations.UnitTests
{
    using System;
    using NUnit.Framework;
    using ObjectApproval;
    using Particular.Backend.Debugging.RavenDB.Model;
    using Raven.Abstractions.Data;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using Raven.Json.Linq;
    using ServiceControl.MessageFailures;

    [TestFixture]
    class FailedMessageMigrationTests
    {
        EmbeddableDocumentStore store;

        [SetUp]
        public void RunMigration()
        {
            store = InMemoryStoreBuilder.GetInMemoryStore();
            new RavenDocumentsByEntityName().Execute(store);
            var metadata = new RavenJObject
                {
                    {"Raven-Entity-Name", new RavenJValue("FailedMessages")}
                };
            store.DatabaseCommands.Put("FailedMessages/474ca846-1f38-24db-3cf8-a147632de5ab", Etag.Empty, RavenJObject.Parse(OldFailedMessageDocument), metadata);

            store.WaitForIndexing();
            // TODO: Good way to set the settings
            var expiryThreshold = TimeSpan.FromDays(50 * 365);
            var migration = new FailedMessageMigration();
            migration.Setup(store);
            migration.Up().Wait();
        }

        [TearDown]
        public void Clean()
        {
            store.Dispose();
        }

        [Test]
        public void It_deletes_the_migrated_document()
        {
            using (var session = store.OpenSession())
            {
                var failedMessage = session.Load<FailedMessage>("FailedMessages/474ca846-1f38-24db-3cf8-a147632de5ab");
                Assert.IsNull(failedMessage);
            }
        }

        [Test]
        public void It_creates_a_message_snapshot_for_debugging()
        {
            using (var session = store.OpenSession())
            {
                var snapshot = session.Load<MessageSnapshotDocument>("AuditMessageSnapshots/474ca846-1f38-24db-3cf8-a147632de5ab");
                Assert.IsNotNull(snapshot);
                ObjectApprover.VerifyWithJson(snapshot);
            }
        }

        [Test]
        public void It_creates_failure_history()
        {
            using (var session = store.OpenSession())
            {
                var failureHistory = session.Load<MessageFailureHistory>("MessageFailureHistories/474ca846-1f38-24db-3cf8-a147632de5ab");
                Assert.IsNotNull(failureHistory);
                ObjectApprover.VerifyWithJson(failureHistory);
            }
        }

        const string OldFailedMessageDocument = @"{
  ""ProcessingAttempts"": [
    {
      ""MessageMetadata"": {
        ""MessageId"": ""dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5"",
        ""MessageIntent"": 1,
        ""HeadersForSearching"": ""dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5 dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5 Receiver.Default fa2c673600d1e72ad99147355265111d Send 4.6.5 2015-02-20 07:34:24:016152 Z SIMON-MAC text/xml ServiceBus.Management.AcceptanceTests.When_a_message_has_failed+MyMessage, ServiceBus.Management.AcceptanceTests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5\\0  96e684f1-74d3-44e2-b1b3-a445008d48cc SIMON-MAC Receiver.Default fa2c673600d1e72ad99147355265111d SIMON-MAC System.Exception Simulated exception ServiceBus.Management.AcceptanceTests System.Exception: Simulated exception\n   at ServiceBus.Management.AcceptanceTests.When_a_message_has_failed.Receiver.MyMessageHandler.Handle(MyMessage message) in c:\\Particular\\ServiceControl\\src\\ServiceControl.AcceptanceTests\\MessageFailures\\When_a_message_has_failed.cs:line 129\n   at lambda_method(Closure , Object , Object )\n   at NServiceBus.Unicast.HandlerInvocationCache.Invoke(Object handler, Object message, Dictionary`2 dictionary) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\HandlerInvocationCache.cs:line 61\n   at NServiceBus.Unicast.HandlerInvocationCache.InvokeHandle(Object handler, Object message) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\HandlerInvocationCache.cs:line 22\n   at NServiceBus.Unicast.Behaviors.LoadHandlersBehavior.<Invoke>b__1(Object handlerInstance, Object message) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\LoadHandlersBehavior.cs:line 42\n   at NServiceBus.Unicast.Behaviors.InvokeHandlersBehavior.DispatchMessageToHandlersBasedOnType(IBuilder builder, LogicalMessage toHandle, MessageHandler messageHandler) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\InvokeHandlersBehavior.cs:line 63\n   at NServiceBus.Unicast.Behaviors.InvokeHandlersBehavior.Invoke(HandlerInvocationContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\InvokeHandlersBehavior.cs:line 29\n   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51   at NServiceBus.Pipeline.BehaviorChain`1.Invoke(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 36\n   at NServiceBus.Pipeline.PipelineExecutor.Execute[T](BehaviorChain`1 pipelineAction, T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 139\n   at NServiceBus.Pipeline.PipelineExecutor.InvokeHandlerPipeline(MessageHandler handler) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 68\n   at NServiceBus.Unicast.Behaviors.LoadHandlersBehavior.Invoke(ReceiveLogicalMessageContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\LoadHandlersBehavior.cs:line 45\n   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51   at NServiceBus.Pipeline.BehaviorChain`1.Invoke(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 36\n   at NServiceBus.Pipeline.PipelineExecutor.Execute[T](BehaviorChain`1 pipelineAction, T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 139\n   at NServiceBus.Pipeline.PipelineExecutor.InvokeLogicalMessagePipeline(LogicalMessage message) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 58\n   at NServiceBus.Unicast.Messages.ExecuteLogicalMessagesBehavior.Invoke(ReceivePhysicalMessageContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Messages\\ExecuteLogicalMessagesBehavior.cs:line 28\n   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 64\n   at NServiceBus.Pipeline.BehaviorChain`1.<>c__DisplayClass2.<InvokeNext>b__0() in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51\n   at NServiceBus.UnitOfWork.UnitOfWorkBehavior.Invoke(ReceivePhysicalMessageContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\UnitOfWork\\UnitOfWorkBehavior.cs:line 24 Receiver.Default@SIMON-MAC 2015-02-20 07:34:32:317585 Z"",
        ""TimeSent"": ""2015-02-20T07:34:24.0161520Z"",
        ""CriticalTime"": ""00:01:00"",
        ""ProcessingTime"": ""00:02:00"",
        ""DeliveryTime"": ""00:03:00"",
        ""ContentType"": ""text/xml"",
        ""BodyUrl"": ""/messages/dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5/body"",
        ""Body"": ""<?xml version=\""1.0\"" ?>\r\n<Messages xmlns:xsi=\""http://www.w3.org/2001/XMLSchema-instance\"" xmlns:xsd=\""http://www.w3.org/2001/XMLSchema\"" xmlns=\""http://tempuri.net/ServiceBus.Management.AcceptanceTests\"">\n<MyMessage>\n</MyMessage>\n</Messages>\r\n"",
        ""ContentLength"": 237,
        ""SearchableMessageType"": ""ServiceBus Management AcceptanceTests When_a_message_has_failed MyMessage"",
        ""IsSystemMessage"": false,
        ""MessageType"": ""ServiceBus.Management.AcceptanceTests.When_a_message_has_failed+MyMessage"",
        ""SendingEndpoint"": {
          ""$type"": ""ServiceControl.Contracts.Operations.EndpointDetails, ServiceControl.InternalContracts"",
          ""Name"": ""Sender.Default"",
          ""HostId"": ""fa2c6736-00d1-e72a-d991-47355265111d"",
          ""Host"": ""SIMON-MAC""
        },
        ""ReceivingEndpoint"": {
          ""$type"": ""ServiceControl.Contracts.Operations.EndpointDetails, ServiceControl.InternalContracts"",
          ""Name"": ""Receiver.Default"",
          ""HostId"": ""fa2c6736-00d1-e72a-d991-47355265111d"",
          ""Host"": ""SIMON-MAC""
        },
        ""ConversationId"": ""96e684f1-74d3-44e2-b1b3-a445008d48cc""
      },
      ""FailureDetails"": {
        ""AddressOfFailingEndpoint"": ""Receiver.Default@SIMON-MAC"",
        ""TimeOfFailure"": ""2015-02-20T07:34:32.3175850Z"",
        ""Exception"": {
          ""ExceptionType"": ""System.Exception"",
          ""Message"": ""Simulated exception"",
          ""Source"": ""ServiceBus.Management.AcceptanceTests"",
          ""StackTrace"": ""System.Exception: Simulated exception\n   at ServiceBus.Management.AcceptanceTests.When_a_message_has_failed.Receiver.MyMessageHandler.Handle(MyMessage message) in c:\\Particular\\ServiceControl\\src\\ServiceControl.AcceptanceTests\\MessageFailures\\When_a_message_has_failed.cs:line 129\n   at lambda_method(Closure , Object , Object )\n   at NServiceBus.Unicast.HandlerInvocationCache.Invoke(Object handler, Object message, Dictionary`2 dictionary) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\HandlerInvocationCache.cs:line 61\n   at NServiceBus.Unicast.HandlerInvocationCache.InvokeHandle(Object handler, Object message) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\HandlerInvocationCache.cs:line 22\n   at NServiceBus.Unicast.Behaviors.LoadHandlersBehavior.<Invoke>b__1(Object handlerInstance, Object message) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\LoadHandlersBehavior.cs:line 42\n   at NServiceBus.Unicast.Behaviors.InvokeHandlersBehavior.DispatchMessageToHandlersBasedOnType(IBuilder builder, LogicalMessage toHandle, MessageHandler messageHandler) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\InvokeHandlersBehavior.cs:line 63\n   at NServiceBus.Unicast.Behaviors.InvokeHandlersBehavior.Invoke(HandlerInvocationContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\InvokeHandlersBehavior.cs:line 29\n   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51   at NServiceBus.Pipeline.BehaviorChain`1.Invoke(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 36\n   at NServiceBus.Pipeline.PipelineExecutor.Execute[T](BehaviorChain`1 pipelineAction, T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 139\n   at NServiceBus.Pipeline.PipelineExecutor.InvokeHandlerPipeline(MessageHandler handler) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 68\n   at NServiceBus.Unicast.Behaviors.LoadHandlersBehavior.Invoke(ReceiveLogicalMessageContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\LoadHandlersBehavior.cs:line 45\n   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51   at NServiceBus.Pipeline.BehaviorChain`1.Invoke(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 36\n   at NServiceBus.Pipeline.PipelineExecutor.Execute[T](BehaviorChain`1 pipelineAction, T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 139\n   at NServiceBus.Pipeline.PipelineExecutor.InvokeLogicalMessagePipeline(LogicalMessage message) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 58\n   at NServiceBus.Unicast.Messages.ExecuteLogicalMessagesBehavior.Invoke(ReceivePhysicalMessageContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Messages\\ExecuteLogicalMessagesBehavior.cs:line 28\n   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 64\n   at NServiceBus.Pipeline.BehaviorChain`1.<>c__DisplayClass2.<InvokeNext>b__0() in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51\n   at NServiceBus.UnitOfWork.UnitOfWorkBehavior.Invoke(ReceivePhysicalMessageContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\UnitOfWork\\UnitOfWorkBehavior.cs:line 24""
        }
      },
      ""AttemptedAt"": ""2015-02-20T07:34:32.3175850Z"",
      ""MessageId"": ""dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5"",
      ""Headers"": {
        ""NServiceBus.MessageId"": ""dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5"",
        ""NServiceBus.CorrelationId"": ""dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5"",
        ""NServiceBus.OriginatingEndpoint"": ""Receiver.Default"",
        ""$.diagnostics.originating.hostid"": ""fa2c673600d1e72ad99147355265111d"",
        ""NServiceBus.MessageIntent"": ""Send"",
        ""NServiceBus.Version"": ""4.6.5"",
        ""NServiceBus.TimeSent"": ""2015-02-20 07:34:24:016152 Z"",
        ""NServiceBus.OriginatingMachine"": ""SIMON-MAC"",
        ""NServiceBus.ContentType"": ""text/xml"",
        ""NServiceBus.EnclosedMessageTypes"": ""ServiceBus.Management.AcceptanceTests.When_a_message_has_failed+MyMessage, ServiceBus.Management.AcceptanceTests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"",
        ""CorrId"": ""dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5\\0"",
        ""WinIdName"": """",
        ""NServiceBus.ConversationId"": ""96e684f1-74d3-44e2-b1b3-a445008d48cc"",
        ""NServiceBus.ProcessingMachine"": ""SIMON-MAC"",
        ""NServiceBus.ProcessingEndpoint"": ""Receiver.Default"",
        ""$.diagnostics.hostid"": ""fa2c673600d1e72ad99147355265111d"",
        ""$.diagnostics.hostdisplayname"": ""SIMON-MAC"",
        ""NServiceBus.ExceptionInfo.ExceptionType"": ""System.Exception"",
        ""NServiceBus.ExceptionInfo.Message"": ""Simulated exception"",
        ""NServiceBus.ExceptionInfo.Source"": ""ServiceBus.Management.AcceptanceTests"",
        ""NServiceBus.ExceptionInfo.StackTrace"": ""System.Exception: Simulated exception\n   at ServiceBus.Management.AcceptanceTests.When_a_message_has_failed.Receiver.MyMessageHandler.Handle(MyMessage message) in c:\\Particular\\ServiceControl\\src\\ServiceControl.AcceptanceTests\\MessageFailures\\When_a_message_has_failed.cs:line 129\n   at lambda_method(Closure , Object , Object )\n   at NServiceBus.Unicast.HandlerInvocationCache.Invoke(Object handler, Object message, Dictionary`2 dictionary) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\HandlerInvocationCache.cs:line 61\n   at NServiceBus.Unicast.HandlerInvocationCache.InvokeHandle(Object handler, Object message) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\HandlerInvocationCache.cs:line 22\n   at NServiceBus.Unicast.Behaviors.LoadHandlersBehavior.<Invoke>b__1(Object handlerInstance, Object message) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\LoadHandlersBehavior.cs:line 42\n   at NServiceBus.Unicast.Behaviors.InvokeHandlersBehavior.DispatchMessageToHandlersBasedOnType(IBuilder builder, LogicalMessage toHandle, MessageHandler messageHandler) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\InvokeHandlersBehavior.cs:line 63\n   at NServiceBus.Unicast.Behaviors.InvokeHandlersBehavior.Invoke(HandlerInvocationContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\InvokeHandlersBehavior.cs:line 29\n   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51   at NServiceBus.Pipeline.BehaviorChain`1.Invoke(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 36\n   at NServiceBus.Pipeline.PipelineExecutor.Execute[T](BehaviorChain`1 pipelineAction, T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 139\n   at NServiceBus.Pipeline.PipelineExecutor.InvokeHandlerPipeline(MessageHandler handler) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 68\n   at NServiceBus.Unicast.Behaviors.LoadHandlersBehavior.Invoke(ReceiveLogicalMessageContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Behaviors\\LoadHandlersBehavior.cs:line 45\n   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51   at NServiceBus.Pipeline.BehaviorChain`1.Invoke(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 36\n   at NServiceBus.Pipeline.PipelineExecutor.Execute[T](BehaviorChain`1 pipelineAction, T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 139\n   at NServiceBus.Pipeline.PipelineExecutor.InvokeLogicalMessagePipeline(LogicalMessage message) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\PipelineExecutor.cs:line 58\n   at NServiceBus.Unicast.Messages.ExecuteLogicalMessagesBehavior.Invoke(ReceivePhysicalMessageContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Unicast\\Messages\\ExecuteLogicalMessagesBehavior.cs:line 28\n   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51   at NServiceBus.Pipeline.BehaviorChain`1.InvokeNext(T context) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 64\n   at NServiceBus.Pipeline.BehaviorChain`1.<>c__DisplayClass2.<InvokeNext>b__0() in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\Pipeline\\BehaviorChain.cs:line 51\n   at NServiceBus.UnitOfWork.UnitOfWorkBehavior.Invoke(ReceivePhysicalMessageContext context, Action next) in c:\\BuildAgent\\work\\1b05a2fea6e4cd32\\src\\NServiceBus.Core\\UnitOfWork\\UnitOfWorkBehavior.cs:line 24"",
        ""NServiceBus.FailedQ"": ""Receiver.Default@SIMON-MAC"",
        ""NServiceBus.TimeOfFailure"": ""2015-02-20 07:34:32:317585 Z""
      },
      ""ReplyToAddress"": ""Receiver.Default@SIMON-MAC"",
      ""Recoverable"": true,
      ""CorrelationId"": ""dc6b6f1f-d1a3-45bb-8e7c-a445008d48c5"",
      ""MessageIntent"": 1
    }
  ],
  ""Status"": 1,
  ""UniqueMessageId"": ""474ca846-1f38-24db-3cf8-a147632de5ab""
}";
    }
}