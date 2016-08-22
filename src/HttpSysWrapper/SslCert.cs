
namespace HttpApiWrapper
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Principal;
    using HttpApiWrapper.Api;

    public class SslCert
    {
        public static void ApplyCertificate(int port, byte[] hash, IPAddress ipAddress = null)
        {
            var ipPortHandle = GetIpPortHandle(port, ipAddress);
            var configSslKey = new HttpServiceConfigSslKey { IPPort = ipPortHandle.AddrOfPinnedObject() };
            var handleHash = GCHandle.Alloc(hash, GCHandleType.Pinned);
            var configSslParam = new HttpServiceConfigSslParam
            {
                AppId = Guid.NewGuid(),
                DefaultCertCheckMode = 0,
                DefaultFlags = 0,
                DefaultRevocationFreshnessTime = 0,
                DefaultRevocationUrlRetrievalTimeout = 0,
                SslCertStoreName = StoreName.My.ToString(),
                SslHash = handleHash.AddrOfPinnedObject(),
                SslHashLength = hash.Length,
            };

            var configSslSet = new HttpServiceConfigSslSet
            {
                ParamDesc = configSslParam,
                KeyDesc = configSslKey
            };

            var pInputConfigInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(HttpServiceConfigSslSet)));

            Marshal.StructureToPtr(configSslSet, pInputConfigInfo, false);
            try
            {
                var retVal = HttpApi.HttpInitialize(HttpApiConstants.Version1, HttpApiConstants.InitializeConfig, IntPtr.Zero);
                ThrowWin32ExceptionIfError(retVal);
                retVal = HttpApi.HttpSetServiceConfiguration(IntPtr.Zero,
                    HttpServiceConfigId.HttpServiceConfigSSLCertInfo,
                    pInputConfigInfo,
                    Marshal.SizeOf(configSslSet),
                    IntPtr.Zero);

                if (retVal == ErrorCode.AlreadyExists)
                {
                    retVal = HttpApi.HttpDeleteServiceConfiguration(IntPtr.Zero,
                        HttpServiceConfigId.HttpServiceConfigSSLCertInfo,
                        pInputConfigInfo,
                        Marshal.SizeOf(configSslSet),
                        IntPtr.Zero);

                    if (retVal == ErrorCode.Success)
                    {
                        retVal = HttpApi.HttpSetServiceConfiguration(IntPtr.Zero,
                            HttpServiceConfigId.HttpServiceConfigSSLCertInfo,
                            pInputConfigInfo,
                            Marshal.SizeOf(configSslSet),
                            IntPtr.Zero);
                    }
                }
                ThrowWin32ExceptionIfError(retVal);
            }
            finally
            {
                Marshal.FreeHGlobal(pInputConfigInfo);
                if (ipPortHandle.IsAllocated)
                    ipPortHandle.Free();
                HttpApi.HttpTerminate(HttpApiConstants.InitializeConfig, IntPtr.Zero);
            }
        }

        public static void ClearCertificate(int port, IPAddress ipAddress = null)
        {

            var ipPortHandle = GetIpPortHandle(port, ipAddress);

            var configSslKey = new HttpServiceConfigSslKey { IPPort = ipPortHandle.AddrOfPinnedObject() };

            var configSslParam = new HttpServiceConfigSslParam
            {
                AppId = Guid.NewGuid(),
                DefaultCertCheckMode = 0,
                DefaultFlags = 0,
                DefaultRevocationFreshnessTime = 0,
                DefaultRevocationUrlRetrievalTimeout = 0,
                SslCertStoreName = StoreName.My.ToString(),
            };

            var configSslSet = new HttpServiceConfigSslSet
            {
                ParamDesc = configSslParam,
                KeyDesc = configSslKey
            };

            var pInputConfigInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(HttpServiceConfigSslSet)));

            Marshal.StructureToPtr(configSslSet, pInputConfigInfo, false);
            try
            {
                var retVal = HttpApi.HttpInitialize(HttpApiConstants.Version1, HttpApiConstants.InitializeConfig, IntPtr.Zero);
                ThrowWin32ExceptionIfError(retVal);
                retVal = HttpApi.HttpDeleteServiceConfiguration(IntPtr.Zero,
                    HttpServiceConfigId.HttpServiceConfigSSLCertInfo,
                    pInputConfigInfo,
                    Marshal.SizeOf(configSslSet),
                    IntPtr.Zero);
                if (retVal == ErrorCode.FileNotFound)
                    return; //No cert to remove

                ThrowWin32ExceptionIfError(retVal);
            }
            finally
            {
                Marshal.FreeHGlobal(pInputConfigInfo);
                if (ipPortHandle.IsAllocated)
                    ipPortHandle.Free();
                HttpApi.HttpTerminate(HttpApiConstants.InitializeConfig, IntPtr.Zero);
            }
        }

        public static X509Certificate2 GetCertificate(int port, IPAddress ipAddress = null)
        {
            var thumbPrint = GetThumbprint(port, ipAddress);
            if (thumbPrint == null) return null;
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var results = store.Certificates.Find(X509FindType.FindByThumbprint,thumbPrint, false);
                return (results.Count > 0) ? results[0] : null;
            }
            finally
            {
                store.Close();
            }
        }

        public static string GetThumbprint(int port, IPAddress ipAddress = null)
        {
            var ipPortHandle = GetIpPortHandle(port, ipAddress);
            var configInfoQuery = new HttpServiceConfigSslQuery
            {
                KeyDesc = new HttpServiceConfigSslKey
                {
                    IPPort = ipPortHandle.AddrOfPinnedObject()
                },
                QueryDesc = HttpServiceConfigQueryType.HttpServiceConfigQueryExact
            };
            var pInputConfigInfo = Marshal.AllocHGlobal(Marshal.SizeOf(configInfoQuery));
            Marshal.StructureToPtr(configInfoQuery, pInputConfigInfo, false);

            var pOutputConfigInfo = IntPtr.Zero;
            var returnLength = 0;
            try
            {
                var retVal = HttpApi.HttpInitialize(HttpApiConstants.Version1, HttpApiConstants.InitializeConfig, IntPtr.Zero);
                ThrowWin32ExceptionIfError(retVal);

                var inputConfigInfoSize = Marshal.SizeOf(configInfoQuery);
                retVal = HttpApi.HttpQueryServiceConfiguration(IntPtr.Zero,
                    HttpServiceConfigId.HttpServiceConfigSSLCertInfo,
                    pInputConfigInfo,
                    inputConfigInfoSize,
                    pOutputConfigInfo,
                    returnLength,
                    out returnLength,
                    IntPtr.Zero);

                if (retVal == ErrorCode.FileNotFound)
                    return null;

                if (retVal == ErrorCode.InsufficientBuffer)
                {
                    pOutputConfigInfo = Marshal.AllocHGlobal(returnLength);
                    retVal = HttpApi.HttpQueryServiceConfiguration(IntPtr.Zero,
                        HttpServiceConfigId.HttpServiceConfigSSLCertInfo,
                        pInputConfigInfo,
                        inputConfigInfoSize,
                        pOutputConfigInfo,
                        returnLength,
                        out returnLength,
                        IntPtr.Zero);
                }

                ThrowWin32ExceptionIfError(retVal);
                var outputConfigInfo = (HttpServiceConfigSslSet) Marshal.PtrToStructure(pOutputConfigInfo, typeof(HttpServiceConfigSslSet));
                var hash = new byte[outputConfigInfo.ParamDesc.SslHashLength];
                Marshal.Copy(outputConfigInfo.ParamDesc.SslHash, hash, 0, hash.Length);
                return hash.ToHexString();
            }
            finally
            {
                Marshal.FreeHGlobal(pOutputConfigInfo);
                if (ipPortHandle.IsAllocated)
                    ipPortHandle.Free();
                HttpApi.HttpTerminate(HttpApiConstants.InitializeConfig, IntPtr.Zero);
            }
        }

        static void ThrowWin32ExceptionIfError(ErrorCode retVal)
        {
            if (retVal != ErrorCode.Success)
            {
                throw new Win32Exception(Convert.ToInt32(retVal));
            }
        }

        static GCHandle GetIpPortHandle(int port, IPAddress address)
        {
            var ipEndPoint = new IPEndPoint(address ?? IPAddress.Any, port);
            var socketAddress = ipEndPoint.Serialize();
            var socketBytes = new byte[socketAddress.Size];
            var handleSocketAddress = GCHandle.Alloc(socketBytes, GCHandleType.Pinned);

            for (var i = 0; i < socketAddress.Size; ++i)
            {
                socketBytes[i] = socketAddress[i];
            }
            return handleSocketAddress;
        }

        public static void MigrateToHttp(string url)
        {
            var builder = new UriBuilder(url)
            {
                Scheme = Uri.UriSchemeHttp
            };
            var existingUrlAcl = UrlReservation.GetAll().SingleOrDefault(p => p.Url.Equals(url));
            var users = existingUrlAcl?.UserSids.ToArray() ?? new[] { new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null) };
            var httpReservation = new UrlReservation(builder.Uri.ToString(), users);
            httpReservation.Create();
            ClearCertificate(httpReservation.Port);
        }

        public static void MigrateToHttps(string url, byte[] hash)
        {
            var builder = new UriBuilder(url)
            {
                Scheme = Uri.UriSchemeHttps
            };

            var existingUrlAcl = UrlReservation.GetAll().SingleOrDefault(p => p.Url.Equals(url));
            var users = existingUrlAcl?.UserSids.ToArray() ?? new[] {new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null)};
            var sslRes = new UrlReservation(builder.Uri.ToString(), users);
            sslRes.Create();
            ApplyCertificate(sslRes.Port, hash);
        }
    }
}
