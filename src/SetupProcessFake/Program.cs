if (args.Any(a => a == "fail"))
{
    throw new Exception("Fake exception");
}

if (args.Any(a => a == "non-zero-exit-code"))
{
    return 3;
}

return 0;
