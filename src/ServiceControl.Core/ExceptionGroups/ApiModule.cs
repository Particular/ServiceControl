namespace ServiceControl.ExceptionGroups
{
    using System;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/exceptionGroups"] = _ =>
            {
                var fakeResponse = new[]
                {
                    new ExceptionGroup { Id = "System.Sample", Title = "Sample", Count = 20, First = DateTime.UtcNow.AddHours(-1), Last = DateTime.UtcNow.AddHours(-0.5) }, 
                    new ExceptionGroup { Id = "System.ArgumentException", Title = "Argument", Count = 5, First = DateTime.UtcNow.AddHours(-4), Last = DateTime.UtcNow.AddHours(-1) }, 
                    new ExceptionGroup { Id = "System.NullReferenceException", Title = "Null", Count = 15, First = DateTime.UtcNow.AddHours(-3), Last = DateTime.UtcNow.AddHours(-2) }, 
                    new ExceptionGroup { Id = "System.SomethingElse", Title = "Something Else", Count = 25, First = DateTime.UtcNow.AddHours(-25), Last = DateTime.UtcNow.AddHours(-20) }, 

                };

                return Negotiate.WithModel(fakeResponse);
            };
        }
    }
}
