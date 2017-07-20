namespace ServiceControlInstaller.Engine.Services
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using ServiceControlInstaller.Engine.Api;

    internal class ServiceRecoveryHelper : IDisposable
    {
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManager")]
        static extern IntPtr OpenSCManager(
            string machineName,
            string databaseName,
            SERVICE_CONTROL_ACCESS_RIGHTS desiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle")]
        static extern int CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", EntryPoint = "OpenService")]
        static extern IntPtr OpenService(
            IntPtr hSCManager,
            string serviceName,
            SERVICE_ACCESS_RIGHTS desiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2")]
        static extern int ChangeServiceConfig2(
            IntPtr hService,
            ServiceConfig2InfoLevel dwInfoLevel,
            IntPtr lpInfo);

        IntPtr SCManager;
        bool disposed;

        IntPtr OpenService(string serviceName, SERVICE_ACCESS_RIGHTS desiredAccess)
        {
            var service = OpenService(
                SCManager,
                serviceName,
                desiredAccess);

            if (service == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open the requested Service.");
            }

            return service;
        }

        public static void SetRecoveryOptions(string serviceName)
        {
            try
            {
                using (var helper = new ServiceRecoveryHelper())
                {
                    helper.SetRestartOnFailure(serviceName);
                }
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
            }
        }

        ServiceRecoveryHelper()
        {
            SCManager = OpenSCManager(
                null,
                null,
                SERVICE_CONTROL_ACCESS_RIGHTS.SC_MANAGER_CONNECT);

            if (SCManager == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open Service Control Manager.");
            }
        }

        void SetRestartOnFailure(string serviceName)
        {
            const int actionCount = 2;
            const uint delay = 60000;

            var service = IntPtr.Zero;
            var failureActionsPtr = IntPtr.Zero;
            var actionPtr = IntPtr.Zero;

            try
            {
                service = OpenService(serviceName, SERVICE_ACCESS_RIGHTS.SERVICE_CHANGE_CONFIG | SERVICE_ACCESS_RIGHTS.SERVICE_START);
                actionPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SC_ACTION)) * actionCount);

                var action1 = new SC_ACTION
                {
                    Type = SC_ACTION_TYPE.SC_ACTION_RESTART,
                    Delay = delay
                };

                Marshal.StructureToPtr(action1, actionPtr, false);

                var action2 = new SC_ACTION
                {
                    Type = SC_ACTION_TYPE.SC_ACTION_NONE,
                    Delay = delay
                };
                Marshal.StructureToPtr(action2, (IntPtr) ((long) actionPtr + Marshal.SizeOf(typeof(SC_ACTION))), false);

                var failureActions = new SERVICE_FAILURE_ACTIONS
                {
                    dwResetPeriod = 0,
                    cActions = actionCount,
                    lpsaActions = actionPtr
                };

                failureActionsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SERVICE_FAILURE_ACTIONS)));
                Marshal.StructureToPtr(failureActions, failureActionsPtr, false);
                var changeResult = ChangeServiceConfig2(
                    service,
                    ServiceConfig2InfoLevel.SERVICE_CONFIG_FAILURE_ACTIONS,
                    failureActionsPtr);

                if (changeResult == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to change the Service configuration.");
                }
            }
            finally
            {
                // Clean up
                if (failureActionsPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(failureActionsPtr);
                }

                if (actionPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(actionPtr);
                }

                if (service != IntPtr.Zero)
                {
                    CloseServiceHandle(service);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (SCManager != IntPtr.Zero)
                {
                    CloseServiceHandle(SCManager);
                    SCManager = IntPtr.Zero;
                }
            }
            disposed = true;
        }

        ~ServiceRecoveryHelper()
        {
            Dispose(false);
        }
    }
}