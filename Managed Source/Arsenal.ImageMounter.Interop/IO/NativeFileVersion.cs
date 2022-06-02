using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WORD = System.UInt16;
using DWORD = System.UInt32;
using LONG = System.Int32;
using BYTE = System.Byte;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable 0649
#pragma warning disable 1591
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.IO;

using Arsenal.ImageMounter.Extensions;
using Internal;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

public enum IMAGE_FILE_MACHINE : WORD
{
    UNKNOWN = 0,
    I386 = 0x014c,
    R3000 = 0x0162,
    R4000 = 0x0166,
    R10000 = 0x0168,
    WCEMIPSV2 = 0x0169,
    ALPHA = 0x0184,
    SH3 = 0x01a2,
    SH3DSP = 0x01a3,
    SH3E = 0x01a4,
    SH4 = 0x01a6,
    SH5 = 0x01a8,
    ARM = 0x01c0,
    THUMB = 0x01c2,
    ARM2 = 0x01c4,
    AM33 = 0x01d3,
    POWERPC = 0x01F0,
    POWERPCFP = 0x01f1,
    IA64 = 0x0200,
    MIPS16 = 0x0266,
    ALPHA64 = 0x0284,
    MIPSFPU = 0x0366,
    MIPSFPU16 = 0x0466,
    TRICORE = 0x0520,
    CEF = 0x0CEF,
    EBC = 0x0EBC,
    AMD64 = 0x8664,
    M32R = 0x9041,
    CEE = 0xC0EE
}

/// <summary>
/// PE image header
/// </summary>
public readonly struct IMAGE_FILE_HEADER
{
    public readonly IMAGE_FILE_MACHINE Machine;
    public readonly WORD NumberOfSections;
    public readonly DWORD TimeDateStamp;
    public readonly DWORD PointerToSymbolTable;
    public readonly DWORD NumberOfSymbols;
    public readonly WORD SizeOfOptionalHeader;
    public readonly WORD Characteristics;
}

internal struct IMAGE_DATA_DIRECTORY
{
    public readonly DWORD VirtualAddress;
    public readonly DWORD Size;
}

/// <summary>
/// PE optional header
/// </summary>
public readonly struct IMAGE_OPTIONAL_HEADER
{
    //
    // Standard fields.
    //

    public readonly WORD Magic;
    public readonly BYTE MajorLinkerVersion;
    public readonly BYTE MinorLinkerVersion;
    public readonly DWORD SizeOfCode;
    public readonly DWORD SizeOfInitializedData;
    public readonly DWORD SizeOfUninitializedData;
    public readonly DWORD AddressOfEntryPoint;
    public readonly DWORD BaseOfCode;

    // Different fields follow depending on architecture
}

internal unsafe struct IMAGE_DOS_HEADER
{      // DOS .EXE header
    public readonly WORD e_magic;                     // Magic number
    public readonly WORD e_cblp;                      // Bytes on last page of file
    public readonly WORD e_cp;                        // Pages in file
    public readonly WORD e_crlc;                      // Relocations
    public readonly WORD e_cparhdr;                   // Size of header in paragraphs
    public readonly WORD e_minalloc;                  // Minimum extra paragraphs needed
    public readonly WORD e_maxalloc;                  // Maximum extra paragraphs needed
    public readonly WORD e_ss;                        // Initial (relative) SS value
    public readonly WORD e_sp;                        // Initial SP value
    public readonly WORD e_csum;                      // Checksum
    public readonly WORD e_ip;                        // Initial IP value
    public readonly WORD e_cs;                        // Initial (relative) CS value
    public readonly WORD e_lfarlc;                    // File address of relocation table
    public readonly WORD e_ovno;                      // Overlay number
    public fixed WORD e_res[4];                       // Reserved words
    public readonly WORD e_oemid;                     // OEM identifier (for e_oeminfo)
    public readonly WORD e_oeminfo;                   // OEM information; e_oemid specific
    public fixed WORD e_res2[10];                     // Reserved words
    public readonly LONG e_lfanew;                    // File address of new exe header
}

/// <summary>
/// Base of PE headers
/// </summary>
public readonly struct IMAGE_NT_HEADERS
{
    public readonly int Signature;
    public readonly IMAGE_FILE_HEADER FileHeader;
    public readonly IMAGE_OPTIONAL_HEADER OptionalHeader;
}

