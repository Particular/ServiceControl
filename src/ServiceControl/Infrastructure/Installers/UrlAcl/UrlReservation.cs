namespace ServiceBus.Management.Infrastructure.Installers
{ 
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text.RegularExpressions;
    using ServiceBus.Management.Infrastructure.Installers.UrlAcl.Api;


    public class UrlReservation
    {
        static Regex urlPattern = new Regex(@"^(?<protocol>https?)://(?<hostname>[^:/]+):?(?<port>\d{0,5})/?(?<virtual>[^:]*)/$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        const int GENERIC_EXECUTE = 536870912;
        
        public string Url {get; }

        List<SecurityIdentifier> securityIdentifiers = new List<SecurityIdentifier>();

        public UrlReservation(string url, params SecurityIdentifier[] securityIdentifiers)
        {
            if (!url.EndsWith("/"))
            {
                throw new ArgumentException("UrlAcl is invalid - it must have a trailing /");
            }

            var matchResults = urlPattern.Match(url);
            if (matchResults.Success)
            {
                HTTPS = (matchResults.Groups["protocol"].Value.Equals("https", StringComparison.OrdinalIgnoreCase));
                HostName = matchResults.Groups["hostname"].Value;
                if (string.IsNullOrEmpty(matchResults.Groups["port"].Value))
                {
                    Port = (HTTPS) ? 443 : 80;
                }
                else
                {
                    Port = int.Parse(matchResults.Groups["port"].Value);
                }
                VirtualDirectory = matchResults.Groups["virtual"].Value;
                Url = url;
            }
            else
            {
                throw new ArgumentException("UrlAcl is invalid");
            }

            if (securityIdentifiers != null)
            {
                this.securityIdentifiers.AddRange(securityIdentifiers);
            }
        }

        public ReadOnlyCollection<string> Users
        {
            get
            {
                var users = securityIdentifiers.Select(sec => ((NTAccount) sec.Translate(typeof(NTAccount))).Value).ToList();
                return new ReadOnlyCollection<string>(users);
            }
        }

        public void AddUser(string user)
        {
            var account = new NTAccount(user);
            var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
            AddSecurityIdentifier(sid);
        }

        public void AddSecurityIdentifier(SecurityIdentifier sid)
        {
            securityIdentifiers.Add(sid);
        }

        public void ClearUsers()
        {
            securityIdentifiers.Clear();
        }

        public void Create()
        {
            Create(this);
        }

        public void Delete()
        {
            Delete(this);
        }

        public int Port { get; private set; }
        public string HostName { get; private set; }
        public string VirtualDirectory { get; private set; }
        public bool HTTPS { get; }

        public static ReadOnlyCollection<UrlReservation> GetAll()
        {
            var reservations = new List<UrlReservation>();

            var retVal =  HttpApi.HttpInitialize(HttpApiConstants.Version1, HttpApiConstants.InitializeConfig, IntPtr.Zero);
            
            if (retVal == ErrorCode.Success)
            {
                var inputConfigInfoSet = new  HttpServiceConfigUrlAclQuery
                {
                    QueryDesc = HttpServiceConfigQueryType.HttpServiceConfigQueryNext
                };

                var i = 0;
                while (retVal == 0)
                {
                    inputConfigInfoSet.Token = (uint)i;
                    var pInputConfigInfo = Marshal.AllocHGlobal((Marshal.SizeOf(typeof(HttpServiceConfigUrlAclQuery))));
                    Marshal.StructureToPtr(inputConfigInfoSet, pInputConfigInfo, false);
                    
                    var pOutputConfigInfo = Marshal.AllocHGlobal(0);
                    var returnLength = 0;
                    retVal = HttpApi.HttpQueryServiceConfiguration(IntPtr.Zero,
                        HttpServiceConfigId.HttpServiceConfigUrlAclInfo, 
                        pInputConfigInfo,
                        Marshal.SizeOf(inputConfigInfoSet),
                        pOutputConfigInfo,
                        returnLength,
                        out returnLength,
                        IntPtr.Zero);

                    if (retVal == ErrorCode.InsufficientBuffer)
                    {
                        Marshal.FreeHGlobal(pOutputConfigInfo);
                        pOutputConfigInfo = Marshal.AllocHGlobal(Convert.ToInt32(returnLength));

                        retVal = HttpApi.HttpQueryServiceConfiguration(IntPtr.Zero,
                        HttpServiceConfigId.HttpServiceConfigUrlAclInfo, 
                        pInputConfigInfo,
                        Marshal.SizeOf(inputConfigInfoSet),
                        pOutputConfigInfo,
                        returnLength,
                        out returnLength,
                        IntPtr.Zero);
                    }
                 
                    if ( retVal == ErrorCode.Success)
                    {
                        var outputConfigInfo = (HttpServiceConfigUrlAclSet) Marshal.PtrToStructure(pOutputConfigInfo, typeof(HttpServiceConfigUrlAclSet));
                        var rev = new UrlReservation(outputConfigInfo.KeyDesc.UrlPrefix, SecurityIdentifiersFromSecurityDescriptor(outputConfigInfo.ParamDesc.StringSecurityDescriptor).ToArray());
                        reservations.Add(rev);
                    }
                    Marshal.FreeHGlobal(pOutputConfigInfo);
                    Marshal.FreeHGlobal(pInputConfigInfo);
                    i++;
                }
                
                retVal = HttpApi.HttpTerminate(HttpApiConstants.InitializeConfig, IntPtr.Zero);
                
            }

            if (retVal != ErrorCode.Success)
            {
                throw new Win32Exception(Convert.ToInt32(retVal));
            }
            return new ReadOnlyCollection<UrlReservation>(reservations);
        }

        public static void Create(UrlReservation urlReservation)
        {
            if (urlReservation.securityIdentifiers.Count == 0)
            {
                throw new Exception("No SecurityIdentifiers have been assigned to the URLACL");
            }

            var sddl = GenerateSecurityDescriptor(urlReservation.securityIdentifiers);
            reserveURL(urlReservation.Url, sddl);
        }

        private static void reserveURL(string networkURL, string securityDescriptor)
        {
            
            var retVal = HttpApi.HttpInitialize(HttpApiConstants.Version1, HttpApiConstants.InitializeConfig, IntPtr.Zero);
            if (retVal == ErrorCode.Success)
            {
                var keyDesc = new HttpServiceConfigUrlAclKey(networkURL);
                var paramDesc = new HttpServiceConfigUrlAclParam(securityDescriptor);

                var inputConfigInfoSet = new HttpServiceConfigUrlAclSet
                {
                    KeyDesc = keyDesc,
                    ParamDesc = paramDesc
                };

                var pInputConfigInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(HttpServiceConfigUrlAclSet)));
                Marshal.StructureToPtr(inputConfigInfoSet, pInputConfigInfo, false);

                retVal = HttpApi.HttpSetServiceConfiguration(IntPtr.Zero,
                    HttpServiceConfigId.HttpServiceConfigUrlAclInfo, 
                    pInputConfigInfo,
                    Marshal.SizeOf(inputConfigInfoSet),
                    IntPtr.Zero);

                if (ErrorCode.AlreadyExists == retVal)  
                {
                    retVal = HttpApi.HttpDeleteServiceConfiguration(IntPtr.Zero,HttpServiceConfigId.HttpServiceConfigUrlAclInfo, pInputConfigInfo,Marshal.SizeOf(inputConfigInfoSet),IntPtr.Zero);

                    if (ErrorCode.Success == retVal)
                    {
                        retVal = HttpApi.HttpSetServiceConfiguration(IntPtr.Zero,HttpServiceConfigId.HttpServiceConfigUrlAclInfo,pInputConfigInfo,Marshal.SizeOf(inputConfigInfoSet),IntPtr.Zero);
                    }
                }

                Marshal.FreeHGlobal(pInputConfigInfo);
                HttpApi.HttpTerminate(HttpApiConstants.InitializeConfig, IntPtr.Zero);
            }

            if (retVal != ErrorCode.Success)
            {
                throw new Win32Exception(Convert.ToInt32(retVal));
            }
        }
        
        public static void Delete(UrlReservation urlReservation)
        {
            var securityDescriptor = GenerateSecurityDescriptor(urlReservation.securityIdentifiers);
            FreeURL(urlReservation.Url, securityDescriptor);
        }

        private static void FreeURL(string networkURL, string securityDescriptor)
        {
            var retVal = HttpApi.HttpInitialize(HttpApiConstants.Version1, HttpApiConstants.InitializeConfig, IntPtr.Zero);
            if (ErrorCode.Success == retVal)
            {
                var urlAclKey = new HttpServiceConfigUrlAclKey(networkURL);
                var urlAclParam = new HttpServiceConfigUrlAclParam(securityDescriptor);

                var urlAclSet = new HttpServiceConfigUrlAclSet
                {
                    KeyDesc = urlAclKey,
                    ParamDesc = urlAclParam
                };

                var configInformation = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(HttpServiceConfigUrlAclSet)));
                Marshal.StructureToPtr(urlAclSet, configInformation, false);
                var configInformationSize = Marshal.SizeOf(urlAclSet);
                retVal = HttpApi.HttpDeleteServiceConfiguration(IntPtr.Zero,
                    HttpServiceConfigId.HttpServiceConfigUrlAclInfo,
                    configInformation,
                    configInformationSize,
                    IntPtr.Zero);

                Marshal.FreeHGlobal(configInformation);
                HttpApi.HttpTerminate(HttpApiConstants.InitializeConfig, IntPtr.Zero);
            }

            if (ErrorCode.Success != retVal)
            {
                throw new Win32Exception(Convert.ToInt32(retVal));
            }
        }

        private static IEnumerable<SecurityIdentifier> SecurityIdentifiersFromSecurityDescriptor(string securityDescriptor)
        {
            var commonSecurityDescriptor = new CommonSecurityDescriptor(false, false, securityDescriptor);
            var discretionaryAcl = commonSecurityDescriptor.DiscretionaryAcl;
            return discretionaryAcl.Cast<CommonAce>().Select(ace => ace.SecurityIdentifier);
        }

        private static DiscretionaryAcl GetDiscretionaryAcl(List<SecurityIdentifier> securityIdentifiers)
        {
            var discretionaryAcl = new DiscretionaryAcl(false, false, 16);
            foreach (var securityIdentifier in securityIdentifiers)
            {
                discretionaryAcl.AddAccess(AccessControlType.Allow, securityIdentifier, GENERIC_EXECUTE, InheritanceFlags.None, PropagationFlags.None);
            }

            return discretionaryAcl;
        }

        private static CommonSecurityDescriptor GetSecurityDescriptor(List<SecurityIdentifier> securityIdentifiers)
        {
            var discretionaryAcl = GetDiscretionaryAcl(securityIdentifiers);
            var securityDescriptor = new CommonSecurityDescriptor(false, false,
                        ControlFlags.GroupDefaulted |
                        ControlFlags.OwnerDefaulted |
                        ControlFlags.DiscretionaryAclPresent,
                        null, null, null, discretionaryAcl);
            return securityDescriptor;
        }

        private static string GenerateSecurityDescriptor(List<SecurityIdentifier> securityIdentifiers)
        {
            return GetSecurityDescriptor(securityIdentifiers).GetSddlForm(AccessControlSections.Access);
        }

        public byte[] ToDiscretionaryAclBytes()
        {
            var discretionaryAcl = GetDiscretionaryAcl(securityIdentifiers);
            var bytes = new byte[discretionaryAcl.BinaryLength];
            discretionaryAcl.GetBinaryForm(bytes, 0);
            return bytes;
        }

        public byte[] ToSystemAclBytes()
        {
            var systemAcl = new SystemAcl(false, false, 0);
            var bytes = new byte[systemAcl.BinaryLength];
            systemAcl.GetBinaryForm(bytes, 0);
            return bytes;
        }
    }
}