using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Arsenal.ImageMounter.Collections;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Data;

/// <summary>
/// Class that caches a text INI file
/// </summary>
[ComVisible(false)]
public class CachedIniFile : NullSafeDictionary<string, NullSafeDictionary<string, string>>
{

    protected override NullSafeDictionary<string, string> GetDefaultValue(string Key)
    {
        var new_section = new NullSafeStringDictionary(StringComparer.CurrentCultureIgnoreCase);
        Add(Key, new_section);
        return new_section;
    }

    /// <summary>
    /// Flushes registry mapping for all INI files.
    /// is thrown.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static void Flush()
        => NativeFileIO.UnsafeNativeMethods.WritePrivateProfileStringW(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static IEnumerable<string> EnumerateFileSectionNames(ReadOnlyMemory<char> filename)
    {

        const int NamesSize = 32767;

        var sectionnames = ArrayPool<char>.Shared.Rent(NamesSize);
        try
        {
            var size = NativeFileIO.UnsafeNativeMethods.GetPrivateProfileSectionNamesW(out sectionnames[0],
                                                                                          NamesSize,
                                                                                          filename.MakeNullTerminated());

            foreach (var name in sectionnames.AsMemory(0, size).ParseDoubleTerminatedString())
            {
                yield return name.ToString();
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(sectionnames);
        }
    }

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static IEnumerable<KeyValuePair<string, string>> EnumerateFileSectionValuePairs(ReadOnlyMemory<char> filename, ReadOnlyMemory<char> section)
    {

        const int ValuesSize = 32767;

        var valuepairs = ArrayPool<char>.Shared.Rent(ValuesSize);
        try
        {
            var size = NativeFileIO.UnsafeNativeMethods.GetPrivateProfileSectionW(section.MakeNullTerminated(),
                                                                                     valuepairs[0],
                                                                                     ValuesSize,
                                                                                     filename.MakeNullTerminated());

            foreach (var valuepair in valuepairs.AsMemory(0, size).ParseDoubleTerminatedString())
            {

                var pos = valuepair.Span.IndexOf('=');

                if (pos < 0)
                {
                    continue;
                }

                var key = valuepair.Slice(0, pos).ToString();
                var value = valuepair.Slice(pos + 1).ToString();

                yield return new KeyValuePair<string, string>(key, value);

            }
        }

        finally
        {
            ArrayPool<char>.Shared.Return(valuepairs);

        }
    }

    /// <summary>
    /// Saves a value to an INI file by calling Win32 API function WritePrivateProfileString. If call fails and exception
    /// is thrown.
    /// </summary>
    /// <param name="FileName">Name and path of INI file where to save value</param>
    /// <param name="SectionName">Name of INI file section where to save value</param>
    /// <param name="SettingName">Name of value to save</param>
    /// <param name="Value">Value to save</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static void SaveValue(ReadOnlyMemory<char> FileName, ReadOnlyMemory<char> SectionName, ReadOnlyMemory<char> SettingName, ReadOnlyMemory<char> Value)
        => NativeFileIO.Win32Try(NativeFileIO.UnsafeNativeMethods.WritePrivateProfileStringW(SectionName.MakeNullTerminated(),
                                                                                             SettingName.MakeNullTerminated(),
                                                                                             Value.MakeNullTerminated(),
                                                                                             FileName.MakeNullTerminated()));

    /// <summary>
    /// Saves a value to an INI file by calling Win32 API function WritePrivateProfileString. If call fails and exception
    /// is thrown.
    /// </summary>
    /// <param name="FileName">Name and path of INI file where to save value</param>
    /// <param name="SectionName">Name of INI file section where to save value</param>
    /// <param name="SettingName">Name of value to save</param>
    /// <param name="Value">Value to save</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static void SaveValue(string? FileName, string? SectionName, string? SettingName, string? Value)
        => NativeFileIO.Win32Try(NativeFileIO.UnsafeNativeMethods.WritePrivateProfileStringW(MemoryMarshal.GetReference(SectionName.AsSpan()),
                                                                                             MemoryMarshal.GetReference(SettingName.AsSpan()),
                                                                                             MemoryMarshal.GetReference(Value.AsSpan()),
                                                                                             MemoryMarshal.GetReference(FileName.AsSpan())));

    /// <summary>
    /// Saves a current value from this object to an INI file by calling Win32 API function WritePrivateProfileString.
    /// If call fails and exception is thrown.
    /// </summary>
    /// <param name="FileName">Name and path of INI file where to save value</param>
    /// <param name="SectionName">Name of INI file section where to save value</param>
    /// <param name="SettingName">Name of value to save</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void SaveValue(ReadOnlyMemory<char> FileName, string SectionName, string SettingName)
        => SaveValue(SectionName.AsMemory(),
                     SettingName.AsMemory(),
                     base[SectionName]?[SettingName].AsMemory() ?? default,
                     FileName);

    /// <summary>
    /// Saves a current value from this object to INI file that this object last loaded values from, either through constructor
    /// call with filename parameter or by calling Load method with filename parameter.
    /// Operation is carried out by calling Win32 API function WritePrivateProfileString.
    /// If call fails and exception is thrown.
    /// </summary>
    /// <param name="SectionName">Name of INI file section where to save value</param>
    /// <param name="SettingName">Name of value to save</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void SaveValue(string SectionName, string SettingName)
    {
        if (Filename is null || string.IsNullOrEmpty(Filename))
        {
            throw new InvalidOperationException("Filename property not set on this object.");
        }
        SaveValue(SectionName, SettingName, this[SectionName]?[SettingName], Filename);
    }

    /// <summary>
    /// Saves current contents of this object to INI file that this object last loaded values from, either through constructor
    /// call with filename parameter or by calling Load method with filename parameter.
    /// </summary>
    public void Save() => File.WriteAllText(Filename ?? throw new InvalidOperationException("Filename property not set on this object."),
                                            ToString(),
                                            Encoding);

    /// <summary>
    /// Saves current contents of this object to an INI file. If the file already exists, it is overwritten.
    /// </summary>
    public void Save(string Filename, Encoding Encoding) => File.WriteAllText(Filename, ToString(), Encoding);

    /// <summary>
    /// Saves current contents of this object to an INI file. If the file already exists, it is overwritten.
    /// </summary>
    public void Save(string Filename) => File.WriteAllText(Filename, ToString(), Encoding);

    public override string ToString()
    {
        using var Writer = new StringWriter();
        WriteTo(Writer);
        return Writer.ToString();
    }

    public void WriteTo(Stream Stream)
        => WriteTo(new StreamWriter(Stream, Encoding)
        {
            AutoFlush = true
        });

    public void WriteTo(TextWriter Writer)
    {
        WriteSectionTo(string.Empty, Writer);
        foreach (var SectionKey in Keys)
        {
            if (string.IsNullOrEmpty(SectionKey))
            {
                continue;
            }

            WriteSectionTo(SectionKey, Writer);
        }
        Writer.Flush();
    }

    public void WriteSectionTo(string SectionKey, TextWriter Writer)
    {
        if (!ContainsKey(SectionKey))
        {
            return;
        }

        var Section = this[SectionKey];

        var any_written = false;

        if (SectionKey is not null && !string.IsNullOrEmpty(SectionKey))
        {
            Writer.WriteLine($"[{SectionKey}]");
            any_written = true;
        }

        if (Section is not null)
        {
            foreach (var key in Section.Keys.OfType<string>())
            {
                Writer.WriteLine($"{key}={Section[key]}");
                any_written = true;
            }
        }

        if (any_written)
        {
            Writer.WriteLine();
        }
    }

    /// <summary>
    /// Name of last INI file loaded into this object.
    /// </summary>
    public string? Filename { get; private set; }

    /// <summary>
    /// Text encoding of last INI file loaded into this object.
    /// </summary>
    public Encoding Encoding { get; private set; }

    /// <summary>
    /// Creates a new empty CachedIniFile object
    /// </summary>
    public CachedIniFile()
        : base(StringComparer.CurrentCultureIgnoreCase)
    {
        Encoding = Encoding.Default;
    }

    /// <summary>
    /// Creates a new CachedIniFile object and fills it with the contents of the specified
    /// INI file
    /// </summary>
    /// <param name="Filename">Name of INI file to read into the created object</param>
    /// <param name="Encoding">Text encoding used in INI file</param>
    public CachedIniFile(string Filename, Encoding Encoding)
        : this()
    {

        Load(Filename, Encoding);
    }

    /// <summary>
    /// Creates a new CachedIniFile object and fills it with the contents of the specified
    /// INI file
    /// </summary>
    /// <param name="Filename">Name of INI file to read into the created object</param>
    public CachedIniFile(string Filename)
        : this(Filename, Encoding.Default)
    {
    }

    /// <summary>
    /// Creates a new CachedIniFile object and fills it with the contents of the specified
    /// INI file
    /// </summary>
    /// <param name="Stream">Stream that contains INI settings to read into the created object</param>
    /// <param name="Encoding">Text encoding used in INI file</param>
    public CachedIniFile(Stream Stream, Encoding Encoding)
        : this()
    {

        Load(Stream, Encoding);
    }

    /// <summary>
    /// Creates a new CachedIniFile object and fills it with the contents of the specified
    /// INI file
    /// </summary>
    /// <param name="Stream">Stream that contains INI settings to read into the created object</param>
    public CachedIniFile(Stream Stream)
        : this(Stream, Encoding.Default)
    {
    }

    /// <summary>
    /// Reloads settings from disk file. This is only supported if this object was created using
    /// a constructor that takes a filename or if a Load() method that takes a filename has been
    /// called earlier.
    /// </summary>
    public void Reload()
        => Load(Filename ?? throw new InvalidOperationException("Filename property not set on this object."),
                Encoding);

    /// <summary>
    /// Loads settings from an INI file into this CachedIniFile object. Existing settings
    /// in object is replaced.
    /// </summary>
    /// <param name="Filename">INI file to load</param>
    public void Load(string Filename) => Load(Filename, Encoding.Default);

    /// <summary>
    /// Loads settings from an INI file into this CachedIniFile object. Existing settings
    /// in object is replaced.
    /// </summary>
    /// <param name="Filename">INI file to load</param>
    /// <param name="Encoding">Text encoding for INI file</param>
    public void Load(string Filename, Encoding Encoding)
    {
        this.Filename = Filename;
        this.Encoding = Encoding;

        try
        {
            using var fs = new FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 20480, FileOptions.SequentialScan);
            Load(fs, Encoding);
        }

        catch
        {

        }
    }

    /// <summary>
    /// Loads settings from an INI file into this CachedIniFile object. Existing settings
    /// in object is replaced.
    /// </summary>
    /// <param name="Stream">Stream containing INI file data</param>
    /// <param name="Encoding">Text encoding for INI stream</param>
    public void Load(Stream Stream, Encoding? Encoding)
    {
        try
        {
            Encoding ??= Encoding.Default;

            var sr = new StreamReader(Stream, Encoding, false, 1048576);

            Load(sr);

            this.Encoding = Encoding;
        }

        catch
        {

        }
    }

    /// <summary>
    /// Loads settings from an INI file into this CachedIniFile object using Default text
    /// encoding. Existing settings in object is replaced.
    /// </summary>
    /// <param name="Stream">Stream containing INI file data</param>
    public void Load(Stream Stream) => Load(Stream, Encoding.Default);

    /// <summary>
    /// Loads settings from an INI file into this CachedIniFile object. Existing settings
    /// in object is replaced.
    /// </summary>
    /// <param name="Stream">Stream containing INI file data</param>
    public void Load(TextReader Stream)
    {
        lock (SyncRoot)
        {
            try
            {
                var CurrentSection = this[string.Empty];

                for(; ; )
                {
                    var Linestr = Stream.ReadLine();

                    if (Linestr is null)
                    {
                        break;
                    }

                    var Line = Linestr.AsSpan().Trim();

                    if (Line.Length == 0 || Line.StartsWith(";".AsSpan(), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (Line.StartsWith("[".AsSpan(), StringComparison.Ordinal) && Line.EndsWith("]".AsSpan(), StringComparison.Ordinal))
                    {
                        var SectionKey = Line.Slice(1, Line.Length - 2).Trim().ToString();
                        CurrentSection = this[SectionKey];
                        continue;
                    }

                    var EqualSignPos = Line.IndexOf('=');
                    if (EqualSignPos < 0)
                    {
                        continue;
                    }

                    var Key = Line.Slice(0, EqualSignPos).Trim().ToString();
                    var Value = Line.Slice(EqualSignPos + 1).Trim().ToString();

                    if (CurrentSection is null)
                    {
                        continue;
                    }

                    CurrentSection[Key] = Value;
                }
            }
            catch
            {
            }
        }
    }
}