''''' MultiPartFileStream.vb
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.IO
Imports Arsenal.ImageMounter.Devio.Server.GenericProviders

Namespace Server.SpecializedProviders

    Public Class MultiPartFileStream
        Inherits CombinedSeekStream

        Public Sub New(Imagefiles As String(), DiskAccess As FileAccess)
            Me.New(Imagefiles, DiskAccess, FileShare.Read Or FileShare.Delete)

        End Sub

        Public Sub New(Imagefiles As String(), DiskAccess As FileAccess, ShareMode As FileShare)
            MyBase.New(DiskAccess.HasFlag(FileAccess.Write), OpenImagefiles(Imagefiles, DiskAccess, ShareMode))

        End Sub

        Private Shared Function OpenImagefiles(Imagefiles As String(), DiskAccess As FileAccess, ShareMode As FileShare) As FileStream()
            If Imagefiles Is Nothing Then
                Throw New ArgumentNullException(NameOf(Imagefiles))
            End If
            If Imagefiles.Length = 0 Then
                Throw New ArgumentException("No image file names provided.", NameOf(Imagefiles))
            End If

            Dim imagestreams As FileStream() = Nothing

            Try
                imagestreams = Array.ConvertAll(Imagefiles,
                                                Function(Imagefile)
                                                    Trace.WriteLine("Opening image " & Imagefile)
                                                    Return New FileStream(Imagefile, FileMode.Open, DiskAccess, ShareMode)
                                                End Function)

            Catch When (
                Function()
                    If imagestreams IsNot Nothing Then
                        Array.ForEach(imagestreams, Sub(imagestream) imagestream.Close())
                    End If
                    Return False
                End Function)()

                Throw

            End Try

            Return imagestreams

        End Function

        Public Sub New(FirstImagefile As String, DiskAccess As FileAccess)
            Me.New(GetMultiSegmentFiles(FirstImagefile), DiskAccess)
        End Sub

        Public Sub New(FirstImagefile As String, DiskAccess As FileAccess, ShareMode As FileShare)
            Me.New(GetMultiSegmentFiles(FirstImagefile), DiskAccess, ShareMode)
        End Sub

    End Class

End Namespace
