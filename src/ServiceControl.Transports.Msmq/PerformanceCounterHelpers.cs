namespace ServiceControl.MSMQ.DLQMonitor
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Win32;

    // Inspired by https://wojciechkulik.pl/csharp/get-a-performancecounter-using-english-name
    static class PerformanceCounterHelpers
    {
        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern UInt32 PdhLookupPerfNameByIndex(string szMachineName, uint dwNameIndex, StringBuilder szNameBuffer, ref uint pcchNameBufferSize);

        public static LocalizedCounterCategoryAndName GetCategoryAndName(string englishCategoryName, string englishCounterName)
        {
            // Get list of counters
            const string perfCountersKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Perflib\009";
            var englishNames = Registry.GetValue(perfCountersKey, "Counter", null) as string[];

            var localizedCategoryName = GetNameByIndex(FindNameId(englishNames, englishCategoryName));
            var localizedCounterName = GetNameByIndex(FindNameId(englishNames, englishCounterName));

            return new LocalizedCounterCategoryAndName {Category = localizedCategoryName, Name = localizedCounterName};
        }

        static int FindNameId(IReadOnlyList<string> englishNames, string name)
        {
            for (var i = 1; i < englishNames.Count; i += 2)
            {
                if (englishNames[i] == name)
                {
                    return int.Parse(englishNames[i - 1]);
                }
            }

            return -1;
        }

        static string GetNameByIndex(int id)
        {
            if (id < 0)
            {
                return null;
            }

            var buffer = new StringBuilder(1024);
            var bufSize = (uint)buffer.Capacity;
            var ret = PdhLookupPerfNameByIndex(null, (uint)id, buffer, ref bufSize);
            return ret == 0 && buffer.Length != 0 ? buffer.ToString() : null;
        }

        public struct LocalizedCounterCategoryAndName
        {
            public string Category;
            public string Name;
        }
    }
}