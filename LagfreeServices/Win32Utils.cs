using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LagfreeServices
{
    internal class Win32Utils
    {
        private class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public class SECURITY_ATTRIBUTES
            {
                public int nLength;
                public byte[] lpSecurityDescriptor;
                public int bInheritHandle;
            }

            public enum PROCESS_INFORMATION_CLASS
            {
                ProcessBasicInformation,
                ProcessQuotaLimits,
                ProcessIoCounters,
                ProcessVmCounters,
                ProcessTimes,
                ProcessBasePriority,
                ProcessRaisePriority,
                ProcessDebugPort,
                ProcessExceptionPort,
                ProcessAccessToken,
                ProcessLdtInformation,
                ProcessLdtSize,
                ProcessDefaultHardErrorMode,
                ProcessIoPortHandlers,
                ProcessPooledUsageAndLimits,
                ProcessWorkingSetWatch,
                ProcessUserModeIOPL,
                ProcessEnableAlignmentFaultFixup,
                ProcessPriorityClass,
                ProcessWx86Information,
                ProcessHandleCount,
                ProcessAffinityMask,
                ProcessPriorityBoost,
                ProcessDeviceMap,
                ProcessSessionInformation,
                ProcessForegroundInformation,
                ProcessWow64Information,
                ProcessImageFileName,
                ProcessLUIDDeviceMapsEnabled,
                ProcessBreakOnTermination,
                ProcessDebugObjectHandle,
                ProcessDebugFlags,
                ProcessHandleTracing,
                ProcessIoPriority,
                ProcessExecuteFlags,
                ProcessResourceManagement,
                ProcessCookie,
                ProcessImageInformation,
                ProcessCycleTime,
                ProcessPagePriority,
                ProcessInstrumentationCallback,
                ProcessThreadStackAllocation,
                ProcessWorkingSetWatchEx,
                ProcessImageFileNameWin32,
                ProcessImageFileMapping,
                ProcessAffinityUpdateMode,
                ProcessMemoryAllocationMode,
                MaxProcessInfoClass
            }


            public enum PROCESS_RIGHTS : uint
            {
                PROCESS_ALL_ACCESS = 0x1fffff,
                PROCESS_CREATE_PROCESS = 0x80,
                PROCESS_CREATE_THREAD = 2,
                PROCESS_DUP_HANDLE = 0x40,
                PROCESS_QUERY_INFORMATION = 0x400,
                PROCESS_QUERY_LIMITED_INFORMATION = 0x1000,
                PROCESS_SET_INFORMATION = 0x200,
                PROCESS_SET_QUOTA = 0x100,
                PROCESS_SET_SESSIONID = 4,
                PROCESS_SUSPEND_RESUME = 0x800,
                PROCESS_TERMINATE = 1,
                PROCESS_VM_OPERATION = 8,
                PROCESS_VM_READ = 0x10,
                PROCESS_VM_WRITE = 0x20
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct STARTUPINFO
            {
                public int cb;
                public string lpReserved;
                public string lpDesktop;
                public string lpTitle;
                public int dwX;
                public int dwY;
                public int dwXSize;
                public int dwYSize;
                public int dwXCountChars;
                public int dwYCountChars;
                public int dwFillAttribute;
                public int dwFlags;
                public short wShowWindow;
                public short cbReserved2;
                public IntPtr lpReserved2;
                public IntPtr hStdInput;
                public IntPtr hStdOutput;
                public IntPtr hStdError;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct PROCESS_INFORMATION
            {
                public IntPtr hProcess;
                public IntPtr hThread;
                public int dwProcessId;
                public int dwThreadId;
            }


            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESS_BASIC_INFORMATION
            {
                public int ExitStatus;
                public int PebBaseAddress;
                public int AffinityMask;
                public int BasePriority;
                public int UniqueProcessId;
                public int InheritedFromUniqueProcessId;
                public int Size
                {
                    get
                    {
                        return 0x18;
                    }
                }
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(PROCESS_RIGHTS dwDesiredAccess, bool bInheritHandle, int dwProcessId);
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool GetProcessIoCounters(SafeProcessHandle hProcess, ref IO_COUNTERS lpIoCounters);

            [DllImport("ntdll.dll", SetLastError = true)]
            public static extern int NtQueryInformationProcess(SafeProcessHandle processHandle, PROCESS_INFORMATION_CLASS processInformationClass, ref IntPtr processInformation, int processInformationLength, out int returnLength);
            [DllImport("ntdll.dll", SetLastError = true)]
            public static extern int NtSetInformationProcess(SafeProcessHandle processHandle, PROCESS_INFORMATION_CLASS processInformationClass, IntPtr processInformation, int processInformationLength);

            public static bool NT_SUCCESS(int NTSTATUS) => NTSTATUS >= 0;


            // For CreateFile to get handle to drive
            public const uint GENERIC_READ = 0x80000000;
            public const uint GENERIC_WRITE = 0x40000000;
            public const uint FILE_SHARE_READ = 0x00000001;
            public const uint FILE_SHARE_WRITE = 0x00000002;
            public const uint OPEN_EXISTING = 3;
            public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

            // CreateFile to get handle to drive
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern SafeFileHandle CreateFileW([MarshalAs(UnmanagedType.LPWStr)]string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

            // For control codes
            public const uint FILE_DEVICE_MASS_STORAGE = 0x0000002d;
            public const uint IOCTL_STORAGE_BASE = FILE_DEVICE_MASS_STORAGE;
            public const uint FILE_DEVICE_CONTROLLER = 0x00000004;
            public const uint IOCTL_SCSI_BASE = FILE_DEVICE_CONTROLLER;
            public const uint METHOD_BUFFERED = 0;
            public const uint FILE_ANY_ACCESS = 0;
            public const uint FILE_READ_ACCESS = 0x00000001;
            public const uint FILE_WRITE_ACCESS = 0x00000002;

            public static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access)
                => ((DeviceType << 16) | (Access << 14) | (Function << 2) | Method);

            // For DeviceIoControl to check no seek penalty
            public const uint StorageDeviceSeekPenaltyProperty = 7;
            public const uint PropertyStandardQuery = 0;

            [StructLayout(LayoutKind.Sequential)]
            public struct STORAGE_PROPERTY_QUERY
            {
                public uint PropertyId;
                public uint QueryType;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
                public byte[] AdditionalParameters;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct DEVICE_SEEK_PENALTY_DESCRIPTOR
            {
                public uint Version;
                public uint Size;
                [MarshalAs(UnmanagedType.U1)]
                public bool IncursSeekPenalty;
            }

            // DeviceIoControl to check no seek penalty
            [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, ref STORAGE_PROPERTY_QUERY lpInBuffer, uint nInBufferSize, ref DEVICE_SEEK_PENALTY_DESCRIPTOR lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

            // For DeviceIoControl to check nominal media rotation rate
            public const uint ATA_FLAGS_DATA_IN = 0x02;

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1049:TypesThatOwnNativeResourcesShouldBeDisposable")]
            [StructLayout(LayoutKind.Sequential)]
            public struct ATA_PASS_THROUGH_EX
            {
                public ushort Length;
                public ushort AtaFlags;
                public byte PathId;
                public byte TargetId;
                public byte Lun;
                public byte ReservedAsUchar;
                public uint DataTransferLength;
                public uint TimeOutValue;
                public uint ReservedAsUlong;
                public IntPtr DataBufferOffset;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
                public byte[] PreviousTaskFile;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
                public byte[] CurrentTaskFile;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct ATAIdentifyDeviceQuery
            {
                public ATA_PASS_THROUGH_EX header;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
                public ushort[] data;
            }

            // DeviceIoControl to check nominal media rotation rate
            [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, ref ATAIdentifyDeviceQuery lpInBuffer, uint nInBufferSize, ref ATAIdentifyDeviceQuery lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
            
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetProcessWorkingSetSize(SafeProcessHandle hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        // Method for nominal media rotation rate
        // (Administrative privilege is required)
        public static bool HasNominalMediaRotationRate(int DeviceId)
        {
            using (SafeFileHandle hDrive = NativeMethods.CreateFileW(@"\\.\PhysicalDrive" + DeviceId,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE, // Administrative privilege is required
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, NativeMethods.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero))
            {

                if (hDrive == null || hDrive.IsInvalid) throw GetWin32Exception();

                uint IOCTL_ATA_PASS_THROUGH = NativeMethods.CTL_CODE(NativeMethods.IOCTL_SCSI_BASE, 0x040b, NativeMethods.METHOD_BUFFERED,
                    NativeMethods.FILE_READ_ACCESS | NativeMethods.FILE_WRITE_ACCESS); // From ntddscsi.h

                NativeMethods.ATAIdentifyDeviceQuery id_query = new NativeMethods.ATAIdentifyDeviceQuery()
                {
                    data = new ushort[256]
                };
                id_query.header.Length = (ushort)Marshal.SizeOf(id_query.header);
                id_query.header.AtaFlags = (ushort)NativeMethods.ATA_FLAGS_DATA_IN;
                id_query.header.DataTransferLength = (uint)(id_query.data.Length * 2); // Size of "data" in bytes
                id_query.header.TimeOutValue = 3; // Sec
                id_query.header.DataBufferOffset = Marshal.OffsetOf(typeof(NativeMethods.ATAIdentifyDeviceQuery), "data");
                id_query.header.PreviousTaskFile = new byte[8];
                id_query.header.CurrentTaskFile = new byte[8];
                id_query.header.CurrentTaskFile[6] = 0xec; // ATA IDENTIFY DEVICE


                if (NativeMethods.DeviceIoControl(hDrive, IOCTL_ATA_PASS_THROUGH, ref id_query, (uint)Marshal.SizeOf(id_query), ref id_query, (uint)Marshal.SizeOf(id_query), out uint retval_size, IntPtr.Zero))
                {
                    // Word index of nominal media rotation rate
                    // (1 means non-rotate device)
                    const int kNominalMediaRotRateWordIndex = 217;

                    return id_query.data[kNominalMediaRotRateWordIndex] != 1;
                }
                else throw GetWin32Exception();
            }
        }

        public static Win32Exception GetWin32Exception() => new Win32Exception(Marshal.GetLastWin32Error());

        public static int GetIOPriority(SafeProcessHandle hProcess)
        {
            IntPtr ptr = new IntPtr();
            if (!NativeMethods.NT_SUCCESS(NativeMethods.NtQueryInformationProcess(hProcess, NativeMethods.PROCESS_INFORMATION_CLASS.ProcessIoPriority, ref ptr, sizeof(int), out int returnLength)))
                throw GetWin32Exception();
            return ptr.ToInt32();
        }

        public static IO_COUNTERS GetIOCounters(SafeProcessHandle hProcess)
        {
            IO_COUNTERS ret = new IO_COUNTERS();
            if (!NativeMethods.GetProcessIoCounters(hProcess, ref ret)) throw GetWin32Exception();
            return ret;
        }

        public static void SetIOPriority(SafeProcessHandle hProcess, int newPrio)
        {
            IntPtr hGlobal = IntPtr.Zero;
            try
            {
                hGlobal = Marshal.AllocHGlobal(new IntPtr(sizeof(int)));
                Marshal.Copy(BitConverter.GetBytes(newPrio), 0, hGlobal, 4);
                if (!NativeMethods.NT_SUCCESS(NativeMethods.NtSetInformationProcess(hProcess, NativeMethods.PROCESS_INFORMATION_CLASS.ProcessIoPriority, hGlobal, IntPtr.Size)))
                    throw GetWin32Exception();
            }
            finally { if (hGlobal != IntPtr.Zero) Marshal.FreeHGlobal(hGlobal); }
        }

        public static void TrimProcessWorkingSet(SafeProcessHandle hProcess)
        {
            if (!NativeMethods.SetProcessWorkingSetSize(hProcess, new IntPtr(-1), new IntPtr(-1)))
                throw GetWin32Exception();
        }
    }
}