/// <summary>
/// Version resource header fields
/// </summary>
public unsafe struct VS_VERSIONINFO
{
    public readonly ushort Length { get; }
    public readonly ushort ValueLength { get; }
    public readonly ushort Type { get; }

    private fixed char szKey[16];
    
    private readonly ushort padding1;
    
    public FixedFileVerInfo FixedFileInfo { get; }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    public ReadOnlySpan<char> Key => MemoryMarshal.CreateReadOnlySpan(ref szKey[0], 16);
#else
    public ReadOnlySpan<char> Key => new(Unsafe.AsPointer(ref szKey[0]), 16);
#endif
}

/// <summary>
/// Fixed numeric fields in file version resource
/// </summary>
public readonly struct FixedFileVerInfo
{
    public const uint FixedFileVerSignature = 0xFEEF04BD;

    public uint Signature { get; }            /* e.g. 0xfeef04bd */
    public int StructVersion { get; }         /* e.g. 0x00000042 = "0.42" */
    public int FileVersionMS { get; }        /* e.g. 0x00030075 = "3.75" */
    public uint FileVersionLS { get; }        /* e.g. 0x00000031 = "0.31" */
    public int ProductVersionMS { get; }     /* e.g. 0x00030010 = "3.10" */
    public uint ProductVersionLS { get; }     /* e.g. 0x00000031 = "0.31" */
    public int FileFlagsMask { get; }        /* = 0x3F for version "0.42" */
    public int FileFlags { get; }            /* e.g. VFF_DEBUG | VFF_PRERELEASE */
    public int FileOS { get; }               /* e.g. VOS_DOS_WINDOWS16 */
    public int FileType { get; }             /* e.g. VFT_DRIVER */
    public int FileSubtype { get; }          /* e.g. VFT2_DRV_KEYBOARD */
    public int FileDateMS { get; }           /* e.g. 0 */
    public uint FileDateLS { get; }           /* e.g. 0 */

    /// <summary>
    /// File version from fixed numeric fields
    /// </summary>
    public Version FileVersion => new(FileVersionMS.HIWORD(), FileVersionMS.LOWORD(), FileVersionLS.HIWORD(), FileVersionLS.LOWORD());

    /// <summary>
    /// Product version from fixed numeric fields
    /// </summary>
    public Version ProductVersion => new(ProductVersionMS.HIWORD(), ProductVersionMS.LOWORD(), ProductVersionLS.HIWORD(), ProductVersionLS.LOWORD());
}

