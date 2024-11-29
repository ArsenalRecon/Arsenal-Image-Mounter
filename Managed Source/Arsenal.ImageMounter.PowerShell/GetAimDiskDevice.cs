using Arsenal.ImageMounter.Devio.Server.Services;
using Arsenal.ImageMounter.IO.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Arsenal.ImageMounter.PowerShell;

[Cmdlet(VerbsCommon.Get, "AimDiskDevice")]
#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
public class GetAimDiskDevice : Cmdlet
{
    [Parameter(Position = 0, HelpMessage = "Mounted virtual disk to open.")]
    public DevioServiceBase? VirtualDisk { get; set; }

    [Parameter(HelpMessage = @"Path to disk device to open, such as \\?\PhysicalDrive1")]
    public string? DevicePath { get; set; }

    [Parameter(HelpMessage = "Open virtual disk for both reading and writing. Without this parameter, disk is opened in read-only mode.")]
    public SwitchParameter Writable { get; set; }

    protected override void ProcessRecord()
    {
        if (!(VirtualDisk is not null ^ DevicePath is not null))
        {
            throw new PSArgumentException("Needs either of VirtualDisk or DevicePath parameters, but not both.");
        }

        var accessMode = FileAccess.Read;

        if (Writable)
        {
            accessMode |= FileAccess.Write;
        }

        var disk = VirtualDisk is not null
            ? VirtualDisk.OpenDiskDevice(accessMode)
            : DevicePath is not null
            ? new DiskDevice(DevicePath, accessMode)
#if NET7_0_OR_GREATER
            : throw new UnreachableException();
#else
            : throw new InvalidOperationException();
#endif

        try
        {
            WriteObject(disk);
        }
        catch
        {
            disk?.Dispose();
            throw;
        }
    }
}
