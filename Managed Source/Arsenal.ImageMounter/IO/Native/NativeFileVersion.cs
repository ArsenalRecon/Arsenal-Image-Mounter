//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Extensions;
using LTRData.Extensions.Buffers;
using System;
using System.Collections.Generic;

#if NET6_0_OR_GREATER
using System.Collections.Immutable;
#endif
using System.Runtime.InteropServices;
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
    public static readonly unsafe int SizeOf = sizeof(IMAGE_FILE_HEADER);

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
    public static readonly unsafe int SizeOf = sizeof(IMAGE_OPTIONAL_HEADER);

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

public struct IMAGE_DOS_HEADER
{      // DOS .EXE header
    public static readonly WORD ExpectedMagic = MemoryMarshal.Read<WORD>("MZ"u8);

    public static unsafe readonly int SizeOf = sizeof(IMAGE_DOS_HEADER);

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
    public unsafe fixed WORD e_res[4];                // Reserved words
    public readonly WORD e_oemid;                     // OEM identifier (for e_oeminfo)
    public readonly WORD e_oeminfo;                   // OEM information; e_oemid specific
    public unsafe fixed WORD e_res2[10];              // Reserved words
    public readonly LONG e_lfanew;                    // File address of new exe header
}

/// <summary>
/// Base of PE headers
/// </summary>
public readonly struct IMAGE_NT_HEADERS
{
    public static readonly unsafe int SizeOf = sizeof(IMAGE_NT_HEADERS);

    public static readonly WORD ExpectedSignature = MemoryMarshal.Read<WORD>("PE\0\0"u8);

    public int Signature { get; }
    public IMAGE_FILE_HEADER FileHeader { get; }
    public IMAGE_OPTIONAL_HEADER OptionalHeader { get; }
}

public enum VersionResourceType : ushort
{
    Binary,
    Text
}

/// <summary>
/// Version resource header fields
/// </summary>
public readonly struct VersionRecordHeader
{
    public ushort Length { get; }
    public ushort ValueLength { get; }
    public VersionResourceType Type { get; }

    public static readonly unsafe int SizeOf = sizeof(VersionRecordHeader);
}

/// <summary>
/// Version resource header fields
/// </summary>
public struct VS_VERSIONINFO
{
    public VersionRecordHeader Header { get; }

    private unsafe fixed char szKey[16];

    private readonly ushort padding1;

    public FixedFileVerInfo FixedFileInfo { get; }

    public unsafe ReadOnlySpan<char> Key
        => BufferExtensions.CreateReadOnlySpan(szKey[0], 16);

