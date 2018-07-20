// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.Engine.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }
}