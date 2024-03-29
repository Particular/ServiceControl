﻿namespace ServiceControl.Management.PowerShell
{
    using System;
    using System.Security.Principal;

    public class Account
    {
        public static void TestIfAdmin()
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                return;
            }

            throw new Exception("You must have administrative permissions to use this method");
        }
    }
}