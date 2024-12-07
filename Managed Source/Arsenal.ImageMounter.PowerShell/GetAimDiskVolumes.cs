using Arsenal.ImageMounter.Devio.Server.Services;
using Arsenal.ImageMounter.IO.Native;
using System.Management.Automation;
using System.Runtime.Versioning;

namespace Arsenal.ImageMounter.PowerShell;

[Cmdlet(VerbsCommon.Get, "AimDiskVolumes")]
#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
public class GetAimDiskVolumes : Cmdlet
{
    [Parameter(Position = 0, ValueFromPipeline = true, HelpMessage = "Mounted virtual disk to open.")]
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

        if (VirtualDisk is not null)
        {
            var deviceName = VirtualDisk.GetDiskDeviceName();

            if (deviceName is null)
            {
                return;
            }

            DevicePath = $@"\\?\{deviceName}";
        }

        var volumes = NativeFileIO.EnumerateDiskVolumes(DevicePath).ToArray();

        WriteObject(volumes);
    }
}
