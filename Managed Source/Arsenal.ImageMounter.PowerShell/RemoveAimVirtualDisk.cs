using Arsenal.ImageMounter.Devio.Server.Services;
using System.Management.Automation;

namespace Arsenal.ImageMounter.PowerShell;

[Cmdlet(VerbsCommon.Remove, "AimVirtualDisk")]
public class RemoveAimVirtualDisk : Cmdlet
{
    static RemoveAimVirtualDisk()
    {
        if (!PowerShellGlobals.Initialized)
        {
            throw new InvalidOperationException("Error loading Arsenal Image Mounter libraries");
        }
    }

    [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, HelpMessage = "Mounted virtual disk to open.")]
    public DevioServiceBase VirtualDisk { get; set; } = null!;

    protected override void ProcessRecord()
    {
        if (VirtualDisk is null)
        {
            throw new PSArgumentException("Needs VirtualDisk parameter");
        }

        VirtualDisk.Dispose();
    }

}
