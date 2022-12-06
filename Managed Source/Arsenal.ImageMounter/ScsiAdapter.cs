// '''' ScsiAdapter.vb
// '''' Class for controlling Arsenal Image Mounter Devices.
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
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
/// Represents Arsenal Image Mounter objects.
/// </summary>
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public class ScsiAdapter : DeviceObject
{
    public const uint CompatibleDriverVersion = 0x101U;

    public const uint AutoDeviceNumber = 0xFFFFFFU;

    public ReadOnlyMemory<char> DeviceInstanceName { get; }

    public uint DeviceInstance { get; }

    private static SafeFileHandle? OpenAdapterHandle(string ntdevice, uint devInst)
    {
        SafeFileHandle handle;
        try
        {
            handle = NativeFileIO.NtCreateFile(ntdevice, 0, FileAccess.ReadWrite, FileShare.ReadWrite, NtCreateDisposition.Open, 0, 0, null, out var argWasCreated);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"PhDskMnt::OpenAdapterHandle: Error opening device '{ntdevice}': {ex.JoinMessages()}");

            return null;
        }

        bool acceptedversion;
        for (var i = 1; i <= 3; i++)
        {
            try
            {
                acceptedversion = CheckDriverVersion(handle);
                if (acceptedversion)
                {
                    return handle;
                }
                else
                {
                    handle.Dispose();
                    throw new Exception("Incompatible version of Arsenal Image Mounter Miniport driver.");
                }
            }
            catch (Win32Exception ex)
            when (ex.NativeErrorCode is NativeConstants.ERROR_INVALID_FUNCTION or NativeConstants.ERROR_IO_DEVICE)
            {
                // ' In case of SCSIPORT (Win XP) miniport, there is always a risk
                // ' that we lose contact with IOCTL_SCSI_MINIPORT after device adds
                // ' and removes. Therefore, in case we know that we have a handle to
                // ' the SCSI adapter and it fails IOCTL_SCSI_MINIPORT requests, just
                // ' issue a bus re-enumeration to find the dummy IOCTL device, which
                // ' will make SCSIPORT let control requests through again.
                if (!API.HasStorPort)
                {
                    Trace.WriteLine("PhDskMnt::OpenAdapterHandle: Lost contact with miniport, rescanning...");
                    try
                    {
                        API.RescanScsiAdapter(devInst);
                        Thread.Sleep(100);
                        continue;
                    }
                    catch (Exception ex2)
                    {
                        Trace.WriteLine($"PhDskMnt::RescanScsiAdapter: {ex2}");
                    }
                }

                handle.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                if (ex is Win32Exception win32exception)
                {
                    Trace.WriteLine($"Error code 0x{win32exception.NativeErrorCode:X8}");
                }

                Trace.WriteLine($"PhDskMnt::OpenAdapterHandle: Error checking driver version: {ex.JoinMessages()}");
                handle.Dispose();
                return null;
            }
        }

        return null;
    }

    private record class AdapterDeviceInstance(ReadOnlyMemory<char> DevInstName, uint DevInst, SafeFileHandle SafeHandle);

    /// <summary>
    /// Retrieves a handle to first found adapter, or null if error occurs.
    /// </summary>
    /// <remarks>Arsenal Image Mounter does not currently support more than one adapter.</remarks>
    /// <returns>An object containing devinst value and an open handle to first found
    /// compatible adapter.</returns>
    private static AdapterDeviceInstance OpenAdapter()
    {

        var devinstNames = API.EnumerateAdapterDeviceInstanceNames();

        if (devinstNames is null)
        {

            throw new FileNotFoundException("No Arsenal Image Mounter adapter found.");

        }

        var found = (from devInstName in devinstNames
                     let devinst = NativeFileIO.GetDevInst(devInstName)
                     where devinst.HasValue
                     let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinst.Value)
                     where path is not null
                     let handle = OpenAdapterHandle(path, devinst.Value)
                     where handle is not null
                     select new AdapterDeviceInstance(devInstName, devinst.Value, handle)).FirstOrDefault();

        if (found is null)
        {
            throw new FileNotFoundException("No Arsenal Image Mounter adapter found.");
        }

        return found;
    }

    /// <summary>
    /// Opens first found Arsenal Image Mounter adapter.
    /// </summary>
    public ScsiAdapter()
        : this(OpenAdapter())
    {

    }

    private ScsiAdapter(AdapterDeviceInstance OpenAdapterHandle)
        : base(OpenAdapterHandle.SafeHandle, FileAccess.ReadWrite)
    {

        DeviceInstance = OpenAdapterHandle.DevInst;
        DeviceInstanceName = OpenAdapterHandle.DevInstName;

        Trace.WriteLine($"Successfully opened SCSI adapter '{OpenAdapterHandle.DevInstName}'.");
    }

    /// <summary>
    /// Opens a specific Arsenal Image Mounter adapter specified by SCSI port number.
    /// </summary>
    /// <param name="ScsiPortNumber">Scsi adapter port number as assigned by SCSI class driver.</param>
    public ScsiAdapter(byte ScsiPortNumber)
        : base($@"\\?\Scsi{ScsiPortNumber}:".AsMemory(), FileAccess.ReadWrite)
    {

        Trace.WriteLine($"Successfully opened adapter with SCSI portnumber = {ScsiPortNumber}.");

        if (!CheckDriverVersion())
        {
            throw new Exception("Incompatible version of Arsenal Image Mounter Miniport driver.");
        }
    }

    /// <summary>
    /// Retrieves a list of virtual disks on this adapter. Each element in returned list holds device number of an existing
    /// virtual disk.
    /// </summary>
    public uint[] GetDeviceList()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            var Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle, NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_ADAPTER, 0U, buffer, out var ReturnCode);

            if (ReturnCode != 0)
            {
                throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);
            }

            var NumberOfDevices = MemoryMarshal.Read<int>(Response);

            if (NumberOfDevices == 0)
            {
                return Array.Empty<uint>();
            }

            var array = MemoryMarshal.Cast<byte, uint>(Response.Slice(sizeof(uint), NumberOfDevices * sizeof(uint)))
                .ToArray();

            return array;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Retrieves a list of DeviceProperties objects for each virtual disk on this adapter.
    /// </summary>
    public IEnumerable<DeviceProperties> EnumerateDevicesProperties()
        => GetDeviceList().Select(QueryDevice);

    /// <summary>
    /// Creates a new virtual disk.
    /// </summary>
    /// <param name="DiskSize">Size of virtual disk. If this parameter is zero, current size of disk image file will
    /// automatically be used as virtual disk size.</param>
    /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry. This parameter can be zero
    /// in which case most reasonable value will be automatically used by the driver.</param>
    /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    /// or Windows filesystem drivers.</param>
    /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    /// <param name="Filename">Name of disk image file to use or create. If disk image file already exists, the DiskSize
    /// parameter can be zero in which case current disk image file size will be used as virtual disk size. If Filename
    /// parameter is Nothing/null disk will be created in virtual memory and not backed by a physical disk image file.</param>
    /// <param name="NativePath">Specifies whether Filename parameter specifies a path in Windows native path format, the
    /// path format used by drivers in Windows NT kernels, for example \Device\Harddisk0\Partition1\imagefile.img. If this
    /// parameter is False path in FIlename parameter will be interpreted as an ordinary user application path.</param>
    /// <param name="DeviceNumber">In: Device number for device to create. Device number must not be in use by an existing
    /// virtual disk. For automatic allocation of device number, pass ScsiAdapter.AutoDeviceNumber.
    /// 
    /// Out: Device number for created device.</param>
    public void CreateDevice(long DiskSize, uint BytesPerSector, long ImageOffset, DeviceFlags Flags, ReadOnlyMemory<char> Filename, bool NativePath, ref uint DeviceNumber)
        => CreateDevice(DiskSize, BytesPerSector, ImageOffset, Flags, Filename, NativePath, WriteOverlayFilename: default, WriteOverlayNativePath: default, DeviceNumber: ref DeviceNumber);

    /// <summary>
    /// Creates a new virtual disk.
    /// </summary>
    /// <param name="DiskSize">Size of virtual disk. If this parameter is zero, current size of disk image file will
    /// automatically be used as virtual disk size.</param>
    /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry. This parameter can be zero
    /// in which case most reasonable value will be automatically used by the driver.</param>
    /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    /// or Windows filesystem drivers.</param>
    /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    /// <param name="Filename">Name of disk image file to use or create. If disk image file already exists, the DiskSize
    /// parameter can be zero in which case current disk image file size will be used as virtual disk size. If Filename
    /// parameter is Nothing/null disk will be created in virtual memory and not backed by a physical disk image file.</param>
    /// <param name="NativePath">Specifies whether Filename parameter specifies a path in Windows native path format, the
    /// path format used by drivers in Windows NT kernels, for example \Device\Harddisk0\Partition1\imagefile.img. If this
    /// parameter is False path in Filename parameter will be interpreted as an ordinary user application path.</param>
    /// <param name="WriteOverlayFilename">Name of differencing image file to use for write overlay operation. Flags fields
    /// must also specify read-only device and write overlay operation for this field to be used.</param>
    /// <param name="WriteOverlayNativePath">Specifies whether WriteOverlayFilename parameter specifies a path in Windows
    /// native path format, the path format used by drivers in Windows NT kernels, for example
    /// \Device\Harddisk0\Partition1\imagefile.img. If this parameter is False path in Filename parameter will be interpreted
    /// as an ordinary user application path.</param>
    /// <param name="DeviceNumber">In: Device number for device to create. Device number must not be in use by an existing
    /// virtual disk. For automatic allocation of device number, pass ScsiAdapter.AutoDeviceNumber.
    /// 
    /// Out: Device number for created device.</param>
    public void CreateDevice(long DiskSize, uint BytesPerSector, long ImageOffset, DeviceFlags Flags, ReadOnlyMemory<char> Filename, bool NativePath, ReadOnlyMemory<char> WriteOverlayFilename, bool WriteOverlayNativePath, ref uint DeviceNumber)
    {

        // ' Temporary variable for passing through lambda function
        var devnr = DeviceNumber;

        // ' Both UInt32.MaxValue and AutoDeviceNumber can be used
        // ' for auto-selecting device number, but only AutoDeviceNumber
        // ' is accepted by driver.
        if (devnr == uint.MaxValue)
        {
            devnr = AutoDeviceNumber;
        }

        // ' Translate Win32 path to native NT path that kernel understands
        if (!Filename.Span.IsWhiteSpace() && !NativePath)
        {
            switch (Flags.GetDiskType())
            {
                case DeviceFlags.TypeProxy:
                    {
                        switch (Flags.GetProxyType())
                        {

                            case DeviceFlags.ProxyTypeSharedMemory:
                                {
                                    Filename = $@"\BaseNamedObjects\Global\{Filename}".AsMemory();
                                    break;
                                }

                            case DeviceFlags.ProxyTypeComm:
                            case DeviceFlags.ProxyTypeTCP:
                                {
                                    break;
                                }

                            default:
                                {
                                    Filename = NativeFileIO.GetNtPath(Filename).AsMemory();
                                    break;
                                }
                        }

                        break;
                    }

                default:
                    {
                        Filename = NativeFileIO.GetNtPath(Filename).AsMemory();
                        break;
                    }
            }
        }

        // ' Show what we got
        Trace.WriteLine($"ScsiAdapter.CreateDevice: Native filename='{Filename}'");

        GlobalCriticalMutex? write_filter_added = null;

        try
        {

            if (!WriteOverlayFilename.Span.IsWhiteSpace())
            {

                if (!WriteOverlayNativePath)
                {
                    WriteOverlayFilename = NativeFileIO.GetNtPath(WriteOverlayFilename).AsMemory();
                }

                Trace.WriteLine($"ScsiAdapter.CreateDevice: Thread {Environment.CurrentManagedThreadId} entering global critical section");

                write_filter_added = new GlobalCriticalMutex();

                NativeFileIO.AddFilter(NativeConstants.DiskDriveGuid, "aimwrfltr", addfirst: true);

            }

            // ' Show what we got
            Trace.WriteLine($"ScsiAdapter.CreateDevice: Native write overlay filename='{WriteOverlayFilename}'");

            var deviceConfig = new IMSCSI_DEVICE_CONFIGURATION(deviceNumber: devnr, diskSize: DiskSize, bytesPerSector: BytesPerSector, imageOffset: ImageOffset, flags: (int)Flags, fileNameLength: (ushort)(Filename.Span.IsWhiteSpace() ? 0 : MemoryMarshal.AsBytes(Filename.Span).Length), writeOverlayFileNameLength: (ushort)(WriteOverlayFilename.Span.IsWhiteSpace() ? 0 : MemoryMarshal.AsBytes(WriteOverlayFilename.Span).Length));

            var Request = ArrayPool<byte>.Shared.Rent(PinnedBuffer<IMSCSI_DEVICE_CONFIGURATION>.TypeSize
                + deviceConfig.FileNameLength
                + deviceConfig.WriteOverlayFileNameLength);

            MemoryMarshal.Write(Request, ref deviceConfig);

            if (!Filename.Span.IsWhiteSpace())
            {
                MemoryMarshal.AsBytes(Filename.Span).CopyTo(Request.AsSpan(PinnedBuffer<IMSCSI_DEVICE_CONFIGURATION>.TypeSize));
            }

            if (!WriteOverlayFilename.Span.IsWhiteSpace())
            {
                MemoryMarshal.AsBytes(WriteOverlayFilename.Span).CopyTo(Request.AsSpan(PinnedBuffer<IMSCSI_DEVICE_CONFIGURATION>.TypeSize + deviceConfig.FileNameLength));
            }

            var Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle, NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_CREATE_DEVICE, 0U, Request, out var ReturnCode);

            if (ReturnCode != 0)
            {
                throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);
            }

            deviceConfig = MemoryMarshal.Read<IMSCSI_DEVICE_CONFIGURATION>(Response);

            DeviceNumber = deviceConfig.DeviceNumber;
            DiskSize = deviceConfig.DiskSize;
            BytesPerSector = deviceConfig.BytesPerSector;
            ImageOffset = deviceConfig.ImageOffset;
            Flags = (DeviceFlags)deviceConfig.Flags;

            while (!GetDeviceList().Contains(DeviceNumber))
            {
                Trace.WriteLine($"Waiting for new device {DeviceNumber:X6} to be registered by driver...");
                Thread.Sleep(2500);
            }

            DiskDevice DiskDevice;

            var waittime = TimeSpan.FromMilliseconds(500d);
            do
            {

                Thread.Sleep(waittime);

                try
                {
                    DiskDevice = OpenDevice(DeviceNumber, FileAccess.Read);
                }

                catch (DriveNotFoundException ex)
                {
                    Trace.WriteLine($"Error opening device: {ex.JoinMessages()}");
                    waittime += TimeSpan.FromMilliseconds(500d);

                    Trace.WriteLine("Not ready, rescanning SCSI adapter...");

                    RescanBus();

                    continue;

                }

                using (DiskDevice)
                {

                    if (0 is var arg2 && DiskDevice.DiskSize is { } arg1 && arg1 == arg2)
                    {

                        // ' Wait at most 20 x 500 msec for device to get initialized by driver
                        for (var i = 1; i <= 20; i++)
                        {

                            Thread.Sleep(500 * i);

                            if (0 is var arg4 && DiskDevice.DiskSize is { } arg3 && arg3 != arg4)
                            {
                                break;
                            }

                            Trace.WriteLine("Updating disk properties...");
                            DiskDevice.UpdateProperties();

                        }
                    }

                    if (Flags.HasFlag(DeviceFlags.WriteOverlay) && !WriteOverlayFilename.Span.IsWhiteSpace())
                    {

                        var status = DiskDevice.WriteOverlayStatus;

                        if (status.HasValue)
                        {

                            Trace.WriteLine($"Write filter attached, {status.Value.UsedDiffSize} differencing bytes used.");

                            break;

                        }

                        Trace.WriteLine("Write filter not registered. Registering and restarting device...");
                    }

                    else
                    {

                        break;

                    }
                }

                try
                {
                    API.RegisterWriteFilter(DeviceInstance, DeviceNumber, API.RegisterWriteFilterOperation.Register);
                }

                catch (Exception ex)
                {
                    RemoveDevice(DeviceNumber);
                    throw new Exception("Failed to register write filter driver", ex);

                }
            }

            while (true);
        }

        finally
        {

            if (write_filter_added is not null)
            {

                NativeFileIO.RemoveFilter(NativeConstants.DiskDriveGuid, "aimwrfltr");

                Trace.WriteLine($"ScsiAdapter.CreateDevice: Thread {Environment.CurrentManagedThreadId} leaving global critical section");

                write_filter_added.Dispose();

            }
        }

        Trace.WriteLine("CreateDevice done.");

    }

    /// <summary>
    /// Removes an existing virtual disk from adapter by first taking the disk offline so that any
    /// mounted file systems are safely dismounted.
    /// </summary>
    /// <param name="DeviceNumber">Device number to remove. Note that AutoDeviceNumber constant passed
    /// in this parameter causes all present virtual disks to be removed from this adapter.</param>
    public void RemoveDeviceSafe(uint DeviceNumber)
    {

        if (DeviceNumber == AutoDeviceNumber)
        {

            RemoveAllDevicesSafe();

            return;

        }

        IEnumerable<string>? volumes = null;

        using (var disk = OpenDevice(DeviceNumber, FileAccess.ReadWrite))
        {

            if (disk.IsDiskWritable)
            {

                volumes = disk.EnumerateDiskVolumes();

            }
        }

        if (volumes is not null)
        {

            foreach (var volname in volumes.Select(v => v.AsMemory().TrimEnd('\\')))
            {
                Trace.WriteLine($"Dismounting volume: {volname}");

                using var vol = NativeFileIO.OpenFileHandle(volname, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open, FileOptions.None);
                if (NativeFileIO.IsDiskWritable(vol))
                {

                    try
                    {
                        NativeFileIO.FlushBuffers(vol);
                    }

                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Failed flushing buffers for volume {volname}: {ex.JoinMessages()}");

                    }

                    // NativeFileIO.Win32Try(NativeFileIO.DismountVolumeFilesystem(vol, Force:=False))

                    NativeFileIO.SetVolumeOffline(vol, offline: true);
                }
            }
        }

        RemoveDevice(DeviceNumber);

    }

    /// <summary>
    /// Removes all virtual disks on current adapter by first taking the disks offline so that any
    /// mounted file systems are safely dismounted.
    /// </summary>
    public void RemoveAllDevicesSafe()
        => Parallel.ForEach(GetDeviceList(), RemoveDeviceSafe);

    /// <summary>
    /// Removes an existing virtual disk from adapter.
    /// </summary>
    /// <param name="DeviceNumber">Device number to remove. Note that AutoDeviceNumber constant passed
    /// in this parameter causes all present virtual disks to be removed from this adapter.</param>
    public void RemoveDevice(uint DeviceNumber)
    {
        NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                   NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_REMOVE_DEVICE,
                                                   0U,
                                                   BitConverter.GetBytes(DeviceNumber),
                                                   out var ReturnCode);

        if (ReturnCode == NativeConstants.STATUS_OBJECT_NAME_NOT_FOUND) // Device already removed
        {
            return;
        }
        else if (ReturnCode != 0)
        {
            throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);
        }
    }

    /// <summary>
    /// Removes all virtual disks on current adapter.
    /// </summary>
    public void RemoveAllDevices() => RemoveDevice(AutoDeviceNumber);

    /// <summary>
    /// Retrieves properties for an existing virtual disk.
    /// </summary>
    /// <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
    /// <param name="DiskSize">Size of virtual disk.</param>
    /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
    /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    /// or Windows filesystem drivers.</param>
    /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    /// <param name="Filename">Name of disk image file holding storage for file type virtual disk or used to create a
    /// virtual memory type virtual disk.</param>
    public void QueryDevice(uint DeviceNumber, out long DiskSize, out uint BytesPerSector, out long ImageOffset, out DeviceFlags Flags, out string? Filename)
        => QueryDevice(DeviceNumber, out DiskSize, out BytesPerSector, out ImageOffset, out Flags, out Filename, WriteOverlayImagefile: out _);

    /// <summary>
    /// Retrieves properties for an existing virtual disk.
    /// </summary>
    /// <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
    /// <param name="DiskSize">Size of virtual disk.</param>
    /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
    /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    /// or Windows filesystem drivers.</param>
    /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    /// <param name="Filename">Name of disk image file holding storage for file type virtual disk or used to create a
    /// virtual memory type virtual disk.</param>
    /// <param name="WriteOverlayImagefile">Path to differencing file used in write-temporary mode.</param>
    public void QueryDevice(uint DeviceNumber,
                            out long DiskSize,
                            out uint BytesPerSector,
                            out long ImageOffset,
                            out DeviceFlags Flags,
                            out string? Filename,
                            out string? WriteOverlayImagefile)
    {
        DiskSize = 0;
        BytesPerSector = 0;
        ImageOffset = 0;
        Flags = 0;
        Filename = null;
        WriteOverlayImagefile = null;

        var Request = ArrayPool<byte>.Shared.Rent(PinnedBuffer<IMSCSI_DEVICE_CONFIGURATION>.TypeSize + 65535);

        try
        {
            var deviceConfig = new IMSCSI_DEVICE_CONFIGURATION(deviceNumber: DeviceNumber, fileNameLength: 65535);

            MemoryMarshal.Write(Request, ref deviceConfig);

            var Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle, NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_DEVICE, 0U, Request, out var ReturnCode);

            // ' STATUS_OBJECT_NAME_NOT_FOUND. Possible "zombie" device, just return empty data.
            if (ReturnCode == 0xC0000034)
            {
                return;
            }
            else if (ReturnCode != 0)
            {
                throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);
            }

            deviceConfig = MemoryMarshal.Read<IMSCSI_DEVICE_CONFIGURATION>(Response);
            DeviceNumber = deviceConfig.DeviceNumber;
            DiskSize = deviceConfig.DiskSize;
            BytesPerSector = deviceConfig.BytesPerSector;
            ImageOffset = deviceConfig.ImageOffset;
            Flags = (DeviceFlags)deviceConfig.Flags;
            if (deviceConfig.FileNameLength == 0)
            {
                Filename = null;
            }
            else
            {
                Filename = MemoryMarshal.Cast<byte, char>(Response.Slice(PinnedBuffer<IMSCSI_DEVICE_CONFIGURATION>.TypeSize,
                                                                         deviceConfig.FileNameLength))
                    .ToString();
            }

            if (Flags.HasFlag(DeviceFlags.WriteOverlay))
            {
                WriteOverlayImagefile = MemoryMarshal.Cast<byte, char>(Response.Slice(PinnedBuffer<IMSCSI_DEVICE_CONFIGURATION>.TypeSize + deviceConfig.FileNameLength,
                                                                                      deviceConfig.WriteOverlayFileNameLength))
                    .ToString();
            }
        }

        finally
        {
            ArrayPool<byte>.Shared.Return(Request);

        }
    }

    /// <summary>
    /// Retrieves properties for an existing virtual disk.
    /// </summary>
    /// <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
    public DeviceProperties QueryDevice(uint DeviceNumber) => new(this, DeviceNumber);

    /// <summary>
    /// Modifies properties for an existing virtual disk.
    /// </summary>
    /// <param name="DeviceNumber">Device number of virtual disk to modify properties for.</param>
    /// <param name="FlagsToChange">Flags for which to change values for.</param>
    /// <param name="FlagValues">New flag values.</param>
    public void ChangeFlags(uint DeviceNumber, DeviceFlags FlagsToChange, DeviceFlags FlagValues)
    {

        Span<byte> Request = stackalloc byte[PinnedBuffer<IMSCSI_SET_DEVICE_FLAGS>.TypeSize];

        var changeFlags = new IMSCSI_SET_DEVICE_FLAGS(DeviceNumber, (uint)FlagsToChange, (uint)FlagValues);

        MemoryMarshal.Write(Request, ref changeFlags);

        NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle, NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_SET_DEVICE_FLAGS, 0U, Request, out var ReturnCode);

        if (ReturnCode != 0)
        {
            throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);
        }
    }

    /// <summary>
    /// Extends size of an existing virtual disk.
    /// </summary>
    /// <param name="DeviceNumber">Device number of virtual disk to modify.</param>
    /// <param name="ExtendSize">Number of bytes to extend.</param>
    public void ExtendSize(uint DeviceNumber, long ExtendSize)
    {

        Span<byte> Request = stackalloc byte[PinnedBuffer<IMSCSI_EXTEND_SIZE>.TypeSize];

        var changeFlags = new IMSCSI_EXTEND_SIZE(DeviceNumber, ExtendSize);

        MemoryMarshal.Write(Request, ref changeFlags);

        NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle, NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_SET_DEVICE_FLAGS, 0U, Request, out var ReturnCode);

        if (ReturnCode != 0)
        {
            throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);
        }
    }

    /// <summary>
    /// Checks if version of running Arsenal Image Mounter SCSI miniport servicing this device object is compatible with this API
    /// library. If this device object is not created by Arsenal Image Mounter SCSI miniport, an exception is thrown.
    /// </summary>
    public bool CheckDriverVersion() => CheckDriverVersion(SafeFileHandle);

    /// <summary>
    /// Checks if version of running Arsenal Image Mounter SCSI miniport servicing this device object is compatible with this API
    /// library. If this device object is not created by Arsenal Image Mounter SCSI miniport, an exception is thrown.
    /// </summary>
    public static bool CheckDriverVersion(SafeFileHandle SafeFileHandle)
    {
        NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle, NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_VERSION, 0U, null, out var ReturnCode);

        if (ReturnCode == CompatibleDriverVersion)
        {
            return true;
        }

        Trace.WriteLine($"Library version: {CompatibleDriverVersion:X4}");
        Trace.WriteLine($"Driver version: {ReturnCode:X4}");

        return false;
    }

    /// <summary>
    /// Retrieves the sub version of the driver. This is not the same as the API compatibility version checked for by
    /// CheckDriverVersion method. The version record returned by this GetDriverSubVersion method can be used to find
    /// out whether the latest version of the driver is loaded, for example to show a dialog box asking user whether to
    /// upgrade the driver. If driver does not support this version query, this method returns Nothing/null.
    /// </summary>
    public Version? GetDriverSubVersion()
    {
        Span<byte> buffer = stackalloc byte[4];

        try
        {
            var Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle, NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_VERSION, 0U, buffer, out var ReturnCode);

            Trace.WriteLine($"Library version: {CompatibleDriverVersion:X4}");
            Trace.WriteLine($"Driver version: {ReturnCode:X4}");

            if (ReturnCode != CompatibleDriverVersion)
            {
                return null;
            }

            var build = Response[0];
            var low = Response[1];
            var minor = Response[2];
            var major = Response[3];

            return new Version(major, minor, low, build);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public bool RescanScsiAdapter() => API.RescanScsiAdapter(DeviceInstance);

    /// <summary>
    /// Issues a SCSI bus rescan to find newly attached devices and remove missing ones.
    /// </summary>
    public void RescanBus()
    {

        try
        {
            NativeFileIO.DeviceIoControl(SafeFileHandle, NativeConstants.IOCTL_SCSI_RESCAN_BUS, default, 0);
        }

        catch (Exception ex)
        {
            Trace.WriteLine($"IOCTL_SCSI_RESCAN_BUS failed: {ex.JoinMessages()}");
            API.RescanScsiAdapter(DeviceInstance);

        }
    }

    /// <summary>
    /// Re-enumerates partitions on all disk drives currently connected to this adapter. No
    /// exceptions are thrown on error, but any exceptions from underlying API calls are logged
    /// to trace log.
    /// </summary>
    public void UpdateDiskProperties()
    {

        foreach (var device in GetDeviceList())
        {
            UpdateDiskProperties(device);
        }
    }

    /// <summary>
    /// Re-enumerates partitions on specified disk currently connected to this adapter. No
    /// exceptions are thrown on error, but any exceptions from underlying API calls are logged
    /// to trace log.
    /// </summary>
    public bool UpdateDiskProperties(uint DeviceNumber)
    {

        try
        {
            using (var disk = OpenDevice(DeviceNumber, 0))
            {

                if (!NativeFileIO.UpdateDiskProperties(disk.SafeFileHandle, throwOnFailure: false))
                {

                    Trace.WriteLine($"Error updating disk properties for device {DeviceNumber:X6}: {new Win32Exception().Message}");

                    return false;

                }
            }

            return true;
        }

        catch (Exception ex)
        {
            Trace.WriteLine($"Error updating disk properties for device {DeviceNumber:X6}: {ex.JoinMessages()}");

            return false;

        }
    }

    /// <summary>
    /// Opens a DiskDevice object for specified device number. Device numbers are created when
    /// a new virtual disk is created and returned in a reference parameter to CreateDevice
    /// method.
    /// </summary>
    public DiskDevice OpenDevice(uint DeviceNumber, FileAccess AccessMode)
    {

        try
        {
            var device_name = GetDeviceName(DeviceNumber);

            return device_name is null
                ? throw new DriveNotFoundException($"No drive found for device number {DeviceNumber:X6}")
                : new DiskDevice($@"\\?\{device_name}".AsMemory(), AccessMode);
        }

        catch (Exception ex)
        {
            throw new DriveNotFoundException($"Device {DeviceNumber:X6} is not ready", ex);

        }
    }

    /// <summary>
    /// Opens a DiskDevice object for specified device number. Device numbers are created when
    /// a new virtual disk is created and returned in a reference parameter to CreateDevice
    /// method. This overload requests a DiskDevice object without read or write access, that
    /// can only be used to query metadata such as size, geometry, SCSI address etc.
    /// </summary>
    public DiskDevice OpenDevice(uint DeviceNumber)
    {

        try
        {
            var device_name = GetDeviceName(DeviceNumber);

            return device_name is null
                ? throw new DriveNotFoundException($"No drive found for device number {DeviceNumber:X6}")
                : new DiskDevice($@"\\?\{device_name}");
        }

        catch (Exception ex)
        {
            throw new DriveNotFoundException($"Device {DeviceNumber:X6} is not ready", ex);

        }
    }

    /// <summary>
    /// Returns a PhysicalDrive or CdRom device name for specified device number. Device numbers
    /// are created when a new virtual disk is created and returned in a reference parameter to
    /// CreateDevice method.
    /// </summary>
    public string? GetDeviceName(uint DeviceNumber)
    {

        try
        {
            var raw_device = GetRawDeviceName(DeviceNumber);

            return raw_device is null ? null : NativeFileIO.GetPhysicalDriveNameForNtDevice(raw_device);
        }

        catch (Exception ex)
        {
            Trace.WriteLine($"Error getting device name for device number {DeviceNumber}: {ex.JoinMessages()}");
            return null;

        }
    }

    /// <summary>
    /// Returns an NT device path to the physical device object that SCSI port driver has created for a mounted device.
    /// This device path can be used even if there is no functional driver attached to the device stack.
    /// </summary>
    public string? GetRawDeviceName(uint DeviceNumber)
        => API.EnumeratePhysicalDeviceObjectPaths(DeviceInstance, DeviceNumber).FirstOrDefault();

    /// <summary>
    /// Returns a PnP registry property for the device object that SCSI port driver has created for a mounted device.
    /// </summary>
    public IEnumerable<string> GetPnPDeviceName(uint DeviceNumber, CmDevNodeRegistryProperty prop)
        => API.EnumerateDeviceProperty(DeviceInstance, DeviceNumber, prop);

}
