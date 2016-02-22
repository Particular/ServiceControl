﻿// ReSharper disable MemberCanBePrivate.Global
namespace ServiceBus.Management.Infrastructure.Installers.UrlAcl.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct HttpApiVersion
    {
        public ushort Major;
        public ushort Minor;
        public HttpApiVersion(ushort majorVersion, ushort minorVersion)
        {
            Major = majorVersion;
            Minor = minorVersion;
        }
    }
}
