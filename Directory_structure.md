Description of directory structure for Arsenal Image Mounter repository
-----------------------------------------------------------------------

Regarding licenses for external components such as libewf, zlib, libaff4,
libqcow etc, see information about there respective license in
<b>Third-Party-Licenses</b> directory.



--------------------------------
Graphical applications directory
================================

ArsenalImageMounter.exe has been moved to ArsenalRecon.com web site.
https://arsenalrecon.com/weapons/image-mounter/

One-piece (with the exception of some third-party libraries for supporting
forensics image file formats) powerful tool for mounting disk image files as
virtual drives. Supports raw disk images and various virtual machine image
formats through integrated DiscUtils library. Also supports certain forensics
image formats if libewf.dll, libaff4.dll and libqcow.dll are also installed.
Automatically installs necessary driver components if not already installed.

ArsenalImageMounter.exe requires .NET 6.0, which can be installed on Windows 7
and later.
https://dotnet.microsoft.com/en-us/download/dotnet/6.0

Tested on Windows 10 and 11, but should also work on Windows 7, 8 and 8.1.


-----------------------------------
Command line applications directory
===================================

aim_cli.exe
-----------

Command line tool for mounting various disk image formats as virtual drives in
a way similar to the graphical ArsenalImageMounter.exe.

Does not include driver setup files. Driver could be set up separately using
either command line tool aim_ll.exe or graphical ArsenalImageMounter.exe, or
the separate packages in the DriverSetup directory.

aim_cli.exe comes in two different versions.

One version is available in <b>Command line applications</b> foder. This
version requires .NET Framework 4.8, which is included by default in latest
versions of Windows 10, 11 and Server 2022 and can be installed separately on
Windows 7, Server 2008 R2 and later.

.NET Framework 4.8 is available here:
https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48

The version of aim_cli.exe included with graphical application linked above is
a different version that runs on .NET 6.0. It has generally better performance
and is recommended for most use cases.

Tested on Windows 7, 8.1, 10, 11 and Linux. When running on other platforms
than Windows, available features are limited to what does not require kernel
level drivers. For example image file format conversion is available in this
scenario, but not mounting images as virtual disks.


aim_ll.zip
----------
Command line tools that provides access to most features of virtual SCSI
miniport driver that is used with Arsenal Image Mounter. Command line syntax
is very similar to that of ImDisk Virtual Disk Driver, so most commands and
scripting work in a similar way. There are also command line switches for
installing or uninstalling the virtual SCSI miniport driver.

None of the files in this zip archive require any .NET components.

Tested on Windows 7, 8.1 and 10, but should also work on any Windows version
from Windows 2000 and up.


---------------------
DriverSetup directory
=====================

Setup tools and signed driver packages that can be used to install the driver
components alone.


ArsenalImageMounterGUISetup.exe
-------------------------------
One-piece simple driver setup GUI application that includes everything to
automatically install the correct driver for current version of Windows.

ArsenalImageMounterCLISetup.exe:
Command line version of ArsenalImageMounterGUISetup.exe, with same
functionality and requirements.


Both ArsenalImageMounterGUISetup.exe and ArsenalImageMounterCLISetup.exe
require .NET Framework 4.8, which is included by default in latest versions of
Windows 10 and 11.


DriverSetup.7z
--------------
Both driver setup files and command line tool aim_ll.exe as a 7-zip archive.
Useful for automated driver setup, for example for use from a script.

Application files in this archive do not require any .NET components.


DriverSetup.zip
---------------
Driver setup files only (sys, cat and inf files). For use when integrating
driver setup with, for example, other driver setup packages.


-------------
API directory
=============

Arsenal.ImageMounter.dll
------------------------
.NET API library. This package has been moved to NuGet:

Requires either of .NET Framework 4.8 or .NET 6.0.

API reference is available online:
http://static.ltr-data.se/library/ArsenalImageMounter

aimapi.zip
----------
DLL files that can be used from other applications to use most features of
virtual SCSI miniport driver. Files in this archive do not require any .NET
Framework components and should work on any Windows version from 2000 and up.

There are .lib and .h files included which can be imported into C/C++ projects
to use the API.


------------------------
Managed Source directory
========================

Visual Studio 2022 solution with source projects for .NET API libraries,
command line tools and some proof-on-concept graphical applications.


--------------------------
Unmanaged Source directory
==========================

Visual Studio 2017-2022 solution with source projects for native (non-.NET)
driver, API library and command line tool. Driver project requires WDK 8.1 or
later.

The driver project directory also contains files for building with WDK 7
build.exe environment, to support targeting older Windows versions than
Windows 7.


-------------------
MountTool directory
===================

Compiled exe file for the sample graphical under Managed Source directory.
The application requires .NET Framework 4.8.