    public readonly unsafe int SizeOf
        => sizeof(VS_VERSIONINFO) - sizeof(FixedFileVerInfo) + Header.ValueLength;
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
    /// Gets numeric value from PE version resource
    /// </summary>
    /// <param name="versionResource">Pointer to version resource</param>
    /// <param name="blockName">Name of sub block</param>
    /// <param name="valueName">Name of value in sub block</param>
    /// <returns>Located uint value, or null if not found</returns>
    internal static unsafe uint? QueryValueInt(ReadOnlySpan<byte> versionResource,
                                               string blockName = "VarFileInfo",
                                               string valueName = "Translation")
    {
        blockName ??= "VarFileInfo";
        valueName ??= "Translation";

        // Skip past fixed version block, if any
        ref readonly var header = ref versionResource.CastRef<VS_VERSIONINFO>();

        var idx = header.SizeOf;
        idx += -idx & 3;

        versionResource = idx < versionResource.Length ? versionResource.Slice(idx) : default;

        while (versionResource.Length > VersionRecordHeader.SizeOf)
        {
            ref readonly var fileInfoBlockHeader = ref versionResource.CastRef<VersionRecordHeader>();
            var fileInfoBlock = versionResource.Slice(0, fileInfoBlockHeader.Length);

            idx = fileInfoBlock.Length;
            idx += -idx & 3;
            versionResource = idx < versionResource.Length ? versionResource.Slice(idx) : default;

            var blockNamePtr = fileInfoBlock.Slice(VersionRecordHeader.SizeOf).ReadNullTerminatedUnicode();

            if (!blockNamePtr.Equals(blockName.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            idx = VersionRecordHeader.SizeOf + (blockNamePtr.Length + 1) * 2;
            idx += -idx & 3;

            var valueBlock = fileInfoBlock.Slice(idx);
            ref readonly var valueBlockHeader = ref valueBlock.CastRef<VersionRecordHeader>();

            var valueBlockNamePtr = valueBlock.Slice(VersionRecordHeader.SizeOf).ReadNullTerminatedUnicode();

            if (!valueBlockNamePtr.Equals(valueName.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            idx = VersionRecordHeader.SizeOf + (valueBlockNamePtr.Length + 1) * 2;
            idx += -idx & 3;

            var value = valueBlock.Slice(idx);

            return MemoryMarshal.Read<uint>(value);
        }

        return null;
    }

    /// <summary>
    /// Gets string block from PE version resource
    /// </summary>
    /// <param name="versionResource">Pointer to version resource</param>
    /// <param name="blockName">Name of sub block</param>
    /// <param name="language">Language translation id, default 040904E4</param>
    /// <param name="valueName">Name of value in sub block</param>
    /// <returns>Pointer to located string, or null if not found</returns>
    internal static unsafe ReadOnlySpan<char> QueryValueString(ReadOnlySpan<byte> versionResource,
                                                               string blockName = "StringFileInfo",
                                                               string language = "040904E4",
                                                               string valueName = "FileDescription")
    {
        blockName ??= "StringFileInfo";
        language ??= "040904E4";
        valueName ??= "FileDescription";

        // Skip past fixed version block, if any
        ref readonly var header = ref versionResource.CastRef<VS_VERSIONINFO>();

        var idx = header.SizeOf;
        idx += -idx & 3;

        versionResource = idx < versionResource.Length ? versionResource.Slice(idx) : default;

        while (versionResource.Length > VersionRecordHeader.SizeOf)
        {
            ref readonly var fileInfoBlockHeader = ref versionResource.CastRef<VersionRecordHeader>();

            if (fileInfoBlockHeader.Length == 0
                || fileInfoBlockHeader.Length > versionResource.Length)
            {
                break;
            }

            var fileInfoBlock = versionResource.Slice(0, fileInfoBlockHeader.Length);

            idx = fileInfoBlockHeader.Length;
            idx += -idx & 3;
            versionResource = idx < versionResource.Length ? versionResource.Slice(idx) : default;

            var blockNamePtr = fileInfoBlock.Slice(VersionRecordHeader.SizeOf).ReadNullTerminatedUnicode();

            if (!blockNamePtr.Equals(blockName.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            idx = VersionRecordHeader.SizeOf + (blockNamePtr.Length + 1) * 2;
            idx += -idx & 3;

            var tableBlock = fileInfoBlock.Slice(idx);
            ref readonly var tableBlockHeader = ref tableBlock.CastRef<VersionRecordHeader>();

            var tableBlockNamePtr = tableBlock.Slice(VersionRecordHeader.SizeOf).ReadNullTerminatedUnicode();

            if (!tableBlockNamePtr.Equals(language.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            idx = VersionRecordHeader.SizeOf + (tableBlockNamePtr.Length + 1) * 2;
            idx += -idx & 3;

            var value = tableBlock.Slice(idx);

            while (value.Length > VersionRecordHeader.SizeOf)
            {
                ref readonly var blockHeader = ref value.CastRef<VersionRecordHeader>();
                var block = value.Slice(0, blockHeader.Length);

                var valueNamePtr = block.Slice(VersionRecordHeader.SizeOf).ReadNullTerminatedUnicode();

                idx = blockHeader.Length;
                idx += -idx & 3;

                value = idx < value.Length ? value.Slice(idx) : default;

                if (!valueNamePtr.Equals(valueName.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                idx = VersionRecordHeader.SizeOf + (valueNamePtr.Length + 1) * 2;
                idx += -idx & 3;

                var valueData = block.Slice(idx).ReadNullTerminatedUnicode();

                return valueData;
            }
        }

        return default;
    }

    /// <summary>
    /// Gets all strings of a string block from PE version resource
    /// </summary>
    /// <param name="versionResource">Pointer to version resource</param>
    /// <param name="blockName">Name of sub block</param>
    /// <param name="dwTranslationCode">Language translation id, default 0x040904E4</param>
    /// <returns>A dictionary with all strings read from string block.</returns>
    internal static unsafe Dictionary<string, string> QueryValueStrings(ReadOnlySpan<byte> versionResource,
                                                                        string blockName = "StringFileInfo",
                                                                        uint dwTranslationCode = 0x040904E4)
    {
        blockName ??= "StringFileInfo";

        var language = $@"{dwTranslationCode.LOWORD():X4}{dwTranslationCode.HIWORD():X4}";

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Skip past fixed version block, if any
        ref readonly var header = ref versionResource.CastRef<VS_VERSIONINFO>();

        var idx = header.SizeOf;
        idx += -idx & 3;

        versionResource = idx < versionResource.Length ? versionResource.Slice(idx) : default;

        while (versionResource.Length > VersionRecordHeader.SizeOf)
        {
            ref readonly var fileInfoBlockHeader = ref versionResource.CastRef<VersionRecordHeader>();

            if (fileInfoBlockHeader.Length == 0
                || fileInfoBlockHeader.Length > versionResource.Length)
            {
                break;
            }

            var fileInfoBlock = versionResource.Slice(0, fileInfoBlockHeader.Length);

            idx = fileInfoBlockHeader.Length;
            idx += -idx & 3;
            versionResource = idx < versionResource.Length ? versionResource.Slice(idx) : default;

            var blockNamePtr = fileInfoBlock.Slice(VersionRecordHeader.SizeOf).ReadNullTerminatedUnicode();

            if (!blockNamePtr.Equals(blockName.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            idx = VersionRecordHeader.SizeOf + (blockNamePtr.Length + 1) * 2;
            idx += -idx & 3;

            var tableBlock = fileInfoBlock.Slice(idx);
            ref readonly var tableBlockHeader = ref tableBlock.CastRef<VersionRecordHeader>();

            var tableBlockNamePtr = tableBlock.Slice(VersionRecordHeader.SizeOf).ReadNullTerminatedUnicode();

            if (!tableBlockNamePtr.Equals(language.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            idx = VersionRecordHeader.SizeOf + (tableBlockNamePtr.Length + 1) * 2;
            idx += -idx & 3;

            var value = tableBlock.Slice(idx);

            while (value.Length > VersionRecordHeader.SizeOf)
            {
                ref readonly var blockHeader = ref value.CastRef<VersionRecordHeader>();
                var block = value.Slice(0, blockHeader.Length);

                var valueNamePtr = block.Slice(VersionRecordHeader.SizeOf).ReadNullTerminatedUnicode();

                idx = blockHeader.Length;
                idx += -idx & 3;

                value = idx < value.Length ? value.Slice(idx) : default;

                idx = VersionRecordHeader.SizeOf + (valueNamePtr.Length + 1) * 2;
                idx += -idx & 3;

                var valueData = block.Slice(idx).ReadNullTerminatedUnicode();

                dict[valueNamePtr.ToString()] = valueData.ToString();
            }
        }

        return dict;
    }

    /// <summary>
    /// Gets string block from PE version resource using default or specific language translation for the version resource
    /// </summary>
    /// <param name="versionResource">Pointer to version resource</param>
    /// <param name="strRecordName">Name of string record</param>
    /// <param name="dwTranslationCode">Translation language code or MaxValue to use default for version resource</param>
    /// <returns>Pointer to located string, or null if not found</returns>
    internal static ReadOnlySpan<char> QueryValueWithTranslation(ReadOnlySpan<byte> versionResource, string strRecordName, DWORD dwTranslationCode = DWORD.MaxValue)
    {
        const DWORD dwDefaultTranslationCode = 0x04E40409;
        if (dwTranslationCode == DWORD.MaxValue)
        {
            var lpwTranslationCode = QueryValueInt(versionResource, "VarFileInfo", "Translation");

            if (lpwTranslationCode.HasValue)
            {
                dwTranslationCode = lpwTranslationCode.Value;
            }
            else
            {
                dwTranslationCode = dwDefaultTranslationCode;
            }
        }

        var language = $@"{dwTranslationCode.LOWORD():X4}{dwTranslationCode.HIWORD():X4}";

        return QueryValueString(versionResource, "StringFileInfo", language, strRecordName);
    }

    /// <summary>
    /// Parses raw or mapped file data into a NativeFileVersion structure
    /// </summary>
    /// <param name="fileData">Raw or mapped exe or dll file data with a version resource</param>
    public NativeFileVersion(ReadOnlySpan<byte> fileData)
    {
        var ptr = NativePE.GetRawFileVersionResource(fileData);

        ref readonly var verHeader = ref ptr.CastRef<VS_VERSIONINFO>();

        Fixed = verHeader.FixedFileInfo;

        var lpdwTranslationCode = QueryValueInt(ptr, "VarFileInfo", "Translation");

        if (!lpdwTranslationCode.HasValue)
        {
            lpdwTranslationCode = 0x04E40409;
        }

        var fields = QueryValueStrings(ptr, "StringFileInfo", lpdwTranslationCode.Value);
        
        fields.Add("TranslationCode", lpdwTranslationCode.Value.ToString("X"));

#if NET6_0_OR_GREATER
        Fields = fields.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
#else
        Fields = fields.AsReadOnly();
#endif
    }
}

