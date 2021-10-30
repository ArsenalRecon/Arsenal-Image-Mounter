Imports System.Diagnostics.CodeAnalysis
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text

Namespace IO

    ''' <summary>
    ''' Class that caches a text INI file
    ''' </summary>
    <ComVisible(False)>
    Public Class CachedIniFile
        Inherits NullSafeDictionary(Of String, NullSafeDictionary(Of String, String))

        Protected Overrides Function GetDefaultValue(Key As String) As NullSafeDictionary(Of String, String)
            Dim new_section As New NullSafeStringDictionary(StringComparer.CurrentCultureIgnoreCase)
            Add(Key, new_section)
            Return new_section
        End Function

        ''' <summary>
        ''' Flushes registry mapping for all INI files.
        ''' is thrown.
        ''' </summary>
        Public Shared Sub Flush()
            NativeFileIO.UnsafeNativeMethods.WritePrivateProfileString(Nothing, Nothing, Nothing, Nothing)
        End Sub

        Public Shared Function EnumerateFileSectionNames(filename As String) As IEnumerable(Of String)

            Dim sectionnames(0 To 32766) As Char

            Dim size = NativeFileIO.UnsafeNativeMethods.GetPrivateProfileSectionNames(sectionnames, sectionnames.Length, filename)

            Return NativeFileIO.ParseDoubleTerminatedString(sectionnames, size)

        End Function

        Public Shared Iterator Function EnumerateFileSectionValuePairs(filename As String, section As String) As IEnumerable(Of KeyValuePair(Of String, String))

            Dim valuepairs(0 To 32766) As Char

            Dim size = NativeFileIO.UnsafeNativeMethods.GetPrivateProfileSection(section, valuepairs, valuepairs.Length, filename)

            For Each valuepair In NativeFileIO.ParseDoubleTerminatedString(valuepairs, size)

                Dim pos = valuepair.IndexOf("="c)

                If pos < 0 Then
                    Continue For
                End If

                Dim key = valuepair.Remove(pos)
                Dim value = valuepair.Substring(pos + 1)

                Yield New KeyValuePair(Of String, String)(key, value)

            Next

        End Function

        ''' <summary>
        ''' Saves a value to an INI file by calling Win32 API function WritePrivateProfileString. If call fails and exception
        ''' is thrown.
        ''' </summary>
        ''' <param name="FileName">Name and path of INI file where to save value</param>
        ''' <param name="SectionName">Name of INI file section where to save value</param>
        ''' <param name="SettingName">Name of value to save</param>
        ''' <param name="Value">Value to save</param>
        Public Shared Sub SaveValue(FileName As String, SectionName As String, SettingName As String, Value As String)
            NativeFileIO.Win32Try(NativeFileIO.UnsafeNativeMethods.WritePrivateProfileString(SectionName, SettingName, Value, FileName))
        End Sub

        ''' <summary>
        ''' Saves a current value from this object to an INI file by calling Win32 API function WritePrivateProfileString.
        ''' If call fails and exception is thrown.
        ''' </summary>
        ''' <param name="FileName">Name and path of INI file where to save value</param>
        ''' <param name="SectionName">Name of INI file section where to save value</param>
        ''' <param name="SettingName">Name of value to save</param>
        Public Sub SaveValue(FileName As String, SectionName As String, SettingName As String)
            SaveValue(SectionName, SettingName, Item(SectionName)(SettingName), FileName)
        End Sub

        ''' <summary>
        ''' Saves a current value from this object to INI file that this object last loaded values from, either through constructor
        ''' call with filename parameter or by calling Load method with filename parameter.
        ''' Operation is carried out by calling Win32 API function WritePrivateProfileString.
        ''' If call fails and exception is thrown.
        ''' </summary>
        ''' <param name="SectionName">Name of INI file section where to save value</param>
        ''' <param name="SettingName">Name of value to save</param>
        Public Sub SaveValue(SectionName As String, SettingName As String)
            If String.IsNullOrEmpty(_Filename) Then
                Throw New InvalidOperationException("Filename property not set on this object.")
            End If
            SaveValue(SectionName, SettingName, Item(SectionName)(SettingName), _Filename)
        End Sub

        ''' <summary>
        ''' Saves current contents of this object to INI file that this object last loaded values from, either through constructor
        ''' call with filename parameter or by calling Load method with filename parameter.
        ''' </summary>
        Public Sub Save()
            File.WriteAllText(_Filename, ToString(), _Encoding)
        End Sub

        ''' <summary>
        ''' Saves current contents of this object to an INI file. If the file already exists, it is overwritten.
        ''' </summary>
        Public Sub Save(Filename As String, Encoding As Encoding)
            File.WriteAllText(Filename, ToString(), Encoding)
        End Sub

        ''' <summary>
        ''' Saves current contents of this object to an INI file. If the file already exists, it is overwritten.
        ''' </summary>
        Public Sub Save(Filename As String)
            File.WriteAllText(Filename, ToString(), _Encoding)
        End Sub

        Public Overrides Function ToString() As String
            Using Writer As New StringWriter
                WriteTo(Writer)
                Return Writer.ToString()
            End Using
        End Function

        Public Sub WriteTo(Stream As Stream)
            WriteTo(New StreamWriter(Stream, _Encoding) With {.AutoFlush = True})
        End Sub

        Public Sub WriteTo(Writer As TextWriter)
            WriteSectionTo(String.Empty, Writer)
            For Each SectionKey In Keys
                If String.IsNullOrEmpty(SectionKey) Then
                    Continue For
                End If

                WriteSectionTo(SectionKey, Writer)
            Next
            Writer.Flush()
        End Sub

        Public Sub WriteSectionTo(SectionKey As String, Writer As TextWriter)
            If Not ContainsKey(SectionKey) Then
                Return
            End If

            Dim Section = Item(SectionKey)

            Dim any_written = False

            If Not String.IsNullOrEmpty(SectionKey) Then
                Writer.WriteLine($"[{SectionKey}]")
                any_written = True
            End If

            For Each key In Section.Keys.OfType(Of String)()
                Writer.WriteLine($"{key}={Section(key)}")
                any_written = True
            Next

            If any_written Then
                Writer.WriteLine()
            End If
        End Sub

        ''' <summary>
        ''' Name of last INI file loaded into this object.
        ''' </summary>
        Public ReadOnly Property Filename() As String

        ''' <summary>
        ''' Text encoding of last INI file loaded into this object.
        ''' </summary>
        Public ReadOnly Property Encoding() As Encoding

        ''' <summary>
        ''' Creates a new empty CachedIniFile object
        ''' </summary>
        Public Sub New()
            MyBase.New(StringComparer.CurrentCultureIgnoreCase)
        End Sub

        ''' <summary>
        ''' Creates a new CachedIniFile object and fills it with the contents of the specified
        ''' INI file
        ''' </summary>
        ''' <param name="Filename">Name of INI file to read into the created object</param>
        ''' <param name="Encoding">Text encoding used in INI file</param>
        Public Sub New(Filename As String, Encoding As Encoding)
            Me.New()

            Load(Filename, Encoding)
        End Sub

        ''' <summary>
        ''' Creates a new CachedIniFile object and fills it with the contents of the specified
        ''' INI file
        ''' </summary>
        ''' <param name="Filename">Name of INI file to read into the created object</param>
        Public Sub New(Filename As String)
            Me.New(Filename, Encoding.Default)
        End Sub

        ''' <summary>
        ''' Creates a new CachedIniFile object and fills it with the contents of the specified
        ''' INI file
        ''' </summary>
        ''' <param name="Stream">Stream that contains INI settings to read into the created object</param>
        ''' <param name="Encoding">Text encoding used in INI file</param>
        Public Sub New(Stream As Stream, Encoding As Encoding)
            Me.New()

            Load(Stream, Encoding)
        End Sub

        ''' <summary>
        ''' Creates a new CachedIniFile object and fills it with the contents of the specified
        ''' INI file
        ''' </summary>
        ''' <param name="Stream">Stream that contains INI settings to read into the created object</param>
        Public Sub New(Stream As Stream)
            Me.New(Stream, Encoding.Default)
        End Sub

        ''' <summary>
        ''' Reloads settings from disk file. This is only supported if this object was created using
        ''' a constructor that takes a filename or if a Load() method that takes a filename has been
        ''' called earlier.
        ''' </summary>
        Public Sub Reload()
            Load(_Filename, _Encoding)
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object. Existing settings
        ''' in object is replaced.
        ''' </summary>
        ''' <param name="Filename">INI file to load</param>
        Public Sub Load(Filename As String)
            Load(Filename, Encoding.Default)
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object. Existing settings
        ''' in object is replaced.
        ''' </summary>
        ''' <param name="Filename">INI file to load</param>
        ''' <param name="Encoding">Text encoding for INI file</param>
        Public Sub Load(Filename As String, Encoding As Encoding)
            _Filename = Filename
            _Encoding = Encoding

            Try
                Using fs As New FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 20480, FileOptions.SequentialScan)
                    Load(fs, Encoding)
                End Using

            Catch

            End Try
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object. Existing settings
        ''' in object is replaced.
        ''' </summary>
        ''' <param name="Stream">Stream containing INI file data</param>
        ''' <param name="Encoding">Text encoding for INI stream</param>
        Public Sub Load(Stream As Stream, Encoding As Encoding)
            Try
                Dim sr As New StreamReader(Stream, Encoding, False, 1048576)

                Load(sr)

                _Encoding = Encoding

            Catch

            End Try
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object using Default text
        ''' encoding. Existing settings in object is replaced.
        ''' </summary>
        ''' <param name="Stream">Stream containing INI file data</param>
        Public Sub Load(Stream As Stream)
            Load(Stream, Encoding.Default)
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object. Existing settings
        ''' in object is replaced.
        ''' </summary>
        ''' <param name="Stream">Stream containing INI file data</param>
        Public Sub Load(Stream As TextReader)
            SyncLock SyncRoot
                Try
                    With Stream
                        Dim CurrentSection = Item(String.Empty)

                        Do
                            Dim Line = .ReadLine()

                            If Line Is Nothing Then
                                Exit Do
                            End If

                            Line = Line.Trim()

                            If Line.Length = 0 OrElse Line.StartsWith(";", StringComparison.Ordinal) Then
                                Continue Do
                            End If

                            If Line.StartsWith("[", StringComparison.Ordinal) AndAlso Line.EndsWith("]", StringComparison.Ordinal) Then
                                Dim SectionKey = Line.Substring(1, Line.Length - 2).Trim()
                                CurrentSection = Item(SectionKey)
                                Continue Do
                            End If

                            Dim EqualSignPos = Line.IndexOf("="c)
                            If EqualSignPos < 0 Then
                                Continue Do
                            End If

                            Dim Key = Line.Remove(EqualSignPos).Trim()
                            Dim Value = Line.Substring(EqualSignPos + 1).Trim()

                            CurrentSection(Key) = Value

                        Loop
                    End With

                Catch

                End Try
            End SyncLock
        End Sub

    End Class
End Namespace
