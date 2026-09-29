using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

//https://learn.microsoft.com/en-us/answers/questions/2113423/how-to-disable-a-device-from-net
// Couldn't get this to work on Windows 11. But it would be interesting to know how to do it properly

namespace MakeWindowsHelloGreatAgain
{
    internal class SetupApi
    {

        private const uint DICS_DISABLE = 0x00000001;
        private const uint DICS_ENABLE = 0x00000002;
        private const uint DICS_PROPCHANGE = 0x00000003;
        private const int DICS_FLAG_GLOBAL = 0x00000001;
        private const int DIGCF_PRESENT = 0x00000002;
        private const int DIGCF_ALLCLASSES = 0x00000004;
        private const int DI_QUIETINSTALL = 0x00800000;

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid,
            IntPtr Enumerator,
            IntPtr hwndParent,
            uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet,
            uint MemberIndex,
            ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            StringBuilder InstanceId,
            uint InstanceIdSize,
            ref uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiSetClassInstallParams(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            ref SP_CLASSINSTALL_HEADER ClassInstallParams,
            uint ClassInstallParamsSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiCallClassInstaller(
            uint InstallFunction,
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_CLASSINSTALL_HEADER
        {
            public uint cbSize;
            public uint InstallFunction;
        }
        private struct SP_PROPCHANGE_PARAMS
        {
            public SP_CLASSINSTALL_HEADER ClassInstallHeader;
            public uint StateChange;
            public uint Scope;
            public uint HwProfile;

            // Stepping into the SetupDiSetClassInstallParams, it looks like the code checks size to be at least 612 (264h) bytes. 
            // Why, I don't effing know.
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
            public byte[] largeBuffer;
        }

        public static bool EnableOrDisableDevice(Guid classGuid,string deviceInstanceId, bool bEnable)
        {
            IntPtr deviceInfoSet = SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
            if (deviceInfoSet == IntPtr.Zero)
            {
                return false;
            }
            // Annoyingly, we seem to have to enumerate almost all devices as the class guid seems to encompass pretty much all PCI devices, not just USB webcams or the like.
            try
            {
                SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA();
                deviceInfoData.cbSize = (uint)Marshal.SizeOf(deviceInfoData);
                for (uint i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref deviceInfoData); i++)
                {
                    uint requiredSize = 0;
                    StringBuilder deviceInstanceFound = new StringBuilder(256);
                    if(!SetupDiGetDeviceInstanceId(deviceInfoSet, ref deviceInfoData, deviceInstanceFound, 256, ref requiredSize))
                    {
                        return false;
                    }
                    //Debug.WriteLine($"Device Instance ID: {deviceInstanceFound.ToString()}");

                    if(deviceInstanceFound.ToString() != deviceInstanceId)
                    {
                        continue;
                    }

                    SP_PROPCHANGE_PARAMS PropChangeParams = new SP_PROPCHANGE_PARAMS();
                    PropChangeParams.ClassInstallHeader.cbSize = (uint)Marshal.SizeOf(PropChangeParams.ClassInstallHeader);
                    PropChangeParams.ClassInstallHeader.InstallFunction = (bEnable ? DICS_ENABLE : DICS_DISABLE) | DI_QUIETINSTALL;
                    PropChangeParams.StateChange = DICS_PROPCHANGE;// bEnable ? DICS_ENABLE : DICS_DISABLE;
                    PropChangeParams.Scope = DICS_FLAG_GLOBAL;
                    PropChangeParams.HwProfile = 0;
                    PropChangeParams.largeBuffer = new byte[1024]; 

                    if (!SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData, ref PropChangeParams.ClassInstallHeader, (uint)Marshal.SizeOf(PropChangeParams)))
                    {
                        Debug.WriteLine($"Failed to set class install params: {Marshal.GetLastWin32Error()}");
                        return false;
                    }

                    if (!SetupDiCallClassInstaller(PropChangeParams.ClassInstallHeader.InstallFunction, deviceInfoSet, ref deviceInfoData))
                    {
                        Debug.WriteLine($"Failed to call Class Installer: {Marshal.GetLastWin32Error()}");
                        return false;
                    }

                }
            }
            finally
            {
                // Cleanup code here if necessary
            }
            return true;
        }

    }
}
