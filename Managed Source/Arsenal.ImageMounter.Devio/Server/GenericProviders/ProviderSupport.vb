
''''' ProviderSupport.vb
''''' 
''''' Copyright (c) 2012-2019, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Namespace Server.GenericProviders

    Public MustInherit Class ProviderSupport

        Private Sub New()
        End Sub

        Public Shared Function GetMultiSegmentFiles(FirstFile As String) As String()

            Dim pathpart = Path.GetDirectoryName(FirstFile)
            Dim filepart = Path.GetFileNameWithoutExtension(FirstFile)
            Dim extension = Path.GetExtension(FirstFile)
            Dim foundfiles As String() = Nothing

            If extension.EndsWith("01") OrElse extension.EndsWith("00") Then

                Dim start = extension.Length - 3

                While start >= 0 AndAlso Char.IsDigit(extension, start)
                    start -= 1
                End While

                start += 1

                Dim segmentnumberchars = New String("?"c, extension.Length - start)
                Dim namebase = filepart & extension.Remove(start)
                Dim pathbase = Path.Combine(Path.GetDirectoryName(FirstFile), namebase)

                foundfiles =
                    Directory.GetFiles(Path.GetDirectoryName(FirstFile), namebase & segmentnumberchars)

                Array.Sort(foundfiles, StringComparer.Ordinal)

            Else

                If File.Exists(FirstFile) Then
                    foundfiles = {FirstFile}
                End If

            End If

            If foundfiles Is Nothing OrElse foundfiles.Length = 0 Then
                Throw New FileNotFoundException("Image file not found", FirstFile)
            End If

            Return foundfiles

        End Function



    End Class

End Namespace
