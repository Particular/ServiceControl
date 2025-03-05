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
    await Task.Delay(TimeSpan.FromSeconds(5));
}

return 0;
