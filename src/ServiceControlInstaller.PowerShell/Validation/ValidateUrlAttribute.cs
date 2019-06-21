namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    class ValidateUrlAttribute : ValidateArgumentsAttribute
    {
        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            Validate(arguments as string);
            Validate(arguments as string[]);
        }

        void Validate(string[] vals)
        {
            foreach (var val in vals)
            {
                Validate(val);
            }
        }

        void Validate(string strVal)
        {
            if (strVal == null)
            {
                return;
            }
            
            if (!Uri.TryCreate(strVal, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Not a Uri");
            }

            if (uri.IsFile)
            {
                throw new ArgumentException("Not a Url");
            }

        }
    }
}