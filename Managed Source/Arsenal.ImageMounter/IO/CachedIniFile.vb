Imports System.Text
Imports System.Collections.Specialized
Imports Arsenal.ImageMounter.IO

Namespace IO

    ''' <summary>
    ''' Class that caches a text INI file
    ''' </summary>
    <ComVisible(False)> _
    Public Class CachedIniFile
        Inherits NullSafeDictionary(Of String, NameValueCollection)

        Private m_Filename As String
        Private m_Encoding As Encoding

        Protected Overrides Function GetDefaultValue() As NameValueCollection
            Return New NameValueCollection(StringComparer.CurrentCultureIgnoreCase)
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
            NativeFileIO.Win32Try(NativeFileIO.Win32API.WritePrivateProfileString(SectionName, SettingName, Value, FileName))
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
            If String.IsNullOrEmpty(m_Filename) Then
                Throw New ArgumentNullException("Filename", "Filename property not set on this object.")
            End If
            SaveValue(SectionName, SettingName, Item(SectionName)(SettingName), m_Filename)
        End Sub

        Public Overrides Function ToString() As String
            Dim Writer As New StringWriter
            WriteTo(Writer)
            Return Writer.ToString()
        End Function

        Public Sub WriteTo(Stream As Stream)
            WriteTo(New StreamWriter(Stream, m_Encoding))
        End Sub

        Public Sub WriteTo(Writer As TextWriter)
            WriteSectionTo(String.Empty, Writer)
            For Each SectionKey In Keys
                If String.IsNullOrEmpty(SectionKey) Then
                    Continue For
                End If

                WriteSectionTo(SectionKey, Writer)
            Next
        End Sub

        Public Sub WriteSectionTo(SectionKey As String, Writer As TextWriter)
            If Not ContainsKey(SectionKey) Then
                Return
            End If

            Dim Section = Item(SectionKey)
            If Not Section.HasKeys Then
                Return
            End If

            If Not String.IsNullOrEmpty(SectionKey) Then
                Writer.WriteLine("[" & SectionKey & "]")
            End If

            For Each key In Section.AllKeys
                Writer.WriteLine(key & "=" & Section(key))
            Next

            Writer.WriteLine()
        End Sub

        ''' <summary>
        ''' Name of last ini file loaded into this object.
        ''' </summary>
        Public ReadOnly Property Filename() As String
            Get
                Return m_Filename
            End Get
        End Property

        ''' <summary>
        ''' Text encoding of last ini file loaded into this object.
        ''' </summary>
        Public ReadOnly Property Encoding() As Encoding
            Get
                Return m_Encoding
            End Get
        End Property

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
            MyBase.New(StringComparer.CurrentCultureIgnoreCase)

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
            MyBase.New(StringComparer.CurrentCultureIgnoreCase)

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
            Load(m_Filename, m_Encoding)
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
            m_Filename = Filename
            m_Encoding = Encoding

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
                Using sr As New StreamReader(Stream, Encoding, False, 1048576)
                    Load(sr)
                End Using

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

                            If Line.Length = 0 OrElse Line.StartsWith(";") Then
                                Continue Do
                            End If

                            If Line.StartsWith("[") AndAlso Line.EndsWith("]") Then
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

                            CurrentSection.Set(Key, Value)

                        Loop
                    End With

                Catch

                End Try
            End SyncLock
        End Sub

    End Class
End Namespace
