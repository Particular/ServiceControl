﻿namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct HttpServiceConfigSslSet
    {
        public HttpServiceConfigSslKey KeyDesc;

        public HttpServiceConfigSslParam ParamDesc;
    }
}