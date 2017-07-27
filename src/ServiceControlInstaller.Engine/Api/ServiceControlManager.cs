
namespace ServiceControlInstaller.Engine.Api
{
    #pragma warning disable 169
    #pragma warning disable 414

    using System;
    using System.Runtime.InteropServices;

    struct SERVICE_FAILURE_ACTIONS
    {
        [MarshalAs(UnmanagedType.U4)]

        public UInt32 dwResetPeriod;

        [MarshalAs(UnmanagedType.LPStr)]
        public String lpRebootMsg;
        [MarshalAs(UnmanagedType.LPStr)]
        public String lpCommand;
        [MarshalAs(UnmanagedType.U4)]
        public UInt32 cActions;
        public IntPtr lpsaActions;
    }

    [Flags]
    enum SERVICE_ACCESS_RIGHTS
    {
        SERVICE_QUERY_CONFIG = 0x0001, // Required to call the QueryServiceConfig and QueryServiceConfig2 functions to query the service configuration.
        SERVICE_CHANGE_CONFIG = 0x0002, // Required to call the ChangeServiceConfig or ChangeServiceConfig2 function to change the service configuration. Because this grants the caller the right to change the executable file that the system runs, it should be granted only to administrators.
        SERVICE_QUERY_STATUS = 0x0004, // Required to call the QueryServiceStatusEx function to ask the service control manager about the status of the service.
        SERVICE_ENUMERATE_DEPENDENTS = 0x0008, // Required to call the EnumDependentServices function to enumerate all the services dependent on the service.
        SERVICE_START = 0x0010, // Required to call the StartService function to start the service.
        SERVICE_STOP = 0x0020, // Required to call the ControlService function to stop the service.
        SERVICE_PAUSE_CONTINUE = 0x0040, // Required to call the ControlService function to pause or continue the service.
        SERVICE_INTERROGATE = 0x0080, // Required to call the ControlService function to ask the service to report its status immediately.
        SERVICE_USER_DEFINED_CONTROL = 0x0100, // Required to call the ControlService function to specify a user-defined control code.
        SERVICE_ALL_ACCESS = 0xF01FF // Includes STANDARD_RIGHTS_REQUIRED in addition to all access rights in this table.
    }

    [Flags]
    public enum SERVICE_CONTROL_ACCESS_RIGHTS
    {
        SC_MANAGER_CONNECT = 0x0001, // Required to connect to the service control manager.
        SC_MANAGER_CREATE_SERVICE = 0x0002, // Required to call the CreateService function to create a service object and add it to the database.
        SC_MANAGER_ENUMERATE_SERVICE = 0x0004, // Required to call the EnumServicesStatusEx function to list the services that are in the database.
        SC_MANAGER_LOCK = 0x0008, // Required to call the LockServiceDatabase function to acquire a lock on the database.
        SC_MANAGER_QUERY_LOCK_STATUS = 0x0010, // Required to call the QueryServiceLockStatus function to retrieve the lock status information for the database
        SC_MANAGER_MODIFY_BOOT_CONFIG = 0x0020, // Required to call the NotifyBootConfigStatus function.
        SC_MANAGER_ALL_ACCESS = 0xF003F // Includes STANDARD_RIGHTS_REQUIRED, in addition to all access rights in this table.
    }

    enum SC_ACTION_TYPE : uint
    {
        SC_ACTION_NONE = 0x00000000, // No action.
        SC_ACTION_RESTART = 0x00000001, // Restart the service.
        SC_ACTION_REBOOT = 0x00000002, // Reboot the computer.
        SC_ACTION_RUN_COMMAND = 0x00000003 // Run a command.
    }

    enum ServiceConfig2InfoLevel
    {
        SERVICE_CONFIG_DESCRIPTION = 0x00000001, // The lpBuffer parameter is a pointer to a SERVICE_DESCRIPTION structure.
        SERVICE_CONFIG_FAILURE_ACTIONS = 0x00000002 // The lpBuffer parameter is a pointer to a SERVICE_FAILURE_ACTIONS structure.
    }

    struct SC_ACTION
    {
        [MarshalAs(UnmanagedType.U4)]
        public SC_ACTION_TYPE Type;
        [MarshalAs(UnmanagedType.U4)]
        public UInt32 Delay;
    }

    #pragma warning restore 169
    #pragma warning restore 414

}