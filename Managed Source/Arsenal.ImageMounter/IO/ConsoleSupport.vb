Imports System.Runtime.CompilerServices
Imports System.Text

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

            Dim origLines = message.Replace(vbCr, "").Split({vbLf(0)})

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
#If NETSTANDARD OrElse NETCOREAPP Then
                    result.Append(line)
#Else
                    result.Append(line.ToString())
#End If
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

        Public Function ParseCommandLine(args As IEnumerable(Of String), comparer As StringComparer) As Dictionary(Of String, String())

            Dim dict = ParseCommandLineParameter(args).
                GroupBy(Function(item) item.Key, Function(item) item.Value, comparer).
                ToDictionary(Function(item) item.Key, Function(item) item.SelectMany(Function(i) i).ToArray(), comparer)

            Return dict

        End Function

        Public Iterator Function ParseCommandLineParameter(args As IEnumerable(Of String)) As IEnumerable(Of KeyValuePair(Of String, IEnumerable(Of String)))

            Dim switches_finished = False

            For Each arg In args

                If switches_finished Then

                ElseIf arg.Length = 0 OrElse arg.Equals("-", StringComparison.Ordinal) Then

                    switches_finished = True

                ElseIf arg.Equals("--", StringComparison.Ordinal) Then

                    switches_finished = True
                    Continue For

                ElseIf arg.StartsWith("--", StringComparison.Ordinal) OrElse arg.StartsWith("/", StringComparison.Ordinal) Then

                    Dim namestart = 1
                    If arg(0) = "-"c Then
                        namestart = 2
                    End If

                    Dim valuepos = arg.IndexOf("="c)
                    If valuepos < 0 Then
                        valuepos = arg.IndexOf(":"c)
                    End If

                    Dim name As String
                    Dim value As IEnumerable(Of String)

                    If valuepos >= 0 Then
                        name = arg.Substring(namestart, valuepos - namestart)
                        value = {arg.Substring(valuepos + 1)}
                    Else
                        name = arg.Substring(namestart)
                        value = Enumerable.Empty(Of String)()
                    End If

                    Yield New KeyValuePair(Of String, IEnumerable(Of String))(name, value)

                ElseIf arg.StartsWith("-", StringComparison.Ordinal) Then

                    For i = 1 To arg.Length - 1

                        Dim name = arg.Substring(i, 1)

                        If i + 1 < arg.Length AndAlso
                                (arg(i + 1) = "="c OrElse arg(i + 1) = ":"c) Then

                            Dim value = {arg.Substring(i + 2)}

                            Yield New KeyValuePair(Of String, IEnumerable(Of String))(name, value)
                            Exit For

                        End If

                        Yield New KeyValuePair(Of String, IEnumerable(Of String))(name, Nothing)

                    Next

                Else

                    switches_finished = True

                End If

                If switches_finished Then

                    Yield New KeyValuePair(Of String, IEnumerable(Of String))(String.Empty, {arg})

                End If

            Next

        End Function

    End Module

End Namespace
