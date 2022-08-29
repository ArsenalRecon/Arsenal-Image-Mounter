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
using System.Buffers;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

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

        var SubBlock = $"\\StringFileInfo\\{NativePE.LOWORD(dwTranslationCode):X4}{NativePE.HIWORD(dwTranslationCode):X4}\\{strRecordName}";

        return QueryValueString(versionResource, SubBlock);
    }

    private static readonly string[] _commonfields = {
        "CompanyName",
        "FileDescription",
        "FileVersion",
        "InternalName",
        "LegalCopyright",
        "OriginalFilename",
        "ProductName",
        "ProductVersion"
    };

    /// <summary>
    /// Parses raw or mapped file data into a NativeFileVersion structure
    /// </summary>
    /// <param name="fileData">Raw or mapped exe or dll file data with a version resource</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public NativeFileVersion(ReadOnlySpan<byte> fileData)
    {
        ref readonly var ptr = ref NativePE.GetRawFileVersionResource(fileData, out var resourceSize);

        Fixed = ptr.FixedFileInfo;

        var lpdwTranslationCode = QueryValueInt(ptr, "\\VarFileInfo\\Translation");

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

        foreach (var fieldname in _commonfields)
        {
            var fieldvalue = QueryValueWithTranslation(ptr, fieldname, dwTranslationCode);

            if (fieldvalue != null)
            {
                Fields.Add(fieldname, fieldvalue);
            }
        }
    }
}

