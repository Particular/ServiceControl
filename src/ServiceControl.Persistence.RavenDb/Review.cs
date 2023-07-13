using System;
using System.Diagnostics;

static class Review
{
    //[Obsolete] TODO: This helper needs to be removed in the final release
    public static void Assert(string message)
    {
        if (Debugger.IsAttached)
        {
            Debugger.Break();
        }
        else
        {
            throw new NotImplementedException(message);
        }
    }
}