[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
internal static class WindowsSpecific
{
    [DllImport("version", CharSet = CharSet.Unicode)]
    public static extern unsafe bool VerQueryValue(void* pBlock, string lpSubBlock, out char* lplpBuffer, out int puLen);

    [DllImport("version", CharSet = CharSet.Unicode)]
    public static extern unsafe bool VerQueryValue(void* pBlock, string lpSubBlock, out uint* lplpBuffer, out int puLen);

    [DllImport("version", CharSet = CharSet.Unicode)]
    public static extern DWORD VerLanguageName(DWORD wLang, out char szLang, DWORD cchLang);
}

public static class NativePE
{
    private static readonly long _rsrc_id = 0x000000637273722E; // ".rsrc\0\0\0"

    private const int ERROR_RESOURCE_DATA_NOT_FOUND = 1812;
    private const int ERROR_RESOURCE_TYPE_NOT_FOUND = 1813;
    private const int ERROR_NO_MORE_ITEMS = 259;

    private const ushort RT_VERSION = 16;

    internal static ushort LOWORD(this int value) => (ushort)(value & 0xffff);
    internal static ushort HIWORD(this int value) => (ushort)((value >> 16) & 0xffff);
    internal static ushort LOWORD(this uint value) => (ushort)(value & 0xffff);
    internal static ushort HIWORD(this uint value) => (ushort)((value >> 16) & 0xffff);
    internal static long LARGE_INTEGER(uint LowPart, int HighPart) => LowPart | ((long)HighPart << 32);

    /// <summary>
    /// Gets IMAGE_NT_HEADERS structure from raw PE image
    /// </summary>
    /// <param name="FileData">Raw exe or dll data</param>
    /// <returns>IMAGE_NT_HEADERS structure</returns>
    public static IMAGE_NT_HEADERS GetImageNtHeaders(ReadOnlySpan<byte> FileData)
    {
        var dos_header = MemoryMarshal.Read<IMAGE_DOS_HEADER>(FileData);
        var header = MemoryMarshal.Read<IMAGE_NT_HEADERS>(FileData.Slice(dos_header.e_lfanew));

        if (header.Signature != 0x4550 || header.FileHeader.SizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        return header;
    }

    /// <summary>
    /// Returns a copy of fixed file version fields in a PE image
    /// </summary>
    /// <param name="FileData">Pointer to raw or mapped exe or dll</param>
    /// <returns>Copy of data from located version resource</returns>
    public static FixedFileVerInfo GetFixedFileVerInfo(ReadOnlySpan<byte> FileData) =>
        GetRawFileVersionResource(FileData, out _).FixedFileInfo;

    /// <summary>
    /// Locates version resource in a PE image
    /// </summary>
    /// <param name="FileData">Pointer to raw or mapped exe or dll</param>
    /// <param name="ResourceSize">Returns size of found resource</param>
    /// <returns>Reference to located version resource</returns>
    public static unsafe ref readonly VS_VERSIONINFO GetRawFileVersionResource(ReadOnlySpan<byte> FileData, out int ResourceSize)
    {
        ResourceSize = 0;

        ref readonly var dos_header = ref FileData.AsRef<IMAGE_DOS_HEADER>();

        var header_ptr = FileData.Slice(dos_header.e_lfanew);

        ref readonly var header = ref header_ptr.AsRef<IMAGE_NT_HEADERS>();

        var sizeOfOptionalHeader = header.FileHeader.SizeOfOptionalHeader;

        if (header.Signature != 0x4550 || sizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        var optional_header_ptr = FileData.Slice(dos_header.e_lfanew + sizeof(IMAGE_NT_HEADERS) - sizeof(IMAGE_OPTIONAL_HEADER));

        ref readonly var resource_header = ref FindImageDataDirectory(sizeOfOptionalHeader, optional_header_ptr);

        var section_table = MemoryMarshal.Cast<byte, IMAGE_SECTION_HEADER>(optional_header_ptr.Slice(sizeOfOptionalHeader))
            .Slice(0, header.FileHeader.NumberOfSections);

        ref readonly var section_header = ref FindResourceSection(section_table);

        var raw = FileData.Slice((int)section_header.PointerToRawData);

        var resource_section = raw.Slice((int)(resource_header.VirtualAddress - section_header.VirtualAddress));
        ref readonly var resource_dir = ref resource_section.AsRef<IMAGE_RESOURCE_DIRECTORY>();
        var resource_dir_entry = MemoryMarshal.Cast<byte, IMAGE_RESOURCE_DIRECTORY_ENTRY>(raw.Slice((int)(resource_header.VirtualAddress - section_header.VirtualAddress) + sizeof(IMAGE_RESOURCE_DIRECTORY)));

        for (var i = 0; i < resource_dir.NumberOfNamedEntries + resource_dir.NumberOfIdEntries; i++)
        {
            if (!resource_dir_entry[i].NameIsString &&
                resource_dir_entry[i].Id == RT_VERSION &&
                resource_dir_entry[i].DataIsDirectory)
            {
                ref readonly var found_entry = ref resource_dir_entry[i];

                var found_dir = resource_section.Slice((int)found_entry.OffsetToDirectory);
                ref readonly var found_dir_header = ref found_dir.AsRef<IMAGE_RESOURCE_DIRECTORY>();

                if ((found_dir_header.NumberOfIdEntries + found_dir_header.NumberOfNamedEntries) == 0)
                {
                    continue;
                }

                var found_dir_entry = MemoryMarshal.Cast<byte, IMAGE_RESOURCE_DIRECTORY_ENTRY>(found_dir.Slice(sizeof(IMAGE_RESOURCE_DIRECTORY)));

                for (var j = 0; j < found_dir_header.NumberOfNamedEntries + found_dir_header.NumberOfIdEntries; j++)
                {
                    if (!found_dir_entry[j].DataIsDirectory)
                    {
                        continue;
                    }

                    var found_subdir = resource_section.Slice((int)found_dir_entry[j].OffsetToDirectory);
                    ref readonly var found_subdir_header = ref found_subdir.AsRef<IMAGE_RESOURCE_DIRECTORY>();

                    if ((found_subdir_header.NumberOfIdEntries + found_subdir_header.NumberOfNamedEntries) == 0)
                    {
                        continue;
                    }

                    var found_subdir_entry = found_subdir.Slice(sizeof(IMAGE_RESOURCE_DIRECTORY));
                    ref readonly var found_subdir_entry_header = ref found_subdir_entry.AsRef<IMAGE_RESOURCE_DIRECTORY_ENTRY>();

                    if (found_subdir_entry_header.DataIsDirectory)
                    {
                        continue;
                    }

                    var found_data_entry = resource_section.Slice((int)found_subdir_entry_header.OffsetToData);
                    ref readonly var found_data_entry_header = ref found_data_entry.AsRef<IMAGE_RESOURCE_DATA_ENTRY>();

                    var found_res = raw.Slice((int)(found_data_entry_header.OffsetToData - section_header.VirtualAddress));
                    ref readonly var found_res_block = ref found_res.AsRef<VS_VERSIONINFO>();

                    if (found_res_block.Type != 0 ||
                        !MemoryExtensions.Equals(found_res_block.Key, "VS_VERSION_INFO\0".AsSpan(), StringComparison.Ordinal) ||
                        found_res_block.FixedFileInfo.StructVersion == 0 ||
                        found_res_block.FixedFileInfo.Signature != FixedFileVerInfo.FixedFileVerSignature)
                    {
                        throw new BadImageFormatException("No valid version resource in PE file");
                    }

                    ResourceSize = (int)found_data_entry_header.Size;

                    return ref found_res_block;
                }
            }
        }

        throw new BadImageFormatException("No version resource in PE file");
    }

    private static ref readonly IMAGE_SECTION_HEADER FindResourceSection(ReadOnlySpan<IMAGE_SECTION_HEADER> section_table)
    {
        for (var i = 0; i < section_table.Length; i++)
        {
            if (section_table[i].Name != _rsrc_id)
            {
                continue;
            }

            return ref section_table[i];
        }

        throw new BadImageFormatException("No resource section found in PE file");
    }

    private static unsafe ref readonly IMAGE_DATA_DIRECTORY FindImageDataDirectory(ushort sizeOfOptionalHeader, ReadOnlySpan<byte> optional_header_ptr)
    {
        if (sizeOfOptionalHeader == sizeof(IMAGE_OPTIONAL_HEADER32) + 16 * sizeof(IMAGE_DATA_DIRECTORY))
        {
            ref readonly var optional_header = ref optional_header_ptr.AsRef<IMAGE_OPTIONAL_HEADER32>();
            var data_directory_ptr = optional_header_ptr.Slice(sizeof(IMAGE_OPTIONAL_HEADER32));
            var data_directory = MemoryMarshal.Cast<byte, IMAGE_DATA_DIRECTORY>(data_directory_ptr);
            return ref data_directory[2];
        }
        else if (sizeOfOptionalHeader == sizeof(IMAGE_OPTIONAL_HEADER64) + 16 * sizeof(IMAGE_DATA_DIRECTORY))
        {
            ref readonly var optional_header = ref optional_header_ptr.AsRef<IMAGE_OPTIONAL_HEADER64>();
            var data_directory_ptr = optional_header_ptr.Slice(sizeof(IMAGE_OPTIONAL_HEADER64));
            var data_directory = MemoryMarshal.Cast<byte, IMAGE_DATA_DIRECTORY>(data_directory_ptr);
            return ref data_directory[2];
        }

        throw new BadImageFormatException();
    }

    /// <summary>
    /// Gets numeric block from PE version resource
    /// </summary>
    /// <param name="versionResource">Pointer to version resource</param>
    /// <param name="SubBlock">Name of sub block</param>
    /// <returns>Located uint value, or null if not found</returns>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    internal static unsafe uint? QueryValueInt(in VS_VERSIONINFO versionResource, string SubBlock)
    {
        if (SubBlock == null)
        {
            SubBlock = "\\StringFileInfo\\040904E4\\FileDescription";
        }

        fixed (VS_VERSIONINFO* versionResourcePtr = &versionResource)
        {
            if (!WindowsSpecific.VerQueryValue(versionResourcePtr, SubBlock, out uint* lpVerBuf, out var len) ||
                lpVerBuf == null ||
                len != sizeof(uint))
            {
                return null;
            }
            else
            {
                return *lpVerBuf;
            }
        }
    }

    /// <summary>
    /// Gets string block from PE version resource
    /// </summary>
    /// <param name="versionResource">Pointer to version resource</param>
    /// <param name="SubBlock">Name of sub block</param>
    /// <returns>Pointer to located string, or null if not found</returns>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    internal static unsafe string QueryValueString(in VS_VERSIONINFO versionResource, string SubBlock)
    {
        if (SubBlock == null)
        {
            SubBlock = "\\StringFileInfo\\040904E4\\FileDescription";
        }

        fixed (VS_VERSIONINFO* versionResourcePtr = &versionResource)
        {
            if (WindowsSpecific.VerQueryValue(versionResourcePtr, SubBlock, out char* lpVerBuf, out var len) &&
                len > 0)
            {
                return new(lpVerBuf, 0, len);
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Gets string block from PE version resource using default or specific language translation for the version resource
    /// </summary>
    /// <param name="versionResource">Pointer to version resource</param>
    /// <param name="strRecordName">Name of string record</param>
    /// <param name="dwTranslationCode">Translation language code or MaxValue to use default for version resource</param>
    /// <returns>Pointer to located string, or null if not found</returns>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    internal static string QueryValueWithTranslation(in VS_VERSIONINFO versionResource, string strRecordName, DWORD dwTranslationCode = DWORD.MaxValue)
    {
        const DWORD dwDefaultTranslationCode = 0x04E40409;
        if (dwTranslationCode == DWORD.MaxValue)
        {
            var lpwTranslationCode = QueryValueInt(versionResource, "\\VarFileInfo\\Translation");
            if (lpwTranslationCode.HasValue)
            {
                dwTranslationCode = lpwTranslationCode.Value;
            }
            else
            {
                dwTranslationCode = dwDefaultTranslationCode;
            }
        }

        var SubBlock = $"\\StringFileInfo\\{LOWORD(dwTranslationCode):X4}{HIWORD(dwTranslationCode):X4}\\{strRecordName}";

        return QueryValueString(versionResource, SubBlock);
    }
}

/// <summary>
/// File version resource information
/// </summary>
public class NativeFileVersion
{
    /// <summary>
    /// Fixed numeric fields
    /// </summary>
    public FixedFileVerInfo Fixed { get; }

    /// <summary>
    /// Common string fields, if present
    /// </summary>
    public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// File version from fixed numeric fields
    /// </summary>
    public Version FileVersion => Fixed.FileVersion;

    /// <summary>
    /// Product version from fixed numeric fields
    /// </summary>
    public Version ProductVersion => Fixed.ProductVersion;

    /// <summary>
    /// File date from fixed numeric fields, if present
    /// </summary>
    public DateTime? FileDate
    {
        get
        {
            var filetime = NativePE.LARGE_INTEGER(
                LowPart: Fixed.FileDateLS,
                HighPart: Fixed.FileDateMS);

            if (filetime > 0)
            {
                return DateTime.FromFileTime(filetime);
            }

            return null;
        }
    }

    /// <summary>
    /// Parses raw or mapped file data into a NativeFileVersion structure
    /// </summary>
    /// <param name="fileData">Raw or mapped exe or dll file data with a version resource</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public NativeFileVersion(ReadOnlySpan<byte> fileData)
    {
        ref readonly var ptr = ref NativePE.GetRawFileVersionResource(fileData, out var resourceSize);

        Fixed = ptr.FixedFileInfo;

        var lpdwTranslationCode = NativePE.QueryValueInt(ptr, "\\VarFileInfo\\Translation");

        DWORD dwTranslationCode;
        if (lpdwTranslationCode.HasValue)
        {
            dwTranslationCode = lpdwTranslationCode.Value;
            Span<char> tcLanguageName = stackalloc char[128];
            if (WindowsSpecific.VerLanguageName(dwTranslationCode.LOWORD(), out tcLanguageName[0], 128) != 0)
            {
                Fields.Add("TranslationCode", dwTranslationCode.ToString("X"));
                Fields.Add("LanguageName", tcLanguageName.ReadNullTerminatedUnicodeString());
            }
        }
        else
        {
            dwTranslationCode = 0x04E40409;
        }

        var commonfields = new[] {
                    "CompanyName",
                    "FileDescription",
                    "FileVersion",
                    "InternalName",
                    "LegalCopyright",
                    "OriginalFilename",
                    "ProductName",
                    "ProductVersion"
                };

        foreach (var fieldname in commonfields)
        {
            var fieldvalue = NativePE.QueryValueWithTranslation(ptr, fieldname, dwTranslationCode);

            if (fieldvalue != null)
            {
                Fields.Add(fieldname, fieldvalue);
            }
        }
    }
}

