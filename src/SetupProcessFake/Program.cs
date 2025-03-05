if (args.Any(a => a == "fail"))
{
    throw new Exception("Fake exception");
}