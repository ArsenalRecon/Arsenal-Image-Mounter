Imports System.Windows.Forms
Imports Arsenal.ImageMounter.IO

Public MustInherit Class LibewfVerify

    Private Sub New()

    End Sub

    Public Shared Function VerifyLibewf(owner As IWin32Window) As Boolean

        '' Just a test to trig cctor call in DevioProviderLibEwf and thereby loading libewf.dll
        '' This will throw an exception if libewf.dll is not found or for wrong architecture
        Try
            Dim test = Devio.Server.SpecializedProviders.DevioProviderLibEwf.AccessFlagsRead

        Catch ex As TypeInitializationException When TypeOf ex.GetBaseException() Is BadImageFormatException
            MessageBox.Show(owner,
                            "Incompatible architecture versions of libewf.dll, zlib.dll or msvcr100.dll detected. Please copy " & NativeFileIO.ProcessArchitecture & " versions of these files to same directory as this exe file.",
                            ex.GetBaseException().GetType().Name,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)

            Return False

        Catch ex As Exception
            MessageBox.Show(owner,
                            "Cannot find or load libewf.dll, zlib.dll or msvcr100.dll. Please copy " & NativeFileIO.ProcessArchitecture & " versions of these files to same directory as this exe file.",
                            ex.GetBaseException().GetType().Name,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)

            Return False

        End Try

        Return True

    End Function

End Class
