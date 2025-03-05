if (args.Any(a => a == "fail"))
{
    throw new Exception("Fake exception");
}

if (args.Any(a => a == "non-zero-exit-code"))
{
    Console.Error.WriteLine("Fake non zero exit code message");

    return 3;
}

if (args.Any(a => a == "delay"))
{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
    await Task.Delay(TimeSpan.FromSeconds(5));
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
}

return 0;
