Arsenal-Image-Mounter
=====================

Arsenal Image Mounter mounts the contents of disk images as complete disks in Microsoft Windows. Arsenal Image Mounter includes a virtual SCSI adapter (via a unique Storport miniport driver) which allows users to benefit from disk-specific features in Windows like integration with Disk Manager, access to Volume Shadow Copies, and more. As far as Windows is concerned, the contents of disk images mounted by Arsenal Image Mounter are “real” SCSI disks.

Arsenal Image Mounter is only available here under the AGPL-3.0 license (http://www.fsf.org/licensing/licenses/agpl-3.0.html). Arsenal Consulting, Inc. (d/b/a Arsenal Recon) retains the copyright to Arsenal Image Mounter.

Contributors to Arsenal Image Mounter must sign the Arsenal Contributor Agreement ("ACA").  The ACA gives Arsenal and the contributor joint copyright interests in the contributed code.

If your project is not licensed under an AGPL v3 compatible license, contact Arsenal Recon directly regarding alternative licensing:

http://ArsenalRecon.com/contact/

Folder specific information:

ArsenalImageMounterMountTool.exe - Compiled, ready-to-run, one-piece simple GUI mount tool. Installs necessary driver components if not already installed.  Complete functionality is available when run on Windows 8, but all basic functionality exists when run on Windows 7.  This mount tool application is primarily intended to show what the Arsenal Image Mounter source code can be used for. libewf and zlib binaries (to facilitate EnCase/EWF image mounting) are being included with MountTool under their respective licenses - see lgpl-3.0.txt and zlib license.txt.

ArsenalImageMounterControl.exe - Provides command line access to basic Arsenal Image Mounter features.

Screenshot.png - Example of MountTool's disk signature collision reporting and remediation.  Note - remediation only available in read/write mode.

