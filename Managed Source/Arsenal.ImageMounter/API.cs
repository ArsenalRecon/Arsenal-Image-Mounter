// '''' API.vb
// '''' API for manipulating flag values, issuing SCSI bus rescans and similar
// '''' tasks.
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
// ''''

using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter;

/// <summary>
/// API for manipulating flag values, issuing SCSI bus rescans, manage write filter driver and similar tasks.
/// </summary>
[ComVisible(false)]
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public static partial class API
{
    public static Version OSVersion { get; } = NativeFileIO.GetOSVersion().Version;

    public static string Kernel { get; private set; } = GetKernelName();

    public static bool HasStorPort { get; private set; }

    private static string GetKernelName()
    {

        if (OSVersion >= new Version(10, 0))
        {
            Kernel = "Win10";
            HasStorPort = true;
        }
        else if (OSVersion >= new Version(6, 3))
        {
            Kernel = "Win8.1";
            HasStorPort = true;
        }
        else if (OSVersion >= new Version(6, 2))
        {
            Kernel = "Win8";
            HasStorPort = true;
        }
        else if (OSVersion >= new Version(6, 1))
        {
            Kernel = "Win7";
            HasStorPort = true;
        }
        else if (OSVersion >= new Version(6, 0))
        {
            Kernel = "WinLH";
            HasStorPort = true;
        }
        else if (OSVersion >= new Version(5, 2))
        {
            Kernel = "WinNET";
            HasStorPort = true;
        }
        else if (OSVersion >= new Version(5, 1))
        {
            Kernel = "WinXP";
            HasStorPort = false;
        }
        else if (OSVersion >= new Version(5, 0))
        {
            Kernel = "Win2K";
            HasStorPort = false;
        }
        else
        {
            throw new NotSupportedException($"Unsupported Windows version ('{OSVersion}')");
        }

        return Kernel;

    }

    /// <summary>
    /// Builds a list of device paths for active Arsenal Image Mounter
    /// objects.
    /// </summary>
    public static IEnumerable<string> EnumerateAdapterDevicePaths(IntPtr HwndParent)
    {

        var status = NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt".AsMemory(), out var devinstances);

        if (status != 0L || devinstances is null)
        {

            Trace.WriteLine($"No devices found serviced by 'phdskmnt'. status=0x{status:X}");
            yield break;
        }

        foreach (var devinstname in devinstances)
        {

            using var DevInfoSet = NativeFileIO.UnsafeNativeMethods.SetupDiGetClassDevsW(NativeConstants.SerenumBusEnumeratorGuid,
                                                                                         devinstname.MakeNullTerminated(),
                                                                                         HwndParent,
                                                                                         NativeConstants.DIGCF_DEVICEINTERFACE | NativeConstants.DIGCF_PRESENT);

            if (DevInfoSet.IsInvalid)
            {
                throw new Win32Exception();
            }

            var i = 0U;

            for(; ;)
            {
                var DeviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();

                if (!NativeFileIO.UnsafeNativeMethods.SetupDiEnumDeviceInterfaces(DevInfoSet,
                                                                                  IntPtr.Zero,
                                                                                  NativeConstants.SerenumBusEnumeratorGuid,
                                                                                  i,
                                                                                  ref DeviceInterfaceData))
                {
                    break;
                }

                var DeviceInterfaceDetailData = new SP_DEVICE_INTERFACE_DETAIL_DATA();

                if (NativeFileIO.UnsafeNativeMethods.SetupDiGetDeviceInterfaceDetailW(DevInfoSet,
                                                                                      DeviceInterfaceData,
                                                                                      ref DeviceInterfaceDetailData,
                                                                                      DeviceInterfaceData.Size,
                                                                                      out _,
                                                                                      IntPtr.Zero)
                    && !DeviceInterfaceDetailData.DevicePath.IsEmpty)
                {
                    yield return DeviceInterfaceDetailData.DevicePath.ToString();
                }

                i += 1U;
            }
        }
    }

    /// <summary>
    /// Returns a value indicating whether Arsenal Image Mounter driver is
    /// installed and running.
    /// </summary>
    public static bool AdapterDevicePresent
    {
        get
        {
            var devInsts = EnumerateAdapterDeviceInstanceNames();
            return devInsts is not null;
        }
    }

    /// <summary>
    /// Builds a list of setup device ids for active Arsenal Image Mounter
    /// objects. Device ids are used in calls to plug-and-play setup functions.
    /// </summary>
    public static IEnumerable<uint> EnumerateAdapterDeviceInstances()
    {

        var devinstances = EnumerateAdapterDeviceInstanceNames();

        if (devinstances is null)
        {
            yield break;
        }

        foreach (var devinstname in devinstances)
        {
#if DEBUG
            Trace.WriteLine($"Found adapter instance '{devinstname}'");
#endif
            var devInst = NativeFileIO.GetDevInst(devinstname);

            if (!devInst.HasValue)
            {
                continue;
            }

            yield return devInst.Value;
        }
    }

    /// <summary>
    /// Builds a list of setup device ids for active Arsenal Image Mounter
    /// objects. Device ids are used in calls to plug-and-play setup functions.
    /// </summary>
    public static IEnumerable<ReadOnlyMemory<char>>? EnumerateAdapterDeviceInstanceNames()
    {

        var status = NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt".AsMemory(), out var devinstances);

        if (status != 0L || devinstances is null)
        {

            Trace.WriteLine($"No devices found serviced by 'phdskmnt'. status=0x{status:X}");
            return null;
        }

        return devinstances;

    }

    /// <summary>
    /// Issues a SCSI bus rescan on found Arsenal Image Mounter adapters. This causes Disk Management
    /// in Windows to find newly created virtual disks and remove newly deleted ones.
    /// </summary>
    public static bool RescanScsiAdapter(uint devInst)
    {

        var rc = false;

        var status = NativeFileIO.UnsafeNativeMethods.CM_Reenumerate_DevNode(devInst, 0U);
        if (status != 0L)
        {
            Trace.WriteLine($"Re-enumeration of '{devInst}' failed: 0x{status:X}");
        }
        else
        {
            Trace.WriteLine($"Re-enumeration of '{devInst}' successful.");
            rc = true;
        }

        return rc;

    }

    private const string NonRemovableSuffix = ":$NonRemovable";

    public static IEnumerable<string> EnumeratePhysicalDeviceObjectPaths(uint devinstAdapter, uint DeviceNumber)
        => from devinstChild in NativeFileIO.EnumerateChildDevices(devinstAdapter)
           let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
           where !string.IsNullOrWhiteSpace(path)
           let address = NativeFileIO.GetScsiAddressForNtDevice(path)
           where address.HasValue && address.Value.DWordDeviceNumber.Equals(DeviceNumber)
           select path;

    public static IEnumerable<string> EnumerateDeviceProperty(uint devinstAdapter, uint DeviceNumber, CmDevNodeRegistryProperty prop)
        => from devinstChild in NativeFileIO.EnumerateChildDevices(devinstAdapter)
           let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
           where !string.IsNullOrWhiteSpace(path)
           let address = NativeFileIO.GetScsiAddressForNtDevice(path)
           where address.HasValue && address.Value.DWordDeviceNumber.Equals(DeviceNumber)
           from value in NativeFileIO.GetDeviceRegistryProperty(devinstChild, prop) ?? Enumerable.Empty<string>()
           select value;

    public static void UnregisterWriteOverlayImage(uint devInst)
        => RegisterWriteOverlayImage(devInst, OverlayImagePath: ReadOnlyMemory<char>.Empty, FakeNonRemovable: false);

    public static void RegisterWriteOverlayImage(uint devInst, string OverlayImagePath)
        => RegisterWriteOverlayImage(devInst, OverlayImagePath.AsMemory(), FakeNonRemovable: false);

    public static void RegisterWriteOverlayImage(uint devInst, string OverlayImagePath, bool FakeNonRemovable)
        => RegisterWriteOverlayImage(devInst, OverlayImagePath.AsMemory(), FakeNonRemovable);

    public static void RegisterWriteOverlayImage(uint devInst, ReadOnlyMemory<char> OverlayImagePath)
        => RegisterWriteOverlayImage(devInst, OverlayImagePath, FakeNonRemovable: false);

    public static void RegisterWriteOverlayImage(uint devInst, ReadOnlyMemory<char> OverlayImagePath, bool FakeNonRemovable)
    {

        string? nativepath;

        if (!OverlayImagePath.Span.IsWhiteSpace())
        {
            nativepath = NativeFileIO.GetNtPath(OverlayImagePath);
        }
        else
        {
            OverlayImagePath = default;
            nativepath = null;
        }

        var pdo_path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devInst)
            ?? throw new KeyNotFoundException($"Cannot find physical device object for devInst = {devInst}");

        var dev_path = NativeFileIO.QueryDosDevice(NativeFileIO.GetPhysicalDriveNameForNtDevice(pdo_path)).FirstOrDefault()
            ?? throw new KeyNotFoundException($"Cannot find symbolic link for devInst = {devInst}");

        Trace.WriteLine($"Device {pdo_path} devinst {devInst}. Registering write overlay '{nativepath}', FakeNonRemovable={FakeNonRemovable}");

        using (var regkey = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\aimwrfltr\Parameters"))
        {
            if (nativepath is null)
            {
                regkey.DeleteValue(pdo_path, throwOnMissingValue: false);
            }
            else if (FakeNonRemovable)
            {
                regkey.SetValue(pdo_path, $"{nativepath}{NonRemovableSuffix}", RegistryValueKind.String);
            }
            else
            {
                regkey.SetValue(pdo_path, nativepath, RegistryValueKind.String);
            }
        }

        if (nativepath is null)
        {
            NativeFileIO.RemoveFilter(devInst, "aimwrfltr");
        }
        else
        {
            NativeFileIO.AddFilter(devInst, "aimwrfltr");
        }

        var last_error = 0;

        for (var r = 1; r <= 4; r++)
        {

            NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, devInst);

            var statistics = new WriteFilterStatistics();

            last_error = GetWriteOverlayStatus(pdo_path, out statistics);

            Trace.WriteLine($"Overlay path '{nativepath}', I/O error code: {last_error}, aimwrfltr error code: 0x{statistics.LastErrorCode:X}, protection: {statistics.IsProtected}, initialized: {statistics.Initialized}");

            if (nativepath is null && last_error == NativeConstants.NO_ERROR)
            {

                Trace.WriteLine("Filter driver not yet unloaded, retrying...");
                Thread.Sleep(300);
                continue;
            }

            else if (nativepath is not null && (last_error == NativeConstants.ERROR_INVALID_FUNCTION || last_error == NativeConstants.ERROR_INVALID_PARAMETER || last_error == NativeConstants.ERROR_NOT_SUPPORTED))
            {

                Trace.WriteLine("Filter driver not yet loaded, retrying...");
                Thread.Sleep(300);
                continue;
            }

            else if (nativepath is not null && last_error != NativeConstants.NO_ERROR || nativepath is null && last_error != NativeConstants.ERROR_INVALID_FUNCTION && last_error != NativeConstants.ERROR_INVALID_PARAMETER && last_error != NativeConstants.ERROR_NOT_SUPPORTED)
            {

                throw new NotSupportedException("Error checking write filter driver status", new Win32Exception(last_error));
            }

            else if (nativepath is not null && statistics.Initialized || nativepath is null)
            {

                return;

            }

            throw new IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode));

        }

        var in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(pdo_path, dev_path).Take(10).Select(NativeFileIO.FormatProcessName).ToArray();

        if (in_use_apps.Length == 0 && last_error != 0)
        {
            throw new NotSupportedException("Write filter driver not attached to device", new Win32Exception(last_error));
        }
        else if (in_use_apps.Length == 0)
        {
            throw new NotSupportedException("Write filter driver not attached to device");
        }
        else
        {
            var apps = string.Join(", ", in_use_apps);

            throw new UnauthorizedAccessException($@"Write filter driver cannot be attached while applications hold the disk device open.

Currently, the following application{(in_use_apps.Length != 1 ? "s" : "")} hold{(in_use_apps.Length == 1 ? "s" : "")} the disk device open:
{apps}");
        }

        throw new FileNotFoundException("Error adding write overlay: Device not found.");

    }

    public static void RegisterWriteFilter(uint devinstAdapter, uint DeviceNumber, RegisterWriteFilterOperation operation)
    {

        foreach (var dev in from devinstChild in NativeFileIO.EnumerateChildDevices(devinstAdapter)
                            let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
                            where !string.IsNullOrWhiteSpace(path)
                            let address = NativeFileIO.GetScsiAddressForNtDevice(path)
                            where address.HasValue && address.Value.DWordDeviceNumber.Equals(DeviceNumber)
                            select (path, devinstChild))
        {

            Trace.WriteLine($"Device number {DeviceNumber:X6}  found at {dev.path} devinst {dev.devinstChild}. Registering write filter driver.");

            if (operation == RegisterWriteFilterOperation.Unregister)
            {

                if (NativeFileIO.RemoveFilter(dev.devinstChild, "aimwrfltr"))
                {
                    NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild);
                }

                return;

            }

            var last_error = 0;

            for (var r = 1; r <= 2; r++)
            {

                if (NativeFileIO.AddFilter(dev.devinstChild, "aimwrfltr"))
                {
                    NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild);
                }

                var statistics = new WriteFilterStatistics();

                last_error = GetWriteOverlayStatus(dev.path, out statistics);

                if (last_error == NativeConstants.ERROR_INVALID_FUNCTION)
                {
                    Trace.WriteLine("Filter driver not loaded, retrying...");
                    Thread.Sleep(200);
                    continue;
                }
                else if (last_error != NativeConstants.NO_ERROR)
                {
                    throw new NotSupportedException("Error checking write filter driver status", new Win32Exception());
                }

                if (statistics.Initialized)
                {
                    return;
                }

                throw new IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode));

            }

            var in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(dev.path).Take(10).Select(NativeFileIO.FormatProcessName).ToArray();

            if (in_use_apps.Length == 0 && last_error > 0)
            {
                throw new NotSupportedException("Write filter driver not attached to device", new Win32Exception(last_error));
            }
            else if (in_use_apps.Length == 0)
            {
                throw new NotSupportedException("Write filter driver not attached to device");
            }
            else
            {
                var apps = string.Join(", ", in_use_apps);
                throw new UnauthorizedAccessException($@"Write filter driver cannot be attached while applications hold the virtual disk device open.

Currently, the following application{(in_use_apps.Length != 1 ? "s" : "")} hold{(in_use_apps.Length == 1 ? "s" : "")} the disk device open:
{apps}");
            }
        }

        throw new FileNotFoundException("Error adding write overlay: Device not found.");

    }

    public static async Task RegisterWriteOverlayImageAsync(uint devInst, ReadOnlyMemory<char> OverlayImagePath, bool FakeNonRemovable, CancellationToken cancel)
    {

        string? nativepath;

        if (!OverlayImagePath.Span.IsWhiteSpace())
        {
            nativepath = NativeFileIO.GetNtPath(OverlayImagePath);
        }
        else
        {
            OverlayImagePath = default;
            nativepath = null;
        }

        var pdo_path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devInst)
            ?? throw new KeyNotFoundException($"Cannot find physical device object for devInst = {devInst}");

        var dev_path = NativeFileIO.QueryDosDevice(NativeFileIO.GetPhysicalDriveNameForNtDevice(pdo_path)).FirstOrDefault()
            ?? throw new KeyNotFoundException($"Cannot find symbolic link object for devInst = {devInst}");

        Trace.WriteLine($"Device {pdo_path} devinst {devInst}. Registering write overlay '{nativepath}', FakeNonRemovable={FakeNonRemovable}");

        using (var regkey = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\aimwrfltr\Parameters"))
        {
            if (nativepath is null)
            {
                regkey.DeleteValue(pdo_path, throwOnMissingValue: false);
            }
            else if (FakeNonRemovable)
            {
                regkey.SetValue(pdo_path, $"{nativepath}{NonRemovableSuffix}", RegistryValueKind.String);
            }
            else
            {
                regkey.SetValue(pdo_path, nativepath, RegistryValueKind.String);
            }
        }

        if (nativepath is null)
        {
            NativeFileIO.RemoveFilter(devInst, "aimwrfltr");
        }
        else
        {
            NativeFileIO.AddFilter(devInst, "aimwrfltr");
        }

        var last_error = 0;

        for (var r = 1; r <= 4; r++)
        {

            NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, devInst);

            var statistics = new WriteFilterStatistics();

            last_error = GetWriteOverlayStatus(pdo_path, out statistics);

            Trace.WriteLine($"Overlay path '{nativepath}', I/O error code: {last_error}, aimwrfltr error code: 0x{statistics.LastErrorCode:X}, protection: {statistics.IsProtected}, initialized: {statistics.Initialized}");

            if (nativepath is null && last_error == NativeConstants.NO_ERROR)
            {

                Trace.WriteLine("Filter driver not yet unloaded, retrying...");
                await Task.Delay(300, cancel).ConfigureAwait(false);
                continue;
            }

            else if (nativepath is not null && (last_error == NativeConstants.ERROR_INVALID_FUNCTION || last_error == NativeConstants.ERROR_INVALID_PARAMETER || last_error == NativeConstants.ERROR_NOT_SUPPORTED))
            {

                Trace.WriteLine("Filter driver not yet loaded, retrying...");
                await Task.Delay(300, cancel).ConfigureAwait(false);
                continue;
            }

            else if (nativepath is not null && last_error != NativeConstants.NO_ERROR || nativepath is null && last_error != NativeConstants.ERROR_INVALID_FUNCTION && last_error != NativeConstants.ERROR_INVALID_PARAMETER && last_error != NativeConstants.ERROR_NOT_SUPPORTED)
            {

                throw new NotSupportedException("Error checking write filter driver status", new Win32Exception(last_error));
            }

            else if (nativepath is not null && statistics.Initialized || nativepath is null)
            {

                return;

            }

            throw new IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode));

        }

        var in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(pdo_path, dev_path).Take(10).Select(NativeFileIO.FormatProcessName).ToArray();

        if (in_use_apps.Length == 0 && last_error != 0)
        {
            throw new NotSupportedException("Write filter driver not attached to device", new Win32Exception(last_error));
        }
        else if (in_use_apps.Length == 0)
        {
            throw new NotSupportedException("Write filter driver not attached to device");
        }
        else
        {
            var apps = string.Join(", ", in_use_apps);

            throw new UnauthorizedAccessException($@"Write filter driver cannot be attached while applications hold the disk device open.

Currently, the following application{(in_use_apps.Length != 1 ? "s" : "")} hold{(in_use_apps.Length == 1 ? "s" : "")} the disk device open:
{apps}");
        }

        throw new FileNotFoundException("Error adding write overlay: Device not found.");

    }

    public static async Task RegisterWriteFilterAsync(uint devinstAdapter, uint DeviceNumber, RegisterWriteFilterOperation operation, CancellationToken cancel)
    {

        foreach (var dev in from devinstChild in NativeFileIO.EnumerateChildDevices(devinstAdapter)
                            let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
                            where !string.IsNullOrWhiteSpace(path)
                            let address = NativeFileIO.GetScsiAddressForNtDevice(path)
                            where address.HasValue && address.Value.DWordDeviceNumber.Equals(DeviceNumber)
                            select (path, devinstChild))
        {

            Trace.WriteLine($"Device number {DeviceNumber:X6}  found at {dev.path} devinst {dev.devinstChild}. Registering write filter driver.");

            if (operation == RegisterWriteFilterOperation.Unregister)
            {

                if (NativeFileIO.RemoveFilter(dev.devinstChild, "aimwrfltr"))
                {
                    NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild);
                }

                return;

            }

            var last_error = 0;

            for (var r = 1; r <= 2; r++)
            {

                if (NativeFileIO.AddFilter(dev.devinstChild, "aimwrfltr"))
                {
                    NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild);
                }

                var statistics = new WriteFilterStatistics();

                last_error = GetWriteOverlayStatus(dev.path, out statistics);

                if (last_error == NativeConstants.ERROR_INVALID_FUNCTION)
                {
                    Trace.WriteLine("Filter driver not loaded, retrying...");
                    await Task.Delay(200, cancel).ConfigureAwait(false);
                    continue;
                }
                else if (last_error != NativeConstants.NO_ERROR)
                {
                    throw new NotSupportedException("Error checking write filter driver status", new Win32Exception());
                }

                if (statistics.Initialized)
                {
                    return;
                }

                throw new IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode));

            }

            var in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(dev.path).Take(10).Select(NativeFileIO.FormatProcessName).ToArray();

            if (in_use_apps.Length == 0 && last_error > 0)
            {
                throw new NotSupportedException("Write filter driver not attached to device", new Win32Exception(last_error));
            }
            else if (in_use_apps.Length == 0)
            {
                throw new NotSupportedException("Write filter driver not attached to device");
            }
            else
            {
                var apps = string.Join(", ", in_use_apps);
                throw new UnauthorizedAccessException($@"Write filter driver cannot be attached while applications hold the virtual disk device open.

Currently, the following application{(in_use_apps.Length != 1 ? "s" : "")} hold{(in_use_apps.Length == 1 ? "s" : "")} the disk device open:
{apps}");
            }
        }

        throw new FileNotFoundException("Error adding write overlay: Device not found.");

    }

    public enum RegisterWriteFilterOperation
    {
        Register,
        Unregister
    }

    /// <summary>
    /// Retrieves status of write overlay for mounted device.
    /// </summary>
    /// <param name="NtDevicePath">NT path to device.</param>
    /// <param name="Statistics">Data structure that receives current statistics and settings for filter</param>
    /// <returns>Returns 0 on success or Win32 error code on failure</returns>
    public static int GetWriteOverlayStatus(string NtDevicePath, out WriteFilterStatistics Statistics)
    {

        using var hDevice = NativeFileIO.NtCreateFile(NtDevicePath, 0, 0, FileShare.ReadWrite, NtCreateDisposition.Open, NtCreateOptions.NonDirectoryFile, 0, null, out var argWasCreated);

        return GetWriteOverlayStatus(hDevice, out Statistics);

    }

    /// <summary>
    /// Retrieves status of write overlay for mounted device.
    /// </summary>
    /// <param name="hDevice">Handle to device.</param>
    /// <param name="Statistics">Data structure that receives current statistics and settings for filter</param>
    /// <returns>Returns 0 on success or Win32 error code on failure</returns>
    public static int GetWriteOverlayStatus(SafeFileHandle hDevice, out WriteFilterStatistics Statistics)
    {

        Statistics = new();

        var version = Statistics.Version;

        return UnsafeNativeMethods.DeviceIoControl(hDevice, UnsafeNativeMethods.IOCTL_AIMWRFLTR_GET_DEVICE_DATA, IntPtr.Zero, 0U, out Statistics, version, out var arglpBytesReturned, default)
            ? (int)NativeConstants.NO_ERROR
            : Marshal.GetLastWin32Error();

    }

    /// <summary>
    /// Deletes the write overlay image file after use. Also sets this filter driver to
    /// silently ignore flush requests to improve performance when integrity of the write
    /// overlay image is not needed for future sessions.
    /// </summary>
    /// <param name="NtDevicePath">NT path to device.</param>
    /// <returns>Returns 0 on success or Win32 error code on failure</returns>
    public static int SetWriteOverlayDeleteOnClose(string NtDevicePath)
    {

        using var hDevice = NativeFileIO.NtCreateFile(NtDevicePath, 0, FileAccess.ReadWrite, FileShare.ReadWrite, NtCreateDisposition.Open, NtCreateOptions.NonDirectoryFile, 0, null, out var argWasCreated);

        return SetWriteOverlayDeleteOnClose(hDevice);

    }

    /// <summary>
    /// Deletes the write overlay image file after use. Also sets this filter driver to
    /// silently ignore flush requests to improve performance when integrity of the write
    /// overlay image is not needed for future sessions.
    /// </summary>
    /// <param name="hDevice">Handle to device.</param>
    /// <returns>Returns 0 on success or Win32 error code on failure</returns>
    public static int SetWriteOverlayDeleteOnClose(SafeFileHandle hDevice) => UnsafeNativeMethods.DeviceIoControl(hDevice, UnsafeNativeMethods.IOCTL_AIMWRFLTR_DELETE_ON_CLOSE, IntPtr.Zero, 0U, IntPtr.Zero, 0U, out var arglpBytesReturned, default)
            ? (int)NativeConstants.NO_ERROR
            : Marshal.GetLastWin32Error();

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    private static partial class UnsafeNativeMethods
    {
        public const uint IOCTL_AIMWRFLTR_GET_DEVICE_DATA = 0x88443404u;

        public const uint IOCTL_AIMWRFLTR_DELETE_ON_CLOSE = 0x8844F407u;

#if NET7_0_OR_GREATER
        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out WriteFilterStatistics lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
#else
        [DllImport("kernel32", SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out WriteFilterStatistics lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
#endif
    }
}