// ReSharper disable UnusedMember.Local
namespace ServiceControlInstaller.Engine.Accounts
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Security.Principal;
    using System.Text;
    using ServiceControlInstaller.Engine.Api;

    internal class Lsa
    {
        public static string[] GetPrivileges(string identity)
        {
            var sidPtr = GetIdentitySid(identity);
            var hPolicy = GetLsaPolicyHandle();
            var rightsPtr = IntPtr.Zero;

            try
            {
                var privileges = new List<string>();

                uint rightsCount;
                var result = LsaEnumerateAccountRights(hPolicy, sidPtr, out rightsPtr, out rightsCount);
                var win32ErrorCode = LsaNtStatusToWinError(result);
                // the user has no privileges
                if (win32ErrorCode == StatusObjectNameNotFound)
                {
                    return new string[0];
                }
                HandleLsaResult(result);

                var myLsaus = new LsaUnicodeString();
                for (ulong i = 0; i < rightsCount; i++)
                {
                    var itemAddr = new IntPtr(rightsPtr.ToInt64() + (long)(i * (ulong)Marshal.SizeOf(myLsaus)));
                    myLsaus = (LsaUnicodeString)Marshal.PtrToStructure(itemAddr, myLsaus.GetType());
                    var cvt = new char[myLsaus.Length / UnicodeEncoding.CharSize];
                    Marshal.Copy(myLsaus.Buffer, cvt, 0, myLsaus.Length / UnicodeEncoding.CharSize);
                    var thisRight = new string(cvt);
                    privileges.Add(thisRight);
                }
                return privileges.ToArray();
            }
            finally
            {
                Marshal.FreeHGlobal(sidPtr);
                var result = LsaClose(hPolicy);
                HandleLsaResult(result);
                result = LsaFreeMemory(rightsPtr);
                HandleLsaResult(result);
            }
        }

        public static void RevokePrivileges(string identity, string[] privileges)
        {
            var sidPtr = GetIdentitySid(identity);
            var hPolicy = GetLsaPolicyHandle();

            try
            {
                var currentPrivileges = GetPrivileges(identity);
                if (currentPrivileges.Length == 0)
                {
                    return;
                }
                var lsaPrivileges = StringsToLsaStrings(privileges);
                var result = LsaRemoveAccountRights(hPolicy, sidPtr, false, lsaPrivileges, (uint)lsaPrivileges.Length);
                HandleLsaResult(result);
            }
            finally
            {
                Marshal.FreeHGlobal(sidPtr);
                var result = LsaClose(hPolicy);
                HandleLsaResult(result);
            }
        }

        public static void GrantPrivileges(string identity, string[] privileges)
        {
            var sidPtr = GetIdentitySid(identity);
            var hPolicy = GetLsaPolicyHandle();

            try
            {
                var lsaPrivileges = StringsToLsaStrings(privileges);
                var result = LsaAddAccountRights(hPolicy, sidPtr, lsaPrivileges, (uint)lsaPrivileges.Length);
                HandleLsaResult(result);
            }
            finally
            {
                Marshal.FreeHGlobal(sidPtr);
                var result = LsaClose(hPolicy);
                HandleLsaResult(result);
            }
        }

        const uint POLICY_VIEW_LOCAL_INFORMATION = 0x00000001;
        const uint POLICY_VIEW_AUDIT_INFORMATION = 0x00000002;
        const uint POLICY_GET_PRIVATE_INFORMATION = 0x00000004;
        const uint POLICY_TRUST_ADMIN = 0x00000008;
        const uint POLICY_CREATE_ACCOUNT = 0x00000010;
        const uint POLICY_CREATE_SECRET = 0x00000014;
        const uint POLICY_CREATE_PRIVILEGE = 0x00000040;
        const uint POLICY_SET_DEFAULT_QUOTA_LIMITS = 0x00000080;
        const uint POLICY_SET_AUDIT_REQUIREMENTS = 0x00000100;
        const uint POLICY_AUDIT_LOG_ADMIN = 0x00000200;
        const uint POLICY_SERVER_ADMIN = 0x00000400;
        const uint POLICY_LOOKUP_NAMES = 0x00000800;
        const uint POLICY_NOTIFICATION = 0x00001000;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern uint LsaAddAccountRights(
            IntPtr PolicyHandle,
            IntPtr AccountSid,
            LsaUnicodeString[] UserRights,
            uint CountOfRights);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        static extern uint LsaClose(IntPtr ObjectHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern uint LsaEnumerateAccountRights(IntPtr PolicyHandle,
            IntPtr AccountSid,
            out IntPtr UserRights,
            out uint CountOfRights);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern uint LsaFreeMemory(IntPtr pBuffer);

        [DllImport("advapi32.dll")]
        static extern int LsaNtStatusToWinError(uint status);

        [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
        static extern uint LsaOpenPolicy(ref LsaUnicodeString SystemName, ref LsaObjectAttributes ObjectAttributes, uint DesiredAccess, out IntPtr PolicyHandle);

        [DllImport("advapi32.dll", SetLastError = true, PreserveSig = true)]
        static extern uint LsaRemoveAccountRights(
            IntPtr PolicyHandle,
            IntPtr AccountSid,
            [MarshalAs(UnmanagedType.U1)]
            bool AllRights,
            LsaUnicodeString[] UserRights,
            uint CountOfRights);

        static IntPtr GetIdentitySid(string identity)
        {
            var sid = new NTAccount(identity).Translate(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (sid == null)
            {
                throw new ArgumentException($"Account {identity} not found.");
            }
            var sidBytes = new byte[sid.BinaryLength];
            sid.GetBinaryForm(sidBytes, 0);
            var sidPtr = Marshal.AllocHGlobal(sidBytes.Length);
            Marshal.Copy(sidBytes, 0, sidPtr, sidBytes.Length);
            return sidPtr;
        }

        static IntPtr GetLsaPolicyHandle()
        {
            var computerName = Environment.MachineName;
            IntPtr hPolicy;
            var objectAttributes = new LsaObjectAttributes()
            {
                Length = 0,
                RootDirectory = IntPtr.Zero,
                Attributes = 0,
                SecurityDescriptor = IntPtr.Zero,
                SecurityQualityOfService = IntPtr.Zero
            };

            const uint accessMask = POLICY_CREATE_SECRET | POLICY_LOOKUP_NAMES | POLICY_VIEW_LOCAL_INFORMATION;
            var machineNameLsa = new LsaUnicodeString(computerName);
            var result = LsaOpenPolicy(ref machineNameLsa, ref objectAttributes, accessMask, out hPolicy);
            HandleLsaResult(result);
            return hPolicy;
        }

        const int StatusSuccess = 0x0;
        const int StatusObjectNameNotFound = 0x00000002;
        const int StatusAccessDenied = 0x00000005;
        const int StatusInvalidHandle = 0x00000006;
        const int StatusUnsuccessful = 0x0000001F;
        const int StatusInvalidParameter = 0x00000057;
        const int StatusNoSuchPrivilege = 0x00000521;
        const int StatusInvalidServerState = 0x00000548;
        const int StatusInternalDbError = 0x00000567;
        const int StatusInsufficientResources = 0x000005AA;

        static readonly Dictionary<int, string> ErrorMessages = new Dictionary<int, string>
                                {
                                    {StatusObjectNameNotFound, "Object name not found. An object in the LSA policy database was not found. The object may have been specified either by SID or by name, depending on its type."},
                                    {StatusAccessDenied, "Access denied. Caller does not have the appropriate access to complete the operation."},
                                    {StatusInvalidHandle, "Invalid handle. Indicates an object or RPC handle is not valid in the context used."},
                                    {StatusUnsuccessful, "Unsuccessful. Generic failure, such as RPC connection failure."},
                                    {StatusInvalidParameter, "Invalid parameter. One of the parameters is not valid."},
                                    {StatusNoSuchPrivilege, "No such privilege. Indicates a specified privilege does not exist."},
                                    {StatusInvalidServerState, "Invalid server state. Indicates the LSA server is currently disabled."},
                                    {StatusInternalDbError, "Internal database error. The LSA database contains an internal inconsistency."},
                                    {StatusInsufficientResources, "Insufficient resources. There are not enough system resources (such as memory to allocate buffers) to complete the call."}
                                };

        static void HandleLsaResult(uint returnCode)
        {
            var win32ErrorCode = LsaNtStatusToWinError(returnCode);

            if (win32ErrorCode == StatusSuccess)
                return;

            if (ErrorMessages.ContainsKey(win32ErrorCode))
            {
                throw new Win32Exception(win32ErrorCode, ErrorMessages[win32ErrorCode]);
            }

            throw new Win32Exception(win32ErrorCode);
        }

        static LsaUnicodeString[] StringsToLsaStrings(string[] privileges)
        {
            var lsaPrivileges = new LsaUnicodeString[privileges.Length];
            for (var idx = 0; idx < privileges.Length; ++idx)
            {
                lsaPrivileges[idx] = new LsaUnicodeString(privileges[idx]);
            }
            return lsaPrivileges;
        }
    }
}