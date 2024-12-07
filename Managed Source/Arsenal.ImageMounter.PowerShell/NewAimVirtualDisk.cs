using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Devio.Server.Interaction;
using Arsenal.ImageMounter.Devio.Server.Services;
using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using System.Management.Automation;
using System.Runtime.Versioning;

namespace Arsenal.ImageMounter.PowerShell;

public enum VirtualDiskType
{
    Disk = 0x0,
    CdRom = 0x1,
    RemovableDisk = 0x2
}

[Cmdlet(VerbsCommon.New, "AimVirtualDisk")]
#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
public class NewAimVirtualDisk : Cmdlet
{
    private static ScsiAdapter? ScsiAdapter;

    [Parameter(Position = 0, HelpMessage = "Path to image file to mount.", Mandatory = true)]
    public string FileName { get; set; } = null!;

    [Parameter(HelpMessage = "Path to file used for storing changes while original image is kept untouched.")]
    public string? WriteOverlay { get; set; }

    [Parameter(HelpMessage = "Stores changes temporarily in RAM while original image is kept untouched.")]
    public SwitchParameter WriteOverlayMem { get; set; }

    [Parameter(HelpMessage = "Delete file specified by WriteOverlay parameter automatically when image is dismounted.")]
    public SwitchParameter AutoDelete{ get; set; }

    [Parameter(HelpMessage = "Type of virtual disk.")]
    public VirtualDiskType? DeviceType { get; set; }

    [Parameter(HelpMessage = "Initial buffer size for communication between driver and user mode image format provider.")]
    public int? BufferSize { get; set; }

    [Parameter(HelpMessage = "Create a modifyable virtual disk. If not specified, virtual disk is read only.")]
    public SwitchParameter Writable { get; set; }

    [Parameter(HelpMessage = "Fake disk signature if zero.")]
    public SwitchParameter FakeSig { get; set; }

    [Parameter(HelpMessage = "Fake master boot record. For use with single-partition images without a partition table to emulerate a full disk.")]
    public SwitchParameter FakeMBR { get; set; }

    [Parameter(HelpMessage = "Automatically bring disk online and assign drive letters to partitions.")]
    public SwitchParameter Online { get; set; }

    [Parameter(HelpMessage = "Disk image format provider. If not specified, automatically selected by file name extension.")]
    public DevioServiceFactory.ProviderType? Provider { get; set; }

    protected override void ProcessRecord()
    {
        ScsiAdapter ??= new();

        IDevioProvider? provider = null;
        DevioServiceBase? service = null;

        try
        {
            var access = FileAccess.Read;

            if (WriteOverlayMem)
            {
                if (WriteOverlay is not null)
                {
                    throw new PSArgumentException("WriteOverlayMem and WriteOverlay cannot be combined", nameof(WriteOverlayMem));
                }

                WriteOverlay = @"\\?\awealloc";
            }

            if (Writable && WriteOverlay is null)
            {
                access |= FileAccess.Write;
            }

            if (Provider.HasValue)
            {
                provider = DevioServiceFactory.GetProvider(FileName, access, Provider.Value);
            }
            else
            {
                provider = DevioServiceFactory.GetProvider(FileName, access);
            }

            if (FakeMBR)
            {
                provider = new DevioProviderWithFakeMBR(provider);
            }

            service = new DevioDrvService(provider, ownsProvider: true, initialBufferSize: BufferSize ?? DevioDrvService.DefaultInitialBufferSize);

            if (DeviceType.HasValue)
            {
                switch (DeviceType.Value)
                {
                    case VirtualDiskType.RemovableDisk:
                        service.AdditionalFlags |= DeviceFlags.Removable;
                        break;

                    case VirtualDiskType.CdRom:
                        service.AdditionalFlags |= DeviceFlags.DeviceTypeCD;
                        break;

                    default:
                        break;
                }
            }

            if (FakeSig)
            {
                service.AdditionalFlags |= DeviceFlags.FakeDiskSignatureIfZero;
            }

            if (!Writable)
            {
                service.AdditionalFlags |= DeviceFlags.ReadOnly;
            }

            if (WriteOverlay is not null)
            {
                service.WriteOverlayImageName = WriteOverlay;
                service.AdditionalFlags |= DeviceFlags.WriteOverlay;
            }

            service.StartServiceThreadAndMount(ScsiAdapter, 0);

            if (Online)
            {
                var device_name = $@"\\?\{service.GetDiskDeviceName()}";

                using (var device = new DiskDevice(device_name, FileAccess.ReadWrite))
                {
                    if (device.DiskPolicyReadOnly == true
                        && (Writable || WriteOverlay is not null))
                    {
                        device.DiskPolicyReadOnly = false;
                    }

                    if (device.DiskPolicyOffline == true)
                    {
                        device.DiskPolicyOffline = false;
                    }

                    device.UpdateProperties();
                }

                NativeFileIO.OnlineDiskVolumes(device_name);
            }

            WriteObject(service);
        }
        catch
        {
            service?.Dispose();
            provider?.Dispose();

            throw;
        }
    }
}

