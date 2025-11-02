//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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
using System.IO;
using System.Diagnostics.CodeAnalysis;



#if NET6_0_OR_GREATER
using System.Collections.Immutable;
#endif
using System.Runtime.InteropServices;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable 0649
#pragma warning disable 1591
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.IO.Native;

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
    internal static ReadOnlySpan<char> QueryValueWithTranslation(ReadOnlySpan<byte> versionResource, string strRecordName, uint dwTranslationCode = uint.MaxValue)
    {
        const uint dwDefaultTranslationCode = 0x04E40409;
        if (dwTranslationCode == uint.MaxValue)
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
    public static NativeFileVersion GetVersion(ReadOnlySpan<byte> fileData)
    {
        var ver = NativePE.GetRawFileVersionResource(fileData);

        if (ver.IsEmpty)
        {
            throw new ArgumentException("File does not contain a version resource.");
        }

        return new(ver);
    }

    private NativeFileVersion(ReadOnlySpan<byte> ver)
    {
        ref readonly var verHeader = ref ver.CastRef<VS_VERSIONINFO>();

        Fixed = verHeader.FixedFileInfo;

        var lpdwTranslationCode = QueryValueInt(ver, "VarFileInfo", "Translation");

        if (!lpdwTranslationCode.HasValue)
        {
            lpdwTranslationCode = 0x04E40409;
        }

        var fields = QueryValueStrings(ver, "StringFileInfo", lpdwTranslationCode.Value);

        fields["TranslationCode"] = lpdwTranslationCode.Value.ToString("X");

#if NET6_0_OR_GREATER
        Fields = fields.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
#else
        Fields = fields.AsReadOnly();
#endif
    }

    /// <summary>
    /// Parses raw or mapped file data into a NativeFileVersion structure
    /// </summary>
    /// <param name="fileData">Raw or mapped exe or dll file data with a version resource</param>
    /// <param name="version">Located NativeFileVersion structure, or null if not found</param>
    /// <returns>True if version resource found, false if not</returns>
    public static bool TryGetVersion(ReadOnlySpan<byte> fileData, [NotNullWhen(true)] out NativeFileVersion? version)
    {
        version = null;
        
        var ver = NativePE.GetRawFileVersionResource(fileData);
        
        if (ver.IsEmpty)
        {
            return false;
        }

        version = new(ver);
        
        return true;
    }
}
