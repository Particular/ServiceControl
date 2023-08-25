using System;
using System.Diagnostics;

static class Guard
{
    /// <summary>
    /// Checks for a condition; if the condition is false, raise exception with message
    /// </summary>
    public static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
            throw new InvalidOperationException(message);
        }
    }
}