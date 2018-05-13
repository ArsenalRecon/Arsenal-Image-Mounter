Arsenal-Image-Mounter
=====================

Arsenal Image Mounter mounts the contents of disk images as complete disks in Microsoft Windows. Arsenal Image Mounter includes a virtual SCSI adapter (via a unique Storport miniport driver) which allows users to benefit from disk-specific features in Windows like integration with Disk Manager, access to Volume Shadow Copies, and more. As far as Windows is concerned, the contents of disk images mounted by Arsenal Image Mounter are “real” SCSI disks.

<i>For developers</i>, Arsenal Image Mounter source code and APIs are available for royalty-free use by open source projects. <b>Commercial projects (and other projects not licensed under an AGPL v3 compatible license - see http://www.fsf.org/licensing/licenses/agpl-3.0.html) that would like to use Arsenal Image Mounter source code and/or APIs must contact us (https://ArsenalRecon.com/contact/) to obtain alternative licensing.</b>

<i>For end users</i>, Arsenal Image Mounter’s full functionality (along with all our other tools) is available as part of an affordable monthly subscription. If Arsenal Image Mounter is licensed, it runs in "Professional Mode.” If Arsenal Image Mounter is run without a license, it will run in "Free Mode" and provide core functionality.

Please see Arsenal Image Mounter’s product page: https://ArsenalRecon.com/weapons/image-mounter for more details.

Supporting Arsenal Image Mounter
--------------------------------

We appreciate your help making commercial projects aware of Arsenal Image Mounter’s capabilities, because commercial licensing of our source code and APIs supports ongoing development. On a related note, we know that some commercial projects are using Arsenal Image Mounter’s source code and APIs without being properly licensed… we also appreciate being alerted to these situations so we can nudge those projects appropriately.

More Details on Licensing and Contributions
-------------------------------------------

We chose a dual-license for Arsenal Image Mounter (more specifically, Arsenal Image Mounter’s source code and APIs) to allow its royalty-free use by open source projects, but require financial support from commercial projects.

Arsenal Consulting, Inc. (d/b/a Arsenal Recon) retains the copyright to Arsenal Image Mounter, including the Arsenal Image Mounter source code and APIs, being made available under terms of the Affero General Public License v3. Arsenal Image Mounter source code and APIs may be used in projects that are licensed so as to be compatible with AGPL v3. If your project is not licensed under an AGPL v3 compatible license and you would like to use Arsenal Image Mounter source code and/or APIs, contact us to obtain alternative licensing.

Contributors to Arsenal Image Mounter must sign the Arsenal Contributor Agreement ("ACA"). The ACA gives Arsenal and the contributor joint copyright interests in the source code.

Disclaimer
----------

Arsenal Image Mounter including its kernel driver, APIs, command line and graphical user applications ("the Software") are provided "AS IS" and "WITH ALL FAULTS," without warranty of any kind, including without limitation the warranties of merchantability, fitness for a particular purpose, and non-infringement. Arsenal makes no warranty that the Software is free of defects or is suitable for any particular purpose. In no event shall Arsenal be responsible for loss or damages arising from the installation or use of the Software, including but not limited to any indirect, punitive, special, incidental, or consequential damages of any character including, without limitation, damages for loss of goodwill, work stoppage, computer failure or malfunction, or any and all other commercial damages or losses. The entire risk as to the quality and performance of the Software is borne by you. Should the Software prove defective, you and not Arsenal assume the entire cost of any service and repair.

Folder Specific Information
----------------------------

Arsenal Image Mounter CLI is a .NET 4.0 tool that provides most of Arsenal Image Mounter’s functionality. The command “AIM_CLI /?” displays basic syntax for using Arsenal Image Mounter CLI.

Arsenal Image Mounter Low Level is a tool that does not use .NET and provides more “low level” access to the Arsenal Image Mounter driver. The command “AIM_LL /?” displays basic syntax for using Arsenal Image Mounter Low Level. 