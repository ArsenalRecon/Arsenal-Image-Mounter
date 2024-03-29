
What's new, driver and low level API/tools version 1.0.7.22
-----------------------------------------------------------

* Driver now supports CD/DVD emulation.

* Corrected some problems with missing paths etc in driver source project
  files. Driver source code changed from C to C++. Use WDK 7.1.0 or Visual
  Studio 2015 with WDK 10 to build the driver.

* A few corrections to message texts and similar in low level API and command
  line tool. Also a safer way for the API to recognize the correct disk object
  as a particular virtual disk, to avoid confusion and the risk of formatting
  the wrong drive when creating and formatting a new virtual disk from command
  line.

* ARM architecture versions of driver and unmanaged command line tool and API.
  Can be used on Windows 10 IoT versions, for example on Raspberry Pi. Run
  "devcon install" to install driver, or use "dism" tool to include driver
  package in a distribution image.

* For directly mounted (raw) image files or RAM disks, the driver now supports
  live extending of disk size. Use for instance aim_ll -e -u 000000 -s 1G
  command line to extend device 000000 by 1 GB. You can then create a new
  partition in the new space, or extend an existing partition over it using
  Disk Management, diskpart or similar tools.

What's new, driver version 1.0.5.014 and API library version 1.0.021
--------------------------------------------------------------------

* Bug fixes in driver. New driver embedded with Mount Tool.

* Driver caused blue-screen crash when virtual SCSI port device was disabled
  or uninstalled through Device Manager while virtual disk drives were still
  mounted. This problem has now been corrected.

* Driver optimization: Pool memory usage was very high when loading disk image
  contents into virtual memory, which could potentially cause various problems
  with other drivers and applications meanwhile. Rewrote image file reading
  process completely. This is essentially same code fixed as in ImDisk Virtual
  Disk Driver version 1.9.2.

* The driver now responds with SRB_STATUS_BUSY instead of
  SRB_STATUS_NOT_POWERED while a newly mounted virtual disk drive is becoming
  ready. This should make drive mounting faster and more reliable.

What's new, API library version 1.0.020
---------------------------------------

* Some users reported error message dialogs when device list is refreshed in
  Mount Tool. A possible cause has been identified and protected so that an
  error while opening a device should not cause an unhandled exception message.

What's new, driver version 1.0.4.013 and API library version 1.0.019
--------------------------------------------------------------------

* Write-temporary mode (also known as "write-overlay") is now supported for
  most virtual disk formats mounted through DiscUtils.dll as well as the
  existing support through libewf.dll. The API needed to change somewhat to
  make this possible, so existing application that uses this API likely
  need to modify code that creates new virtual disks.

* The driver now supports emulating removable disk devices that can be
  removed using right-click/eject in for example Disk Management.

* The routine that scans for new disks after creating a new disk device is
  now much faster, more efficient and should have less compatibility
  problems.

* The driver now supports directly mounting an existing "PhysicalDrive"
  object as a new virtual SCSI disk. This makes it possible to emulate a
  full partitionable disk using a USB thumb drive. Useful for example to
  access partitions other than the first one on a USB thumb drive.

* Mounting of images using libewf.dll now supports the new .ex01 formats as
  well as old .e01 formats.

* Included newer version of DiscUtils.dll with some fixes and improvements
  for some virtual disk formats.

What's new, API library version 1.0.014
---------------------------------------

* Disks mounted from ewf images through libewf.dll now use sector size from
  ewf image. Previously, sector size was hard-coded as 512 bytes which
  caused Windows to misread partition table and similar if sector size was
  different from that number.

What's new, driver version 1.0.1 and API library version 1.0.013
----------------------------------------------------------------

* MountTool previously crashed on start if any disk drive in the system was
  exclusively locked by another application. This problem has been corrected
  in this version.

* Driver now supports drives larger than 2 TB. This requires Windows Server
  2003 or Windows Vista or later, there is still a 2 TB limit on Windows XP.

* MountTool now shows a "mount options" dialog after image file has been
  selected. This dialog shows different options depending on what kind of
  image file is opened.

* A new feature in the driver enables mounting VHD files from Windows Backup
  that have blank disk signature, even in read-only mode. It is done by
  reporting a random disk signature when Windows reads MBR data from the
  disk. This feature is activated from the "mount options" dialog in
  MountTool, or using an option flag when integrating with the API.

* Driver now supports up to 8 MB transfer size instead of old limit of 64 KB.
  For compatibility with existing proxy service providers, large I/O requests
  are automatically split into several smaller requests if proxy service uses
  smaller buffer size. This could severely degrade performance, so when
  developing proxy service providers, use at least 8 MB buffer size.

* Some users have experienced extremely bad performance on Windows XP. A
  possible reason has been identified and corrected. Please report if such
  problems are still seen!

Managed API 3.9.204
-------------------

* Optimizations and modernizations in many places.

* Split Windows-specific code to isolated libraries so that other libraries
  can be referenced by applications that target other platforms.

* Target frameworks are now .NET Framework 4.8 and .NET 6.0 only.

* Integrated with libqcow.dll to support Qemu images and libaff4.dll to
  support AFF4 images.

* The build process now generates nuget packages to the directory specified
  by LocalNuGetPath environment variable.

* Projects now reference most third-party .NET dependencies as packages
  instead of .dll files directly.

Managed API 3.9.230
-------------------

* Library projects merged into two projects in C#. Also lots of optimizations
  and code cleanup that could be done as we have dropped support for older
  .NET Framework versions.

* Added .NET 7.0 as a target framework. The library projects and command line
  tool now target all of .NET Framework 4.8, .NET 6.0 and .NET 7.0. Since .NET
  6.0 is the latest LTS version, it makes sense to keep it supported by AIM.
  Also, .NET Framework 4.8 is the pre-installed version on latest Windows
  versions and still used but lots of projects as well.

* NuGet packages Arsenal.ImageMounter and Arsenal.ImageMounter.Forms that
  can be referenced directly from other Visual Studio and .NET projects.
