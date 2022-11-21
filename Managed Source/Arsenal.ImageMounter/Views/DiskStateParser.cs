using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Views;

[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public static class DiskStateParser
{
    public static IEnumerable<DiskStateView> GetSimpleView(ScsiAdapter portnumber, IEnumerable<DeviceProperties> deviceProperties)
        => GetSimpleViewSpecial<DiskStateView>(portnumber, deviceProperties);

    public static IEnumerable<T> GetSimpleViewSpecial<T>(ScsiAdapter portnumber, IEnumerable<DeviceProperties> deviceProperties) where T : DiskStateView, new()
    {

        try
        {
            var ids = NativeFileIO.GetDevicesScsiAddresses(portnumber);

            string? getid(DeviceProperties dev)
            {
                if (ids.TryGetValue(dev.DeviceNumber, out var result))
                {
                    return result;
                }
                else
                {
                    Trace.WriteLine($"No PhysicalDrive object found for device number {dev.DeviceNumber:X6}");
                    return null;
                }
            }

            return deviceProperties.Select(dev =>
            {
                var view = new T()
                {
                    DeviceProperties = dev,
                    DeviceName = getid(dev),
                    FakeDiskSignature = dev.Flags.HasFlag(DeviceFlags.FakeDiskSignatureIfZero)
                };

                if (view.DeviceName is not null)
                {
                    try
                    {
                        view.DevicePath = $@"\\?\{view.DeviceName}";
                        using var device = new DiskDevice(view.DevicePath.AsMemory(), FileAccess.Read);
                        view.RawDiskSignature = device.DiskSignature;
                        view.NativePropertyDiskOffline = device.DiskPolicyOffline;
                        view.NativePropertyDiskReadOnly = device.DiskPolicyReadOnly;
                        view.StorageDeviceNumber = device.StorageDeviceNumber;
                        var drive_layout = device.DriveLayoutEx;
                        view.DiskId = (drive_layout as NativeFileIO.DriveLayoutInformationGPT)?.GPT.DiskId;
                        if (device.HasValidPartitionTable)
                        {
                            view.NativePartitionLayout = drive_layout?.DriveLayoutInformation.PartitionStyle;
                        }
                        else
                        {
                            view.NativePartitionLayout = default;
                        }
                    }

                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Error reading signature from MBR for drive {view.DevicePath}: {ex.JoinMessages()}");

                    }

                    try
                    {
                        view.Volumes = NativeFileIO.EnumerateDiskVolumes(view.DevicePath).ToArray();
                        view.MountPoints = view.Volumes?
                            .SelectMany(vol => NativeFileIO.EnumerateVolumeMountPoints(vol))
                            .Select(mnt => mnt.ToString())
                            .ToArray();
                    }

                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Error enumerating volumes for drive {view.DevicePath}: {ex.JoinMessages()}");

                    }
                }

                return view;

            });
        }

        catch (Exception ex)
        {
            Trace.WriteLine($"Exception in GetSimpleView: {ex}");

            throw new Exception("Exception generating view", ex);

        }
    }
}