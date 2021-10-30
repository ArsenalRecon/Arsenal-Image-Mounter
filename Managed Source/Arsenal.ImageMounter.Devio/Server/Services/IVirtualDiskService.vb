''''' DevioServiceBase.vb
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Threading

Namespace Server.Services

    Public Interface IVirtualDiskService
        Inherits IDisposable

        Event ServiceShutdown As EventHandler

        Event ServiceStopping As EventHandler

        Event ServiceUnhandledException As ThreadExceptionEventHandler

        ReadOnly Property IsDisposed As Boolean

        ReadOnly Property HasDiskDevice As Boolean

        ReadOnly Property SectorSize As UInteger

        ReadOnly Property DiskSize As Long

        ReadOnly Property Description As String

        Function GetDiskDeviceName() As String

        Sub RemoveDevice()

        Sub RemoveDeviceSafe()

        Sub WaitForServiceThreadExit()

    End Interface

End Namespace
