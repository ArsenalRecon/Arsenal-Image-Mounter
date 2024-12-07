using Arsenal.ImageMounter.IO.Native;
using System.Management.Automation;
using System.Runtime.Versioning;

namespace Arsenal.ImageMounter.PowerShell;

[Cmdlet(VerbsCommon.Get, "AimVolumeMountPoints")]
#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
public class GetAimVolumeMountPoints : Cmdlet
{
    [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, HelpMessage = "Volume path")]
    public string[] VolumePath { get; set; } = null!;

    protected override void ProcessRecord()
    {
        if (VolumePath is null)
        {
            throw new PSArgumentException("Needs VolumePath parameter");
        }

        var mountPoints = VolumePath.SelectMany(NativeFileIO.EnumerateVolumeMountPoints).ToArray();

        WriteObject(mountPoints);
    }
}
