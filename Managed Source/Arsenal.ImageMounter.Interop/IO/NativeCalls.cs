using Arsenal.ImageMounter.Extensions;
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO;

public static class NativeCalls
{
#if NETCOREAPP
    public static IntPtr CrtDllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!OperatingSystem.IsWindows() &&
            (libraryName.StartsWith("msvcr", StringComparison.OrdinalIgnoreCase) ||
            libraryName.StartsWith("msvcp", StringComparison.OrdinalIgnoreCase) ||
            libraryName.Equals("ntdll", StringComparison.OrdinalIgnoreCase) ||
            libraryName.Equals("advapi32", StringComparison.OrdinalIgnoreCase) ||
            libraryName.Equals("kernel32", StringComparison.OrdinalIgnoreCase) ||
            libraryName.Equals("crtdll", StringComparison.OrdinalIgnoreCase)))
        {
            return NativeLibrary.Load("c", assembly, searchPath);
        }

        return IntPtr.Zero;
    }
#endif

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    private static class WindowsAPI
    {
        [DllImport("ntdll", CharSet = CharSet.Auto)]
        public static extern unsafe int NtQueryVolumeInformationFile(SafeFileHandle FileHandle, out IoStatusBlock IoStatusBlock, void* FsInformation, int FsInformationLength, FsInformationClass FsInformationClass);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        public static extern unsafe bool DeviceIoControl(SafeFileHandle FileHandle, uint IoControlCode, void* InBuffer, int InBufferSize, void* OutBuffer, int OutBufferSize, out int BytesReturned, void* overlapped);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        public static extern bool DeviceIoControl(SafeFileHandle FileHandle, uint IoControlCode, in byte InBuffer, int InBufferSize, out byte OutBuffer, int OutBufferSize, out int BytesReturned, IntPtr overlapped);

        [DllImport("advapi32", CharSet = CharSet.Auto, EntryPoint = "SystemFunction036", SetLastError = true)]
        public static extern byte RtlGenRandom(out byte buffer, int length);

        [DllImport("advapi32", CharSet = CharSet.Auto, EntryPoint = "SystemFunction036", SetLastError = true)]
        public static extern unsafe byte RtlGenRandom(void* buffer, int length);
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP

    public static void GenRandom(Span<byte> bytes) =>
        RandomNumberGenerator.Fill(bytes);

    public static T GenRandom<T>() where T : unmanaged
    {
        T value = default;
        GenRandom(BufferExtensions.AsBytes(ref value));
        return value;
    }

#else

    public static void GenRandom(Span<byte> bytes)
    {
        if (WindowsAPI.RtlGenRandom(out bytes[0], bytes.Length) == 0)
        {
            throw new Exception("Random generation failed");
        }
    }

    public static unsafe T GenRandom<T>() where T : unmanaged
    {
        T value;
        if (WindowsAPI.RtlGenRandom(&value, sizeof(T)) == 0)
        {
            throw new Exception("Random generation failed");
        }
        return value;
    }

#endif

    public static sbyte GenRandomSByte() => GenRandom<sbyte>();

    public static short GenRandomInt16() => GenRandom<short>();

    public static int GenRandomInt32() => GenRandom<int>();

    public static long GenRandomInt64() => GenRandom<long>();

    public static byte GenRandomByte() => GenRandom<byte>();

    public static ushort GenRandomUInt16() => GenRandom<ushort>();

    public static uint GenRandomUInt32() => GenRandom<uint>();

    public static ulong GenRandomUInt64() => GenRandom<ulong>();

    public static Guid GenRandomGuid() => GenRandom<Guid>();

    public static byte[] GenRandomBytes(int count)
    {
        var bytes = new byte[count];
        GenRandom(bytes);
        return bytes;
    }

    public static uint GenerateDiskSignature() => (GenRandomUInt32() | 0x80808081U) & 0xFEFEFEFFU;

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static unsafe FILE_FS_FULL_SIZE_INFORMATION? GetVolumeSizeInformation(SafeFileHandle SafeFileHandle)
    {
        var value = default(FILE_FS_FULL_SIZE_INFORMATION);

        var status = WindowsAPI.NtQueryVolumeInformationFile(SafeFileHandle,
                                                             out var _,
                                                             &value,
                                                             sizeof(FILE_FS_FULL_SIZE_INFORMATION),
                                                             FsInformationClass.FileFsFullSizeInformation);

        if (status < 0)
        {
            return default;
        }

        return value;
    }

    private struct TrimDiskRegionInData
    {
        public unsafe TrimDiskRegionInData(DEVICE_DATA_SET_RANGE range,
                                           DEVICE_DATA_MANAGEMENT_SET_ACTION action,
                                           int flags)
        {
            Attributes = new(action,
                             flags,
                             0,
                             0,
                             sizeof(TrimDiskRegionInData) - sizeof(DEVICE_DATA_SET_RANGE),
                             sizeof(DEVICE_DATA_SET_RANGE));

            Range = range;
        }

        public DEVICE_MANAGE_DATA_SET_ATTRIBUTES Attributes { get; }

        public DEVICE_DATA_SET_RANGE Range { get; }
    }

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static unsafe void TrimDiskRange(SafeFileHandle disk, long startingOffset, ulong lengthInBytes)
    {
        var request = new TrimDiskRegionInData(new(startingOffset, lengthInBytes),
                                               DEVICE_DATA_MANAGEMENT_SET_ACTION.DeviceDsmAction_Trim,
                                               0);

        if (!WindowsAPI.DeviceIoControl(disk,
                                        NativeConstants.IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES,
                                        &request,
                                        sizeof(TrimDiskRegionInData),
                                        null,
                                        0,
                                        out var _,
                                        null))
        {
            throw new Win32Exception();
        }
    }

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static unsafe void InitializeDisk(SafeFileHandle disk, PARTITION_STYLE PartitionStyle)
    {
        switch (PartitionStyle)
        {
            case PARTITION_STYLE.MBR:
                {
                    var mbr = new CREATE_DISK_MBR(partitionStyle: PARTITION_STYLE.MBR,
                                                  diskSignature: GenerateDiskSignature());

                    if (!WindowsAPI.DeviceIoControl(disk,
                                                    NativeConstants.IOCTL_DISK_CREATE_DISK,
                                                    &mbr,
                                                    sizeof(CREATE_DISK_MBR),
                                                    null,
                                                    0,
                                                    out var _,
                                                    null))
                    {
                        throw new Win32Exception();
                    }

                    break;
                }

            case PARTITION_STYLE.GPT:
                {
                    var gpt = new CREATE_DISK_GPT(partitionStyle: PARTITION_STYLE.GPT,
                                                  diskId: Guid.NewGuid(),
                                                  maxPartitionCount: 128);

                    if (!WindowsAPI.DeviceIoControl(disk,
                                                    NativeConstants.IOCTL_DISK_CREATE_DISK,
                                                    &gpt,
                                                    sizeof(CREATE_DISK_GPT),
                                                    null,
                                                    0,
                                                    out var _,
                                                    null))
                    {
                        throw new Win32Exception();
                    }

                    break;
                }

            default:
                {
                    throw new ArgumentOutOfRangeException(nameof(PartitionStyle));
                }
        }
    }
}

