namespace ServiceControl.Infrastructure.SignalR
{
    using System.Globalization;
    using Microsoft.AspNet.SignalR;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class Configure : NServiceBus.INeedInitialization
    {
        public void Customize( NServiceBus.BusConfiguration configuration )
        {
            //WARN: needs a fix
            //var serializer = new JsonNetSerializer(new JsonSerializerSettings
            //{
            //    ContractResolver = new CustomSignalRContractResolverBecauseOfIssue500InSignalR(),
            //    Formatting = Formatting.None,
            //    NullValueHandling = NullValueHandling.Ignore,
            //    Converters = { 
            //        new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.RoundtripKind }, 
            //        new StringEnumConverter { CamelCaseText = true }
            //    }
            //});

            //var settings = new JsonSerializerSettings
            //{
            //    ContractResolver = new SignalRContractResolver()
            //};
            //var serializer = JsonSerializer.Create( settings );

            //GlobalHost.DependencyResolver.Register(typeof(IJsonSerializer), () => serializer); 
        }
    }
}
