Imports System.Runtime.CompilerServices

Namespace IO

    Public Module ConsoleSupport

        Friend ReadOnly ConsoleSync As New Object

        <Extension>
        Public Function LineFormat(message As String, Optional IndentWidth As Integer = 0, Optional LineWidth As Integer? = Nothing, Optional WordDelimiter As Char = " "c, Optional FillChar As Char = " "c) As String

            Dim Width As Integer

            If LineWidth.HasValue Then
                Width = LineWidth.Value
            Else
                If NativeFileIO.UnsafeNativeMethods.GetFileType(NativeFileIO.UnsafeNativeMethods.GetStdHandle(NativeFileIO.StdHandle.Output)) <> NativeFileIO.Win32FileType.Character Then
                    Width = 79
                Else
                    Width = Console.WindowWidth - 1
                End If
            End If

            Dim origLines = message.Replace(Microsoft.VisualBasic.vbCr, "").Split({Microsoft.VisualBasic.vbLf(0)})

            Dim resultLines As New List(Of String)(origLines.Length)

            Dim result As New StringBuilder

            Dim line As New StringBuilder(Width)

            For Each origLine In origLines

                result.Length = 0
                line.Length = 0

                For Each Word In origLine.Split(WordDelimiter)
                    If Word.Length >= Width Then
                        result.AppendLine(Word)
                        Continue For
                    End If
                    If Word.Length + line.Length >= Width Then
                        result.AppendLine(line.ToString())
                        line.Length = 0
                        line.Append(FillChar, IndentWidth)
                    ElseIf line.Length > 0 Then
                        line.Append(WordDelimiter)
                    End If
                    line.Append(Word)
                Next

                If line.Length > 0 Then
                    result.Append(line.ToString())
                End If

                resultLines.Add(result.ToString())

            Next

            Return String.Join(Environment.NewLine, resultLines)

        End Function

        Public Property WriteMsgTraceLevel As TraceLevel = TraceLevel.Info

        Public Sub WriteMsg(level As TraceLevel, msg As String)

            Dim color As ConsoleColor

            Select Case level

                Case <= TraceLevel.Off
                    color = ConsoleColor.Cyan

                Case TraceLevel.Error
                    color = ConsoleColor.Red

                Case TraceLevel.Warning
                    color = ConsoleColor.Yellow

                Case TraceLevel.Info
                    color = ConsoleColor.Gray

                Case >= TraceLevel.Verbose
                    color = ConsoleColor.DarkGray

            End Select

            If level <= _WriteMsgTraceLevel Then

                SyncLock ConsoleSync

                    Console.ForegroundColor = color

                    Console.WriteLine(msg.LineFormat())

                    Console.ResetColor()

                End SyncLock

            End If

        End Sub

    End Module

End Namespace
