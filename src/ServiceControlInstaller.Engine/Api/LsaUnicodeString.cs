// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.Engine.Api
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    [StructLayout(LayoutKind.Sequential)]
    internal struct LsaUnicodeString
    {
        internal LsaUnicodeString(string inputString)
        {
            if (inputString == null)
            {
                Buffer = IntPtr.Zero;
                Length = 0;
                MaximumLength = 0;
            }
            else
            {
                Buffer = Marshal.StringToHGlobalAuto(inputString);
                Length = (ushort)(inputString.Length * UnicodeEncoding.CharSize);
                MaximumLength = (ushort)((inputString.Length + 1) * UnicodeEncoding.CharSize);
            }
        }

        internal ushort Length;
        internal ushort MaximumLength;
        internal IntPtr Buffer;
    }
}