using System;
using System.Messaging;
using System.Runtime.InteropServices;

static class MsmqExtensions
{
    /// <remarks>
    /// Source: http://functionalflow.co.uk/blog/2008/08/27/counting-the-number-of-messages-in-a-message-queue-in/
    /// </remarks>
    public static int GetCount(this MessageQueue self)
    {
        var props = new Win32.MQMGMTPROPS {cProp = 1};
        try
        {
            props.aPropID = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(props.aPropID, Win32.PROPID_MGMT_QUEUE_MESSAGE_COUNT);

            props.aPropVar = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Win32.MQPROPVariant)));
            Marshal.StructureToPtr(new Win32.MQPROPVariant {vt = Win32.VT_NULL}, props.aPropVar, false);

            props.status = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(props.status, 0);

            var result = Win32.MQMgmtGetInfo(null, "queue=" + self.FormatName, ref props);
            if (result != 0)
            {
                throw new InvalidOperationException($"Unable to retrieve queue information (error: {result:x8}");
            }

            if (Marshal.ReadInt32(props.status) != 0)
            {
                return -1;
            }

            var propVar = (Win32.MQPROPVariant)Marshal.PtrToStructure(props.aPropVar, typeof(Win32.MQPROPVariant));

            return propVar.vt != Win32.VT_UI4
                ? 0
                : Convert.ToInt32(propVar.ulVal);
        }
        finally
        {
            Marshal.FreeHGlobal(props.aPropID);
            Marshal.FreeHGlobal(props.aPropVar);
            Marshal.FreeHGlobal(props.status);
        }
    }

    static class Win32
    {
        [DllImport("mqrt.dll")]
        internal static extern uint MQMgmtGetInfo([MarshalAs(UnmanagedType.BStr)] string computerName, [MarshalAs(UnmanagedType.BStr)] string objectName, ref MQMGMTPROPS mgmtProps);

        public const byte VT_NULL = 1;
        public const byte VT_UI4 = 19;
        public const int PROPID_MGMT_QUEUE_MESSAGE_COUNT = 7;

        //size must be 16
        [StructLayout(LayoutKind.Sequential)]
        internal struct MQPROPVariant
        {
            public byte vt; //0
            public byte spacer; //1
            public short spacer2; //2
            public int spacer3; //4
            public uint ulVal; //8
            public int spacer4; //12
        }

        //size must be 16 in x86 and 28 in x64
        [StructLayout(LayoutKind.Sequential)]
        internal struct MQMGMTPROPS
        {
            public uint cProp;
            public IntPtr aPropID;
            public IntPtr aPropVar;
            public IntPtr status;
        }
    }
}