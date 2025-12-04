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
using Arsenal.ImageMounter.Internal;
using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Buffers;
using System;
using System.Buffers;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression

#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.IO.Native;

using BYTE = System.Byte;
using DWORD = System.UInt32;
using LONG = System.Int32;
using WORD = System.UInt16;

public static class NativePE
{
    private static ReadOnlySpan<byte> RsrcId => ".rsrc\0\0\0"u8;

    private const ushort RT_VERSION = 16;

    internal static ushort LOWORD(this int value) => (ushort)(value & 0xffff);
    internal static ushort HIWORD(this int value) => (ushort)(value >> 16 & 0xffff);
    internal static ushort LOWORD(this uint value) => (ushort)(value & 0xffff);
    internal static ushort HIWORD(this uint value) => (ushort)(value >> 16 & 0xffff);
    internal static long LARGE_INTEGER(uint LowPart, int HighPart) => LowPart | (long)HighPart << 32;

    public static FixedFileVerInfo GetFixedFileVerInfo(Stream exe)
    {
        if (exe.CanSeek)
        {
            exe.Position = 0;

            var buffer = ArrayPool<byte>.Shared.Rent((int)exe.Length);
            try
            {
                var span = buffer.AsSpan(0, exe.Read(buffer, 0, (int)exe.Length));
                return GetFixedFileVerInfo(span);
            }

            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        else
        {
            using var buffer = new MemoryStream();

            exe.CopyTo(buffer);

            return GetFixedFileVerInfo(buffer.AsSpan());
        }
    }

    public static async Task<FixedFileVerInfo> GetFixedFileVerInfoAsync(Stream exe, CancellationToken cancellationToken)
    {
        if (exe.CanSeek)
        {
            exe.Position = 0;

            var buffer = ArrayPool<byte>.Shared.Rent((int)exe.Length);
            try
            {
                var length = await exe.ReadAsync(buffer.AsMemory(0, (int)exe.Length), cancellationToken).ConfigureAwait(false);

                return GetFixedFileVerInfo(buffer.AsSpan(0, length));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        else
        {
            using var buffer = new MemoryStream();

            await exe.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);

            return GetFixedFileVerInfo(buffer.AsSpan());
        }
    }

    public static FixedFileVerInfo GetFixedFileVerInfo(string exepath)
    {
        try
        {
            using var mmap = MemoryMappedFile.CreateFromFile(exepath, FileMode.Open, mapName: null, capacity: 0, MemoryMappedFileAccess.Read);

            using var view = mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            return GetFixedFileVerInfo(view.SafeMemoryMappedViewHandle.AsSpan());
        }
        catch (IOException)
        {
        }

        using var file = File.OpenRead(exepath);

        return GetFixedFileVerInfo(file);
    }

    /// <summary>
    /// Gets IMAGE_NT_HEADERS structure from raw PE image
    /// </summary>
    /// <param name="exe">Raw exe or dll data</param>
    /// <returns>IMAGE_NT_HEADERS structure</returns>
    /// <exception cref="BadImageFormatException">Input data is not a PE image</exception>
    public static ImageNtHeaders GetImageNtHeaders(Stream exe)
    {
        var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(exe.Length, 65536));
        try
        {
            var span = buffer.AsSpan(0, exe.Read(buffer, 0, buffer.Length));

            return GetImageNtHeaders(span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Gets IMAGE_NT_HEADERS structure from raw PE image
    /// </summary>
    /// <param name="exepath">Exe or dll file</param>
    /// <returns>IMAGE_NT_HEADERS structure</returns>
    /// <exception cref="BadImageFormatException">Input file is not a PE image</exception>
    public static ImageNtHeaders GetImageNtHeaders(string exepath)
    {
        var size = (int)Math.Min(65536, new FileInfo(exepath).Length);

        try
        {
            using var mmap = MemoryMappedFile.CreateFromFile(exepath, FileMode.Open, mapName: null, capacity: 0, MemoryMappedFileAccess.Read);

            using var view = mmap.CreateViewAccessor(0, size, MemoryMappedFileAccess.Read);

            return GetImageNtHeaders(view.SafeMemoryMappedViewHandle.AsSpan());
        }
        catch (IOException)
        {
        }

        using var file = File.OpenRead(exepath);

        return GetImageNtHeaders(file);
    }

    public static int PadValue(int value, int align) => value + align - 1 & -align;

    /// <summary>
    /// Gets IMAGE_NT_HEADERS structure from raw PE image
    /// </summary>
    /// <param name="fileData">Raw exe or dll data</param>
    /// <returns>IMAGE_NT_HEADERS structure</returns>
    /// <exception cref="BadImageFormatException">Input data is not a PE image</exception>
    public static ref readonly ImageNtHeaders GetImageNtHeaders(ReadOnlySpan<byte> fileData)
    {
        ref readonly var dos_header = ref GetImageDosHeader(fileData);

        if (dos_header.e_lfanew == 0)
        {
            throw new BadImageFormatException("DOS images not supported");
        }

        ref readonly var header = ref fileData.Slice(dos_header.e_lfanew).CastRef<ImageNtHeaders>();

        if (header.Signature != ImageNtHeaders.ExpectedSignature || header.FileHeader.SizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException("Not valid Portable Exceutable format");
        }

        return ref header;
    }

    /// <summary>
    /// Gets IMAGE_DOS_HEADER structure from raw MZ image
    /// </summary>
    /// <param name="fileData">Raw exe or dll data</param>
    /// <returns>IMAGE_DOS_HEADER structure</returns>
    /// <exception cref="BadImageFormatException">Input data is not an MZ image</exception>
    public static ref readonly ImageDosHeader GetImageDosHeader(ReadOnlySpan<byte> fileData)
    {
        ref readonly var dos_header = ref fileData.CastRef<ImageDosHeader>();

        if (dos_header.e_magic != ImageDosHeader.ExpectedMagic)
        {
            throw new BadImageFormatException("Not a valid executable file");
        }

        return ref dos_header;
    }

    /// <summary>
    /// Gets e_lfanew pointed area of raw MZ image. This is where PE
    /// headers are located for PE images. For actual DOS executable
    /// files, an empty span is returned instead.
    /// </summary>
    /// <param name="fileData">Raw exe or dll data</param>
    /// <returns>Location of second header, or empty span if none exists.</returns>
    /// <exception cref="BadImageFormatException">Input data is not an MZ image</exception>
    public static ReadOnlySpan<byte> GetImageSecondHeader(ReadOnlySpan<byte> fileData)
    {
        ref readonly var dos_header = ref fileData.CastRef<ImageDosHeader>();

        if (dos_header.e_magic != ImageDosHeader.ExpectedMagic)
        {
            throw new BadImageFormatException("Not a valid executable file");
        }

        if (dos_header.e_lfanew == 0)
        {
            return default;
        }

        return fileData.Slice(dos_header.e_lfanew);
    }

    /// <summary>
    /// Returns a copy of fixed file version fields in a PE image
    /// </summary>
    /// <param name="fileData">Pointer to raw or mapped exe or dll</param>
    /// <returns>Copy of data from located version resource</returns>
    public static FixedFileVerInfo GetFixedFileVerInfo(ReadOnlySpan<byte> fileData) =>
        GetRawFileVersionResource(fileData).CastRef<VS_VERSIONINFO>().FixedFileInfo;

    /// <summary>
    /// Locates version resource in a PE image
    /// </summary>
    /// <param name="fileData">Pointer to raw or mapped exe or dll</param>
    /// <returns>Reference to located version resource</returns>
    public static unsafe ReadOnlySpan<byte> GetRawFileVersionResource(ReadOnlySpan<byte> fileData)
    {
        var section_header = FindSection(fileData, RsrcId);

        if (section_header.SizeOfRawData == 0)
        {
            return null;
        }

        var raw = fileData.Slice(section_header.PointerToRawData, section_header.SizeOfRawData);

        var resource_header = GetRawFileDirectoryEntry(fileData, ImageDirectoryEntry.Resource);

        if (resource_header.Size == 0)
        {
            return null;
        }

        var resource_section = raw.Slice((int)(resource_header.RelativeVirtualAddress - section_header.VirtualAddress));
        ref readonly var resource_dir = ref resource_section.CastRef<ImageResourceDirectory>();
        var resource_dir_entry = MemoryMarshal.Cast<byte, ImageResourceDirectoryEntry>(
            raw.Slice((int)(resource_header.RelativeVirtualAddress - section_header.VirtualAddress) + sizeof(ImageResourceDirectory)));

        for (var i = 0; i < resource_dir.NumberOfNamedEntries + resource_dir.NumberOfIdEntries; i++)
        {
            if (resource_dir_entry[i].NameIsString ||
                resource_dir_entry[i].Id != RT_VERSION ||
                !resource_dir_entry[i].DataIsDirectory)
            {
                continue;
            }

            ref readonly var found_entry = ref resource_dir_entry[i];

            var found_dir = resource_section.Slice((int)found_entry.OffsetToDirectory);
            ref readonly var found_dir_header = ref found_dir.CastRef<ImageResourceDirectory>();

            if (found_dir_header.NumberOfIdEntries + found_dir_header.NumberOfNamedEntries == 0)
            {
                continue;
            }

            var found_dir_entry = MemoryMarshal.Cast<byte, ImageResourceDirectoryEntry>(found_dir.Slice(sizeof(ImageResourceDirectory)));

            for (var j = 0; j < found_dir_header.NumberOfNamedEntries + found_dir_header.NumberOfIdEntries; j++)
            {
                if (!found_dir_entry[j].DataIsDirectory)
                {
                    continue;
                }

                var found_subdir = resource_section.Slice((int)found_dir_entry[j].OffsetToDirectory);
                ref readonly var found_subdir_header = ref found_subdir.CastRef<ImageResourceDirectory>();

                if (found_subdir_header.NumberOfIdEntries + found_subdir_header.NumberOfNamedEntries == 0)
                {
                    continue;
                }

                var found_subdir_entry = found_subdir.Slice(sizeof(ImageResourceDirectory));
                ref readonly var found_subdir_entry_header = ref found_subdir_entry.CastRef<ImageResourceDirectoryEntry>();

                if (found_subdir_entry_header.DataIsDirectory)
                {
                    continue;
                }

                var found_data_entry = resource_section.Slice((int)found_subdir_entry_header.OffsetToData);
                ref readonly var found_data_entry_header = ref found_data_entry.CastRef<ImageResourceDataEntry>();

                var found_res = raw.Slice(
                    (int)(found_data_entry_header.OffsetToData - section_header.VirtualAddress),
                    (int)found_data_entry_header.Size);

                ref readonly var found_res_block = ref found_res.CastRef<VS_VERSIONINFO>();

                if (found_res_block.Header.Type != VersionResourceType.Binary ||
                    !found_res_block.Key.Equals("VS_VERSION_INFO\0".AsSpan(), StringComparison.Ordinal) ||
                    found_res_block.FixedFileInfo.StructVersion == 0 ||
                    found_res_block.FixedFileInfo.Signature != FixedFileVerInfo.FixedFileVerSignature)
                {
                    return null;
                }

                return found_res;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds and returns the image section header that matches the specified section identifier.
    /// </summary>
    /// <remarks>This method checks the validity of the file data by verifying the DOS header magic number and
    /// the NT headers signature. If these checks fail, the method returns the default value of <see
    /// cref="ImageSectionHeader"/>.</remarks>
    /// <param name="fileData">The binary data of the file to search, represented as a read-only span of bytes.</param>
    /// <param name="sectionId">The identifier of the section to find, represented as a read-only span of eight bytes.</param>
    /// <returns>The <see cref="ImageSectionHeader"/> that matches the specified section identifier. Returns the default value of
    /// <see cref="ImageSectionHeader"/> if the section is not found or if the file data is invalid.</returns>
    public static unsafe ImageSectionHeader FindSection(ReadOnlySpan<byte> fileData, ReadOnlySpan<byte> sectionId)
    {
        ref readonly var dos_header = ref fileData.CastRef<ImageDosHeader>();

        if (dos_header.e_magic != ImageDosHeader.ExpectedMagic)
        {
            return default;
        }

        var header_ptr = fileData.Slice(dos_header.e_lfanew);

        ref readonly var header = ref header_ptr.CastRef<ImageNtHeaders>();

        if (header.Signature != ImageNtHeaders.ExpectedSignature
            || header.FileHeader.SizeOfOptionalHeader == 0)
        {
            return default;
        }

        var section_table_ptr = fileData.Slice(dos_header.e_lfanew + sizeof(ImageNtHeaders) - sizeof(ImageOptionalHeader) + header.FileHeader.SizeOfOptionalHeader);

        var section_table = MemoryMarshal.Cast<byte, ImageSectionHeader>(section_table_ptr)
            .Slice(0, header.FileHeader.NumberOfSections);

        var section_header = FindSection(section_table, sectionId);

        return section_header;
    }

    /// <summary>
    /// Locates directory entry in a PE image
    /// </summary>
    /// <param name="fileData">Pointer to raw or mapped exe or dll</param>
    /// <param name="imageDirectoryEntry"></param>
    /// <returns>Reference to located certificate</returns>
    public static unsafe ref readonly ImageDataDirectory GetRawFileDirectoryEntry(ReadOnlySpan<byte> fileData, ImageDirectoryEntry imageDirectoryEntry)
    {
        ref readonly var dos_header = ref fileData.CastRef<ImageDosHeader>();

        if (dos_header.e_magic != ImageDosHeader.ExpectedMagic)
        {
            throw new BadImageFormatException();
        }

        var header_ptr = fileData.Slice(dos_header.e_lfanew);

        ref readonly var headers = ref header_ptr.CastRef<ImageNtHeaders>();

        var sizeOfOptionalHeader = headers.FileHeader.SizeOfOptionalHeader;

        if (headers.Signature != ImageNtHeaders.ExpectedSignature
            || sizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        var optional_header_ptr = header_ptr.Slice(sizeof(ImageNtHeaders) - sizeof(ImageOptionalHeader), sizeOfOptionalHeader);

        ReadOnlySpan<ImageDataDirectory> data_directory;

        switch (headers.OptionalHeader.Magic)
        {
            case ImageOptionalHeaderMagic.IMAGE_NT_OPTIONAL_HDR32_MAGIC:
                {
                    var data_directory_ptr = optional_header_ptr.Slice(sizeof(ImageOptionalHeader32));

                    data_directory = MemoryMarshal.Cast<byte, ImageDataDirectory>(data_directory_ptr);

                    ref readonly var optional_header = ref optional_header_ptr.CastRef<ImageOptionalHeader32>();

                    data_directory = data_directory.Slice(0, optional_header.NumberOfRvaAndSizes);

                    break;
                }

            case ImageOptionalHeaderMagic.IMAGE_NT_OPTIONAL_HDR64_MAGIC:
                {
                    var data_directory_ptr = optional_header_ptr.Slice(sizeof(ImageOptionalHeader64));

                    data_directory = MemoryMarshal.Cast<byte, ImageDataDirectory>(data_directory_ptr);

                    ref readonly var optional_header = ref optional_header_ptr.CastRef<ImageOptionalHeader64>();

                    data_directory = data_directory.Slice(0, optional_header.NumberOfRvaAndSizes);

                    break;
                }

            case ImageOptionalHeaderMagic.IMAGE_ROM_OPTIONAL_HDR_MAGIC:
                throw new BadImageFormatException("ROM images are not supported");

            default:
                throw new BadImageFormatException($"Unrecognized optional header type: 0x{headers.OptionalHeader.Magic}");
        }

        var index = (int)imageDirectoryEntry;

        if (index < 0 || index >= data_directory.Length)
        {
            throw new BadImageFormatException($"Image data directory does not contain directory index {index}");
        }

        return ref data_directory[index];
    }

    public static unsafe ref readonly uint GetRawFileChecksumField(ReadOnlySpan<byte> fileData)
    {
        ref readonly var dos_header = ref fileData.CastRef<ImageDosHeader>();

        if (dos_header.e_magic != ImageDosHeader.ExpectedMagic)
        {
            throw new BadImageFormatException();
        }

        var header_ptr = fileData.Slice(dos_header.e_lfanew);

        ref readonly var headers = ref header_ptr.CastRef<ImageNtHeaders>();

        var sizeOfOptionalHeader = headers.FileHeader.SizeOfOptionalHeader;

        if (headers.Signature != ImageNtHeaders.ExpectedSignature
            || sizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        var optional_header_ptr = header_ptr.Slice(sizeof(ImageNtHeaders) - sizeof(ImageOptionalHeader), sizeOfOptionalHeader);

        switch (headers.OptionalHeader.Magic)
        {
            case ImageOptionalHeaderMagic.IMAGE_NT_OPTIONAL_HDR32_MAGIC:
                {
                    ref readonly var optional_header = ref optional_header_ptr.CastRef<ImageOptionalHeader32>();

                    return ref optional_header.CheckSum;
                }

            case ImageOptionalHeaderMagic.IMAGE_NT_OPTIONAL_HDR64_MAGIC:
                {
                    ref readonly var optional_header = ref optional_header_ptr.CastRef<ImageOptionalHeader64>();

                    return ref optional_header.CheckSum;
                }

            case ImageOptionalHeaderMagic.IMAGE_ROM_OPTIONAL_HDR_MAGIC:
                throw new BadImageFormatException("ROM images are not supported");

            default:
                throw new BadImageFormatException($"Unrecognized optional header type: 0x{headers.OptionalHeader.Magic}");
        }
    }

    /// <summary>
    /// Gets authenticode hash value for a raw PE image
    /// </summary>
    /// <param name="hashAlgorithmFactory">Hash algorithm</param>
    /// <param name="fileData">Pointer to raw exe or dll</param>
    /// <param name="fileLength"></param>
    /// <returns>Hash value</returns>
    public static unsafe byte[] GetRawFileAuthenticodeHash(Func<HashAlgorithm> hashAlgorithmFactory, byte[] fileData, int fileLength)
    {
        using var hash = hashAlgorithmFactory();
        return GetRawFileAuthenticodeHash(hash, fileData, fileLength);
    }

    /// <summary>
    /// Gets authenticode hash value for a raw PE image
    /// </summary>
    /// <param name="hashAlgorithm">Hash algorithm</param>
    /// <param name="fileData">Raw exe or dll</param>
    /// <param name="fileLength"></param>
    /// <returns>Hash value</returns>
    public static unsafe byte[] GetRawFileAuthenticodeHash(HashAlgorithm hashAlgorithm, byte[] fileData, int fileLength)
    {
        var fileSpan = fileData.AsSpan(0, fileLength);

        ref readonly var dos_header = ref fileSpan.CastRef<ImageDosHeader>();

        if (dos_header.e_magic != ImageDosHeader.ExpectedMagic)
        {
            throw new BadImageFormatException();
        }

        var header_ptr = fileSpan.Slice(dos_header.e_lfanew);

        ref readonly var header = ref header_ptr.CastRef<ImageNtHeaders>();

        var sizeOfOptionalHeader = header.FileHeader.SizeOfOptionalHeader;

        if (header.Signature != ImageNtHeaders.ExpectedSignature
            || sizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        ref readonly var security_header = ref GetRawFileDirectoryEntry(fileSpan, ImageDirectoryEntry.Security);

        var dataTableEntryOffset = (int)Unsafe.ByteOffset(ref Unsafe.AsRef(in fileSpan[0]), ref Unsafe.As<ImageDataDirectory, byte>(ref Unsafe.AsRef(in security_header)));

        ref readonly uint checksumField = ref GetRawFileChecksumField(fileSpan);

        var checksumFieldOffset = (int)Unsafe.ByteOffset(ref Unsafe.AsRef(in fileSpan[0]), ref Unsafe.As<uint, byte>(ref Unsafe.AsRef(in checksumField)));

        hashAlgorithm.Initialize();

        hashAlgorithm.TransformBlock(fileData, 0, checksumFieldOffset, null, 0);

        var afterChecksumFieldOffset = checksumFieldOffset + sizeof(uint);

        hashAlgorithm.TransformBlock(fileData, afterChecksumFieldOffset, dataTableEntryOffset - afterChecksumFieldOffset, null, 0);

        var afterDataTableEntryOffset = dataTableEntryOffset + sizeof(ImageDataDirectory);

        if (security_header.Size == 0)
        {
            hashAlgorithm.TransformBlock(fileData, afterDataTableEntryOffset, fileLength - afterDataTableEntryOffset, null, 0);
        }
        else
        {
            hashAlgorithm.TransformBlock(fileData, afterDataTableEntryOffset, security_header.RelativeVirtualAddress - afterDataTableEntryOffset, null, 0);
        }

        hashAlgorithm.TransformFinalBlock([], 0, 0);

        return hashAlgorithm.Hash!;
    }

    public enum CertificateType : ushort
    {
        /// <summary>
        /// Blob contains an X.509 certificate
        /// </summary>
        X509 = 0x0001,

        /// <summary>
        /// Blob contains a PKCS SignedData structure
        /// </summary>
        PkcsSignedData = 0x0002,

        /// <summary>
        /// Blob contains PKCS1_MODULE_SIGN fields
        /// </summary>
        Pkcs1Sign = 0x0009
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public readonly struct WinCertificateHeader
    {
        public int Length { get; }
        public ushort Revision { get; }
        public CertificateType CertificateType { get; }
    }

    /// <summary>
    /// Locates PKCS#7 certificate blob in a certificate section
    /// </summary>
    /// <param name="certificateSection">Certificate data section in a PE image</param>
    /// <returns>Bytes of embedded certificate blob</returns>
    public static unsafe ReadOnlySpan<byte> GetCertificateBlob(ReadOnlySpan<byte> certificateSection)
        => certificateSection
        .Slice(0, certificateSection.CastRef<WinCertificateHeader>().Length)
        .Slice(sizeof(WinCertificateHeader));

    private static ImageSectionHeader FindSection(ReadOnlySpan<ImageSectionHeader> section_table, ReadOnlySpan<byte> sectionId)
    {
        foreach (var section_header in section_table)
        {
            if (section_header.Name.SequenceEqual(sectionId))
            {
                return section_header;
            }
        }

        return default;
    }
}

/// <summary>
/// Identifies the various directory entries in a PE (Portable Executable) file's optional header.
/// These correspond to IMAGE_DIRECTORY_ENTRY_* constants in winnt.h.
/// </summary>
public enum ImageDirectoryEntry
{
    /// <summary>Export Directory</summary>
    Export = 0,

    /// <summary>Import Directory</summary>
    Import = 1,

    /// <summary>Resource Directory</summary>
    Resource = 2,

    /// <summary>Exception Directory</summary>
    Exception = 3,

    /// <summary>Security Directory</summary>
    Security = 4,

    /// <summary>Base Relocation Table</summary>
    BaseRelocationTable = 5,

    /// <summary>Debug Directory</summary>
    Debug = 6,

    /// <summary>Architecture Specific Data (formerly IMAGE_DIRECTORY_ENTRY_COPYRIGHT on x86)</summary>
    Architecture = 7,

    /// <summary>RVA of Global Pointer</summary>
    GlobalPointer = 8,

    /// <summary>TLS (Thread Local Storage) Directory</summary>
    Tls = 9,

    /// <summary>Load Configuration Directory</summary>
    LoadConfig = 10,

    /// <summary>Bound Import Directory in headers</summary>
    BoundImport = 11,

    /// <summary>Import Address Table</summary>
    ImportAddressTable = 12,

    /// <summary>Delay Load Import Descriptors</summary>
    DelayImport = 13,

    /// <summary>COM Runtime Descriptor</summary>
    ComDescriptor = 14
}

public enum ImageFileMachine : WORD
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
public readonly struct ImageFileHeader
{
    public static readonly unsafe int SizeOf = sizeof(ImageFileHeader);

    public ImageFileMachine Machine { get; }
    public WORD NumberOfSections { get; }
    public DWORD TimeDateStamp { get; }
    public DWORD PointerToSymbolTable { get; }
    public DWORD NumberOfSymbols { get; }
    public WORD SizeOfOptionalHeader { get; }
    public WORD Characteristics { get; }
}

public readonly struct ImageDataDirectory
{
    public int RelativeVirtualAddress { get; }
    public int Size { get; }
}

/// <summary>
/// Identifies the format of the IMAGE_OPTIONAL_HEADER in a PE file.
/// </summary>
public enum ImageOptionalHeaderMagic : ushort
{
    /// <summary>
    /// Standard 32-bit executable (PE32).
    /// </summary>
    IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10B,

    /// <summary>
    /// 64-bit executable (PE32+).
    /// </summary>
    IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20B,

    /// <summary>
    /// ROM image.
    /// </summary>
    IMAGE_ROM_OPTIONAL_HDR_MAGIC = 0x107
}

/// <summary>
/// PE optional header
/// </summary>
public readonly struct ImageOptionalHeader
{
    public static readonly unsafe int SizeOf = sizeof(ImageOptionalHeader);

    //
    // Standard fields.
    //

    public ImageOptionalHeaderMagic Magic { get; }
    public BYTE MajorLinkerVersion { get; }
    public BYTE MinorLinkerVersion { get; }
    public DWORD SizeOfCode { get; }
    public DWORD SizeOfInitializedData { get; }
    public DWORD SizeOfUninitializedData { get; }
    public DWORD AddressOfEntryPoint { get; }
    public DWORD BaseOfCode { get; }

    // Different fields follow depending on architecture
}

public struct ImageDosHeader
{      // DOS .EXE header
    public static readonly WORD ExpectedMagic = MemoryMarshal.Read<WORD>("MZ"u8);

    public static readonly unsafe int SizeOf = sizeof(ImageDosHeader);

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
public readonly struct ImageNtHeaders
{
    public static readonly unsafe int SizeOf = sizeof(ImageNtHeaders);

    public const uint ExpectedSignature = 0x00004550;

    public uint Signature { get; }
    public ImageFileHeader FileHeader { get; }
    public ImageOptionalHeader OptionalHeader { get; }
}

#region N3 Overlay

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct N3OverlayHeader
{
    public static readonly unsafe int SizeOf = sizeof(N3OverlayHeader);

    public const WORD ExpectedSignature = 0x336E;

    public readonly ushort Signature;      // "n3"
    public readonly byte VersionMajor;
    public readonly byte VersionMinor;
    public readonly ushort HeaderSize;
    public readonly ushort NumOverlays;
    public readonly ushort Reserved;
    public readonly ushort OverlayTableOffset;
    public readonly ushort OverlayNameTableOffset;
    public readonly ushort OverlayNameCount;
    // followed by overlay descriptors and name strings
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct N3OverlayDescriptor
{
    public readonly ushort Segment;
    public readonly uint FileOffset;
    public readonly ushort Size;
    public readonly ushort FileIndex;
    public readonly ushort Reserved;
}

#endregion

#region LE LX

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct LE_LX_Header
{
    public static readonly unsafe int SizeOf = sizeof(LE_LX_Header);

    public const WORD ExpectedSignatureLE = 0x454C;

    public const WORD ExpectedSignatureLX = 0x584C;

    // 00h: 'L','E' or 'L','X'
    public readonly WORD Signature;

    // 02h–03h: byte/word endianness (0 = little endian)
    public readonly byte ByteOrder;    // 0 = little-endian
    public readonly byte WordOrder;    // 0 = little-endian

    // 04h: executable format level
    public readonly uint FormatLevel;

    // 08h: CPU type
    public readonly LeLxCpuType CpuType;

    // 0Ah: target OS
    public readonly LeLxTargetOS TargetOS;

    // 0Ch: module version
    public readonly uint ModuleVersion;

    // 10h: module flags
    public readonly LeLxModuleFlags ModuleFlags;

    // 14h: number of memory pages
    public readonly uint PageCount;

    // 18h: Initial CS:EIP (object number then offset)
    public readonly uint EntryEIPObject;
    public readonly uint EntryEIP;

    // 20h: Initial SS:ESP (object number then offset)
    public readonly uint EntryESPObject;
    public readonly uint EntryESP;

    // 28h: memory page size
    public readonly uint PageSize;

    // 2Ch: LE = bytes on last page; LX = page offset shift count
    public readonly uint BytesOnLastPage_or_PageOffShift;

    // 30h–3Fh: fixup/loader sizes & checksums
    public readonly uint FixupSectionSize;
    public readonly uint FixupSectionChecksum;
    public readonly uint LoaderSectionSize;
    public readonly uint LoaderSectionChecksum;

    // 40h–4Fh: object table (offset/count)
    public readonly uint ObjectTableOffset;
    public readonly uint ObjectCount;

    // 48h: object page map table offset
    public readonly uint ObjectPageMapOffset;

    // 4Ch: object iterate data map offset
    public readonly uint ObjectIterDataMapOffset;

    // 50h–57h: resource table (offset/count)
    public readonly uint ResourceTableOffset;
    public readonly uint ResourceCount;

    // 58h: resident names table offset
    public readonly uint ResidentNameTableOffset;

    // 5Ch: entry table offset
    public readonly uint EntryTableOffset;

    // 60h–67h: module directives (offset/count)
    public readonly uint ModuleDirectiveTableOffset;
    public readonly uint ModuleDirectiveCount;

    // 68h–6Fh: fixup page/record tables
    public readonly uint FixupPageTableOffset;
    public readonly uint FixupRecordTableOffset;

    // 70h–77h: import modules (offset/count)
    public readonly uint ImportModuleTableOffset;
    public readonly uint ImportModuleCount;

    // 78h: import procedure name table offset
    public readonly uint ImportProcNameTableOffset;

    // 7Ch: per-page checksum table offset
    public readonly uint PerPageChecksumTableOffset;

    // 80h: start of paged image (file offset)
    public readonly uint DataPagesOffset;

    // 84h: preload page count
    public readonly uint PreloadPageCount;

    // 88h–8Fh: non-resident names (offset/length)
    public readonly uint NonResidentNameTableOffset;
    public readonly uint NonResidentNameTableLength;

    // 90h: non-resident names checksum
    public readonly uint NonResidentNameTableChecksum;

    // 94h: automatic data object (DS provider)
    public readonly uint AutoDataObject;

    // 98h–9Fh: debug info (offset/length)
    public readonly uint DebugInfoOffset;
    public readonly uint DebugInfoLength;

    // A0h–A7h: instance page counts
    public readonly uint InstancePreloadPages;
    public readonly uint InstanceDemandPages;

    // A8h: extra heap allocation (bytes)
    public readonly uint ExtraHeapAllocation;

    // ACh–B7h: reserved
    public readonly uint Reserved_ACh_AFh;
    public readonly uint Reserved_B0h_B3h;
    public readonly uint Reserved_B4h_B7h;

    // VxD-only tail (LE VxD); typically zero for OS/2 LX
    public readonly uint Vxd_VersionInfoOffset; // B8h
    public readonly uint Vxd_UnknownPointer;    // BCh
    public readonly ushort Vxd_DeviceID;        // C0h
    public readonly ushort Vxd_DDKVersion;      // C2h
}

/// <summary>
/// CPU architecture value used in LE/LX headers.
/// </summary>
public enum LeLxCpuType : ushort
{
    /// <summary>Intel 80286.</summary>
    Intel80286 = 1,

    /// <summary>Intel 80386.</summary>
    Intel80386 = 2,

    /// <summary>Intel 80486.</summary>
    Intel80486 = 3,

    /// <summary>Intel 80586 / Pentium or later.</summary>
    Intel80586 = 4,

    /// <summary>MIPS R2000 or R3000 family.</summary>
    MIPS_R2000_R3000 = 0x20,

    /// <summary>MIPS R4000 family.</summary>
    MIPS_R4000 = 0x21,

    /// <summary>DEC Alpha (AXP).</summary>
    DEC_Alpha = 0x40,

    /// <summary>PowerPC.</summary>
    PowerPC = 0x50
}

/// <summary>
/// Operating system type for which the executable was built.
/// </summary>
public enum LeLxTargetOS : ushort
{
    /// <summary>Unknown or unspecified.</summary>
    Unknown = 0,

    /// <summary>OS/2 operating system.</summary>
    OS2 = 1,

    /// <summary>Microsoft Windows.</summary>
    Windows = 2,

    /// <summary>European MS-DOS 4.x (multitasking DOS).</summary>
    EuroDOS = 3,

    /// <summary>Windows 386 (Windows/386 2.x environment).</summary>
    Windows386 = 4,

    /// <summary>IBM OS/2 2.x family API (LX format).</summary>
    OS2FamilyAPI = 5,

    /// <summary>Windows NT (experimental LE variant).</summary>
    WinNT = 6
}

/// <summary>
/// Flags that describe characteristics of the LE/LX module.
/// </summary>
[Flags]
public enum LeLxModuleFlags : uint
{
    /// <summary>No flags set.</summary>
    None = 0x0000_0000,

    /// <summary>Executable is for 16-bit protected mode (286).</summary>
    ProtectedMode286 = 0x0000_0001,

    /// <summary>Executable is for 32-bit protected mode (386+).</summary>
    ProtectedMode386 = 0x0000_0002,

    /// <summary>Module is a library (DLL) rather than an application.</summary>
    LibraryModule = 0x0000_8000,

    /// <summary>Module uses per-process library initialization (OS/2).</summary>
    PerProcessInit = 0x0001_0000,

    /// <summary>Module is multi-thread aware (OS/2).</summary>
    MultiThread = 0x0002_0000,

    /// <summary>Executable uses a protected memory model (OS/2 Warp+).</summary>
    ProtectedMemory = 0x0004_0000,

    /// <summary>Executable uses a single data segment shared among instances.</summary>
    SingleDataSegment = 0x0008_0000,

    /// <summary>Executable supports demand paging.</summary>
    DemandPaged = 0x0010_0000,

    /// <summary>Executable is pageable (OS/2 dynamic loader flag).</summary>
    Pageable = 0x0020_0000,

    /// <summary>Executable is a device driver (VxD or OS/2 driver).</summary>
    DeviceDriver = 0x0040_0000,

    /// <summary>Executable supports 32-bit addressing (flat model).</summary>
    FlatAddressing = 0x0080_0000
}

#endregion

#region NE

/// <summary>
/// NE header flags (bitfield).  
/// Add specific bit definitions from your NE specification as needed.
/// </summary>
[Flags]
public enum NeFlagWord : ushort
{
    None = 0x0000,
    // Example bit definitions (uncomment or adjust as needed):
    // SingleDataSegment = 0x0001,
    // MultipleData = 0x0002,
    // GlobalInit = 0x0010,
    // ProtectedMode = 0x0200,
    // DLL = 0x8000,
}

/// <summary>
/// Target operating system identifier in NE headers.
/// </summary>
public enum NeTargetOS : byte
{
    /// <summary>Unknown or unspecified.</summary>
    Unknown = 0,

    /// <summary>OS/2 operating system.</summary>
    OS2 = 1,

    /// <summary>Microsoft Windows.</summary>
    Windows = 2,

    /// <summary>European multitasking MS-DOS 4.x.</summary>
    EuroDOS4x = 3,

    /// <summary>Windows/386 environment.</summary>
    Windows386 = 4
}

/// <summary>
/// Additional OS/2-specific NE flags (rarely used by Windows).  
/// Add bit definitions as needed from OS/2 NE specs.
/// </summary>
[Flags]
public enum NeOs2ExeFlags : byte
{
    None = 0x00
    // Example OS/2-specific bits could be added here.
}

/// <summary>
/// Represents the header of a 16-bit New Executable (NE) file format,  
/// used by early Microsoft Windows and OS/2 applications.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ImageNeHeader
{
    public static readonly unsafe int SizeOf = sizeof(ImageNeHeader);

    public const WORD ExpectedSignature = 0x454E;

    /// <summary>
    /// File signature — should contain the ASCII characters 'N', 'E'.
    /// </summary>
    public readonly WORD sig;

    /// <summary>
    /// The major linker version.
    /// </summary>
    public readonly byte MajLinkerVersion;

    /// <summary>
    /// The minor linker version (also known as the linker revision).
    /// </summary>
    public readonly byte MinLinkerVersion;

    /// <summary>
    /// Offset of the entry table from the start of the NE header.
    /// </summary>
    public readonly ushort EntryTableOffset;

    /// <summary>
    /// Length of the entry table in bytes.
    /// </summary>
    public readonly ushort EntryTableLength;

    /// <summary>
    /// 32-bit CRC of the entire contents of the file.
    /// </summary>
    public readonly uint FileLoadCRC;

    /// <summary>
    /// Flag word describing attributes of the executable.  
    /// See <see cref="NeFlagWord"/>.
    /// </summary>
    public readonly NeFlagWord FlagWord;

    /// <summary>
    /// The automatic data segment index.
    /// </summary>
    public readonly ushort AutoDataSegIndex;

    /// <summary>
    /// The initial local heap size.
    /// </summary>
    public readonly ushort InitHeapSize;

    /// <summary>
    /// The initial stack size.
    /// </summary>
    public readonly ushort InitStackSize;

    /// <summary>
    /// CS:IP entry point — CS is an index into the segment table.
    /// </summary>
    public readonly uint EntryPoint;

    /// <summary>
    /// SS:SP initial stack pointer — SS is an index into the segment table.
    /// </summary>
    public readonly uint InitStack;

    /// <summary>
    /// Number of segments in the segment table.
    /// </summary>
    public readonly ushort SegCount;

    /// <summary>
    /// Number of module references (DLLs) used by this module.
    /// </summary>
    public readonly ushort ModRefs;

    /// <summary>
    /// Size of the non-resident names table, in bytes.
    /// </summary>
    public readonly ushort NoResNamesTabSiz;

    /// <summary>
    /// Offset of the segment table from the start of the NE header.
    /// </summary>
    public readonly ushort SegTableOffset;

    /// <summary>
    /// Offset of the resources table from the start of the NE header.
    /// </summary>
    public readonly ushort ResTableOffset;

    /// <summary>
    /// Offset of the resident names table from the start of the NE header.
    /// </summary>
    public readonly ushort ResidNamTable;

    /// <summary>
    /// Offset of the module reference table from the start of the NE header.
    /// </summary>
    public readonly ushort ModRefTable;

    /// <summary>
    /// Offset of the imported names table from the start of the NE header.
    /// </summary>
    public readonly ushort ImportNameTable;

    /// <summary>
    /// Offset of the non-resident names table from the start of the file (absolute offset).
    /// </summary>
    public readonly uint OffStartNonResTab;

    /// <summary>
    /// Count of moveable entry points listed in the entry table.
    /// </summary>
    public readonly ushort MovEntryCount;

    /// <summary>
    /// File alignment size shift count (0 = 9 → default 512-byte pages).
    /// </summary>
    public readonly ushort FileAlnSzShftCnt;

    /// <summary>
    /// Number of entries in the resource table (often inaccurate).
    /// </summary>
    public readonly ushort nResTabEntries;

    /// <summary>
    /// Target operating system for which this executable was built.  
    /// See <see cref="NeTargetOS"/>.
    /// </summary>
    public readonly NeTargetOS targOS;

    /// <summary>
    /// Additional OS/2-specific flags.  
    /// See <see cref="NeOs2ExeFlags"/>.
    /// </summary>
    public readonly NeOs2ExeFlags OS2EXEFlags;

    /// <summary>
    /// Offset to return thunks or the start of the gangload area.
    /// </summary>
    public readonly ushort retThunkOffset;

    /// <summary>
    /// Offset to segment reference thunks or size of gangload area.
    /// </summary>
    public readonly ushort segrefthunksoff;

    /// <summary>
    /// Minimum code swap area size.
    /// </summary>
    public readonly ushort mincodeswap;

    /// <summary>
    /// Expected Windows version (minor version byte first, then major).  
    /// For example: 0x0A 0x03 → Windows 3.10.
    /// </summary>
    public readonly byte expctwinverMinor;

    /// <summary>
    /// Expected Windows version (minor version byte first, then major).  
    /// For example: 0x0A 0x03 → Windows 3.10.
    /// </summary>
    public readonly byte expctwinverMajor;
}

#endregion