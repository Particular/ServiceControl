using System;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using JustSaying.Messaging.Middleware;
using Microsoft.Extensions.Hosting.Internal;

namespace JustSaying.Sample.Restaurant.KitchenConsole;

public class ExceptionLoggerMiddleware : MiddlewareBase<HandleMessageContext, bool>
{
    protected override Task<bool> RunInnerAsync(HandleMessageContext context, Func<CancellationToken, Task<bool>> func, CancellationToken stoppingToken)
    {
        try
        {
            return func(stoppingToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw ex;
        }
    }
}