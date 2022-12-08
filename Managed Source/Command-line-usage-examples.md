Command line usage examples
===========================

Command Prompt
--------------

* To mount an image file called <b>D:\image.e01</b> in write-temporary mode
  with write overlay file <b>D:\image.e01.diff</b>:

`aim_cli --mount --filename=D:\image.e01 --writeoverlay=D:\image.e01.diff --background`

You will see output with information about mount points and how to dismount
the virtual disk again.

Notice that mount and dismount require administrative priviliges so you need
to start Command Prompt as administrator.

* To convert an image file called <b>D:\image.e01</b> to <b>D:\image.vhdx</b>:

`aim_cli --filename=D:\image.e01 --convert=D:\image.vhdx`

* To calculate MD5 and SHA1 checksums for contents within an image file called
  <b>D:\image.e01</b>:

`aim_cli --filename=D:\image.e01 --checksum`

Converting between image file formats or calculating checksums in this way
does not mount the images and it does not require administative privileges.
It also works on other platforms than Windows.

* To acquire an image file from contents of a physical disk:

`aim_cli --device=\\?\PhysicalDrive2 --saveas=D:\image.e01`

* To convert an image file called <b>D:\image.e01</b> to raw data and write it
  to a physical disk:

`aim_cli --filename=D:\image.e01 --convert=\\?\PhysicalDrive2`

Notice that reading from or writing to physical disks require administrative
priviliges so you need to start Command Prompt as administrator.

To use the API in PowerShell 7.2.x and later
--------------------------------------------

* To mount an image file called <b>D:\image.e01</b> in write-temporary mode
  with write overlay file <b>D:\image.e01.diff</b>:

```
### Load DLL file
Add-Type -Path .\Arsenal.ImageMounter.dll

### Create ScsiAdapter instance
$adapter = [Arsenal.ImageMounter.ScsiAdapter]::new()

### Create service object for image file and instruct it to use libewf for mounting
$service = [Arsenal.ImageMounter.Devio.Server.Interaction.DevioServiceFactory]::GetService('D:\test.E01', [Arsenal.ImageMounter.Devio.Server.Interaction.DevioServiceFactory+VirtualDiskAccess]::ReadOnly, [Arsenal.ImageMounter.Devio.Server.Interaction.DevioServiceFactory+ProviderType]::LibEwf)

### Set write-overlay path 
$service.WriteOverlayImageName = 'D:\test.E01.diff'

### Start servicing image and call drive to create a virtual disk for it
$service.StartServiceThreadAndMount($adapter, [Arsenal.ImageMounter.DeviceFlags]::WriteOverlay -bor [Arsenal.ImageMounter.DeviceFlags]::ReadOnly)

### Enumerate mount points for virtual disk
[Arsenal.ImageMounter.IO.NativeFileIO]::EnumerateDiskVolumesMountPoints('\\?\' + $service.GetDiskDeviceName()) | ForEach-Object {
    "Drive letter: " + $_
}

### Dispose service object. This will dismount the virtual disk
$service.Dispose()
```

To dismount all virtual disks mounted by Arsenal Image Mounter:

```
### Load DLL file
Add-Type -Path .\Arsenal.ImageMounter.dll

### Create ScsiAdapter instance
$adapter = [Arsenal.ImageMounter.ScsiAdapter]::new()

### Instruct driver to remove all mounted devices
$adapter.RemoveAllDevices()
```

Notice that that mount and dismount require administrative priviliges so you
need to start PowerShell as administrator.
