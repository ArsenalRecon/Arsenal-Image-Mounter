
''''' Enums.vb
''''' .NET definitions of the same flags and structures as in phdskmnt.h
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''



''' <summary>
''' Values for flag fields used when creating, querying or modifying virtual disks.
''' </summary>
<Flags()>
Public Enum DeviceFlags As UInt32

    ''' <summary>
    ''' Creates a read-only virtual disk.
    ''' </summary>
    [ReadOnly] = &H1UI
    ''' <summary>
    ''' Creates a virtual disk with "removable" properties reported to the operating system.
    ''' </summary>
    Removable = &H2UI
    ''' <summary>
    ''' Specifies that image files are created with sparse attribute.
    ''' </summary>
    SparseFile = &H4UI

    ''' <summary>
    ''' Creates a virtual disk with device type hard disk volume.
    ''' </summary>
    DeviceTypeHD = &H10UI
    ''' <summary>
    ''' Creates a virtual disk with device type floppy disk.
    ''' </summary>
    DeviceTypeFD = &H20UI
    ''' <summary>
    ''' Creates a virtual disk with device type CD-ROM/DVD-ROM etc.
    ''' </summary>
    DeviceTypeCD = &H30UI

    ''' <summary>
    ''' Creates a virtual disk backed by a image file on disk. The Filename parameter specifies image file to use.
    ''' </summary>
    TypeFile = &H100UI
    ''' <summary>
    ''' Creates a virtual disk backed by virtual memory. If Filename parameter is also specified, contents of that file
    ''' will be loaded to the virtual memory before driver starts to service I/O requests for it.
    ''' </summary>
    TypeVM = &H200UI
    ''' <summary>
    ''' Creates a virtual disk for which storage is provided by an I/O proxy application.
    ''' </summary>
    TypeProxy = &H300UI

    ''' <summary>
    ''' Specifies that proxy application will be contacted directly through a named pipe. The Filename parameter specifies
    ''' path to named pipe.
    ''' </summary>
    ProxyTypeDirect = &H0UI
    ''' <summary>
    ''' Specifies that proxy application will be contacted through a serial communications port. The Filename parameter
    ''' specifies port optionally followed by colon, space and a port configuration string using same format as MODE COM
    ''' command. Example: "COM1: BAUD=9600 PARITY=N STOP=1 DATA=8"
    ''' </summary>
    ProxyTypeComm = &H1000UI
    ''' <summary>
    ''' Specifies that proxy application will be contacted through a TCP/IP port. The Filename parameter specifies host
    ''' name or IP address optionally followed by colon and port number. If port number is omitted a default value of 9000
    ''' is used.
    ''' </summary>
    ProxyTypeTCP = &H2000UI
    ''' <summary>
    ''' Specifies that proxy application will be contacted through shared memory. The Filename parameter specifies object
    ''' name of shared memory block and synchronization event objects.
    ''' </summary>
    ProxyTypeSharedMemory = &H3000UI

    ''' <summary>
    ''' This flag can only be set by the driver and may be included in the response Flags field from QueryDevice method.
    ''' It indicates that virtual disk contents have changed since created or since flag was last cleared. This flag can be
    ''' cleared by specifying it in FlagsToChange parameter and not in Flags parameter in a call to ChangeFlags method.
    ''' </summary>
    Modified = &H10000UI

    ''' <summary>
    ''' If this flag is set, the driver will report a random fake disk signature to Windows in case device is read-only,
    ''' existing disk signature is zero and master boot record has otherwise apparently valid data.
    ''' </summary>
    FakeDiskSignatureIfZero = &H20000UI

End Enum


