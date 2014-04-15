namespace ServiceControl.UnitTests.Plugins.DebugSession
{
    using System;
    using System.IO;
    using NServiceBus;
    using NUnit.Framework;
    using Plugin.DebugSession;

    //using Plugin.DebugSession;

    [TestFixture]
    public class DebugSessionTests
    {
        [Test]
        public void Should_attach_debug_session_ids_on_outgoing_messages()
        {
            var debugSessionId = "MyDebugSession-1";

            var transportMessage = new TransportMessage();

            var mutator = new ApplyDebugSessionId
            {
                SessionId = debugSessionId
            };

            mutator.MutateOutgoing(null, transportMessage);

            Assert.AreEqual(debugSessionId, transportMessage.Headers["ServiceControl.DebugSessionId"]);
        }

      
        [Test]
        public void Should_initiate_the_mutator_with_the_active_session_id()
        {
            var debugSessionId = "MyDebugSession-1";
            
            File.Delete("ServiceControl.DebugSessionId.txt");
            File.WriteAllText("ServiceControl.DebugSessionId.txt", debugSessionId);

            Configure.With(new Type[] {})
                .DefaultBuilder();
            
            new NServiceBus.Features.DebugSession()
                .Initialize();

            Assert.AreEqual(debugSessionId, Configure.Instance.Builder.Build<ApplyDebugSessionId>().SessionId);
        }
    }
}