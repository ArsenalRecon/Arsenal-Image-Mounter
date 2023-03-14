//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Extensions;
using System;
using System.Collections.Generic;
#if NET6_0_OR_GREATER
using System.Collections.Immutable;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BYTE = System.Byte;
using DWORD = System.UInt32;
using LONG = System.Int32;
using WORD = System.UInt16;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable 0649
#pragma warning disable 1591
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.IO.Native;
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
    ARM64 = 0xAA64,
    CEE = 0xC0EE
}

/// <summary>
/// PE image header
/// </summary>
public readonly struct IMAGE_FILE_HEADER
{
    public IMAGE_FILE_MACHINE Machine { get; }
    public WORD NumberOfSections { get; }
    public DWORD TimeDateStamp { get; }
    public DWORD PointerToSymbolTable { get; }
    public DWORD NumberOfSymbols { get; }
    public WORD SizeOfOptionalHeader { get; }
    public WORD Characteristics { get; }
}

internal readonly struct IMAGE_DATA_DIRECTORY
{
    public DWORD VirtualAddress { get; }
    public DWORD Size { get; }
}

/// <summary>
/// PE optional header
/// </summary>
public readonly struct IMAGE_OPTIONAL_HEADER
{
    //
    // Standard fields.
    //

    public WORD Magic { get; }
    public BYTE MajorLinkerVersion { get; }
    public BYTE MinorLinkerVersion { get; }
    public DWORD SizeOfCode { get; }
    public DWORD SizeOfInitializedData { get; }
    public DWORD SizeOfUninitializedData { get; }
    public DWORD AddressOfEntryPoint { get; }
    public DWORD BaseOfCode { get; }

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
    public int Signature { get; }
    public IMAGE_FILE_HEADER FileHeader { get; }
    public IMAGE_OPTIONAL_HEADER OptionalHeader { get; }
}

/// <summary>
/// Version resource header fields
/// </summary>
public struct VS_VERSIONINFO
{
    public ushort Length { get; }
    public ushort ValueLength { get; }
    public ushort Type { get; }

    private unsafe fixed char szKey[16];

    private readonly ushort padding1;

    public FixedFileVerInfo FixedFileInfo { get; }

    public unsafe ReadOnlySpan<char> Key => BufferExtensions.CreateReadOnlySpan(szKey[0], 16);
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
internal static partial class WindowsSpecific
{
#if NET7_0_OR_GREATER
    [LibraryImport("version", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VerQueryValueW(void* pBlock, string lpSubBlock, out char* lplpBuffer, out int puLen);

    [LibraryImport("version", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VerQueryValueW(void* pBlock, string lpSubBlock, out uint* lplpBuffer, out int puLen);

    [LibraryImport("version", StringMarshalling = StringMarshalling.Utf16)]
    public static partial DWORD VerLanguageNameW(DWORD wLang, out char szLang, DWORD cchLang);
#else
    [DllImport("version", CharSet = CharSet.Unicode)]
    public static extern unsafe bool VerQueryValueW(void* pBlock, string lpSubBlock, out char* lplpBuffer, out int puLen);

    [DllImport("version", CharSet = CharSet.Unicode)]
    public static extern unsafe bool VerQueryValueW(void* pBlock, string lpSubBlock, out uint* lplpBuffer, out int puLen);

    [DllImport("version", CharSet = CharSet.Unicode)]
    public static extern DWORD VerLanguageNameW(DWORD wLang, out char szLang, DWORD cchLang);
#endif
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
    public IReadOnlyDictionary<string, string> Fields { get; }

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

            return filetime > 0 ? DateTime.FromFileTime(filetime) : null;
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
        SubBlock ??= "\\StringFileInfo\\040904E4\\FileDescription";

        fixed (VS_VERSIONINFO* versionResourcePtr = &versionResource)
        {
            return !WindowsSpecific.VerQueryValueW(versionResourcePtr, SubBlock, out uint* lpVerBuf, out var len) ||
                lpVerBuf == null ||
                len != sizeof(uint)
                ? null
                : *lpVerBuf;
        }
    }

    /// <summary>
    /// Gets string block from PE version resource
    /// </summary>
    /// <param name="versionResource">Pointer to version resource</param>
    /// <param name="SubBlock">Name of sub block</param>
    /// <returns>Pointer to located string, or null if not found</returns>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    internal static unsafe string? QueryValueString(in VS_VERSIONINFO versionResource, string SubBlock)
    {
        SubBlock ??= "\\StringFileInfo\\040904E4\\FileDescription";

        fixed (VS_VERSIONINFO* versionResourcePtr = &versionResource)
        {
            return WindowsSpecific.VerQueryValueW(versionResourcePtr, SubBlock, out char* lpVerBuf, out var len) &&
                len > 0
                ? (new(lpVerBuf, 0, len))
                : null;
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
    internal static string? QueryValueWithTranslation(in VS_VERSIONINFO versionResource, string strRecordName, DWORD dwTranslationCode = DWORD.MaxValue)
    {
        const DWORD dwDefaultTranslationCode = 0x04E40409;
        if (dwTranslationCode == DWORD.MaxValue)
        {
            var lpwTranslationCode = QueryValueInt(versionResource, @"\VarFileInfo\Translation");
            if (lpwTranslationCode.HasValue)
            {
                dwTranslationCode = lpwTranslationCode.Value;
            }
            else
            {
                dwTranslationCode = dwDefaultTranslationCode;
            }
        }

        var SubBlock = $@"\StringFileInfo\{dwTranslationCode.LOWORD():X4}{dwTranslationCode.HIWORD():X4}\{strRecordName}";

        return QueryValueString(versionResource, SubBlock);
    }

    private static readonly string[] Commonfields = {
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

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var lpdwTranslationCode = QueryValueInt(ptr, @"\VarFileInfo\Translation");

        DWORD dwTranslationCode;

        if (lpdwTranslationCode.HasValue)
        {
            dwTranslationCode = lpdwTranslationCode.Value;
            Span<char> tcLanguageName = stackalloc char[128];

            if (WindowsSpecific.VerLanguageNameW(dwTranslationCode.LOWORD(), out tcLanguageName[0], 128) != 0)
            {
                fields.Add("TranslationCode", dwTranslationCode.ToString("X"));
                fields.Add("LanguageName", tcLanguageName.ReadNullTerminatedUnicodeString());
            }
        }
        else
        {
            dwTranslationCode = 0x04E40409;
        }

        foreach (var fieldname in Commonfields)
        {
            var fieldvalue = QueryValueWithTranslation(ptr, fieldname, dwTranslationCode);

            if (fieldvalue != null)
            {
                fields.Add(fieldname, fieldvalue);
            }
        }

#if NET6_0_OR_GREATER
        Fields = fields.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
#else
        Fields = fields.AsReadOnly();
#endif
    }
}

