// ReSharper disable StringLiteralTypo

namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System;
    using System.Runtime.InteropServices;

    internal static class HttpApi
    {
        [DllImport("httpapi.dll", SetLastError = true)]
        internal static extern ErrorCode HttpQueryServiceConfiguration(
            IntPtr ServiceIntPtr,
            HttpServiceConfigId ConfigId,
            IntPtr pInputConfigInfo,
            int InputConfigInfoLength,
            IntPtr pOutputConfigInfo,
            int OutputConfigInfoLength,
            // ReSharper disable once OptionalParameterRefOut
            [Optional] out int pReturnLength,
            IntPtr pOverlapped);

        [DllImport("httpapi.dll", SetLastError = true)]
        internal static extern ErrorCode HttpSetServiceConfiguration(
            IntPtr ServiceIntPtr,
            HttpServiceConfigId ConfigId,
            IntPtr pConfigInformation,
            int ConfigInformationLength,
            IntPtr pOverlapped);

        [DllImport("httpapi.dll", SetLastError = true)]
        internal static extern ErrorCode HttpDeleteServiceConfiguration(
            IntPtr ServiceIntPtr,
            HttpServiceConfigId ConfigId,
            IntPtr pConfigInformation,
            int ConfigInformationLength,
            IntPtr pOverlapped);

        [DllImport("httpapi.dll", SetLastError = true)]
        internal static extern ErrorCode HttpInitialize(
            HttpApiVersion Version,
            uint Flags,
            IntPtr pReserved);

        [DllImport("httpapi.dll", SetLastError = true)]
        internal static extern ErrorCode HttpTerminate(
            uint Flags,
            IntPtr pReserved);
    }
}