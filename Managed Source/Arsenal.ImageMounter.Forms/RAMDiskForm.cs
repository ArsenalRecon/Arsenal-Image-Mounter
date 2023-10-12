using Arsenal.ImageMounter.Devio.Server.Services;
using Arsenal.ImageMounter.IO.Native;
using LTRData.Extensions.Formatting;
using System;
using System.Windows.Forms;

namespace Arsenal.ImageMounter.Dialogs;

/// <summary>
/// Provides interactive GUI for RAM disk creation
/// </summary>
public static class RAMDiskForm
{
    /// <summary>
    /// Create RAM disk interactively by showing dialog that asks for size etc.
    /// </summary>
    /// <param name="_"></param>
    /// <param name="adapter"></param>
    /// <returns></returns>
    public static RAMDiskService? InteractiveCreate(IWin32Window? _, ScsiAdapter adapter)
    {
        var strsize = Microsoft.VisualBasic.Interaction.InputBox("Enter size in MB", "RAM disk");

        if (strsize is null || string.IsNullOrWhiteSpace(strsize))
        {
            return null;
        }

        if (!long.TryParse(strsize, out var size_mb) || size_mb <= 0)
        {
            return null;
        }

        var ramdisk = RAMDiskService.Create(adapter, size_mb << 20, InitializeFileSystem.NTFS);

        if (ramdisk.MountPoint is not null)
        {
            try
            {
                NativeFileIO.BrowseTo(ramdisk.MountPoint);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Explorer window for created RAM disk: {ex.JoinMessages()}");
            }
        }

        return ramdisk;
    }
}
