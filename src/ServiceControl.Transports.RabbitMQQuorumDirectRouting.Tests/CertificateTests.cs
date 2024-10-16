//namespace ServiceControl.Transport.Tests;

//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Threading.Tasks;
//using NUnit.Framework;
//using Particular.Approvals;
//using Transports.RabbitMQ;
//using ServiceControl.Transports.BrokerThroughput;
//using ServiceControl.Transports;
//using NServiceBus;

//[TestFixture]
//class CertificateTests : TransportTestFixture
//{
//    [Test]
//    public async Task Passing_CertPath_In_ConnectionString_Sets_The_ClientCertificate_Correctly()
//    {
//        var connectionString = "host=localhost;user=guest;pass=guest;certPath='c:\\certpathlocation\\cert.xml'";
//        var transportSettings = new TransportSettings { ConnectionString = connectionString };

//        var endpointConfiguration = new EndpointConfiguration("Test");
//        var customizer = new RabbitMQQuorumConventionalRoutingTransportCustomization();
//        customizer.CustomizeAuditEndpoint(endpointConfiguration, transportSettings);

//        Assert.That(endpointConfiguration)

//        Assert.That(success, Is.False);
//        Approver.Verify(diagnostics,
//            s => Regex.Replace(s, "defaulted to using \"\\w*\" username", "defaulted to using \"xxxxx\" username",
//                RegexOptions.Multiline));
//    }
//}