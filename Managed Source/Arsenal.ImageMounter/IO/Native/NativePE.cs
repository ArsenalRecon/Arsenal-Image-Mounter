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
using Arsenal.ImageMounter.IO.Streams;
using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Buffers;
using System;
using System.Buffers;
using System.Diagnostics;
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

public static class NativePE
{
    private static ReadOnlySpan<byte> RsrcId => ".rsrc\0\0\0"u8;

    /*
    private const int ERROR_RESOURCE_DATA_NOT_FOUND = 1812;
    private const int ERROR_RESOURCE_TYPE_NOT_FOUND = 1813;
    private const int ERROR_NO_MORE_ITEMS = 259;
    */

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

    public static IMAGE_NT_HEADERS GetImageNtHeaders(Stream exe)
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

    public static IMAGE_NT_HEADERS GetImageNtHeaders(string exepath)
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
    public static ref readonly IMAGE_NT_HEADERS GetImageNtHeaders(ReadOnlySpan<byte> fileData)
    {
        ref readonly var dos_header = ref fileData.CastRef<IMAGE_DOS_HEADER>();
        ref readonly var header = ref fileData.Slice(dos_header.e_lfanew).CastRef<IMAGE_NT_HEADERS>();

        if (header.Signature != 0x4550 || header.FileHeader.SizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        return ref header;
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
        ref readonly var dos_header = ref fileData.CastRef<IMAGE_DOS_HEADER>();

        if (dos_header.e_magic != IMAGE_DOS_HEADER.ExpectedMagic)
        {
            throw new BadImageFormatException();
        }

        var header_ptr = fileData.Slice(dos_header.e_lfanew);

        ref readonly var header = ref header_ptr.CastRef<IMAGE_NT_HEADERS>();

        var sizeOfOptionalHeader = header.FileHeader.SizeOfOptionalHeader;

        if (header.Signature != IMAGE_NT_HEADERS.ExpectedSignature
            || sizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        var optional_header_ptr = fileData.Slice(dos_header.e_lfanew + sizeof(IMAGE_NT_HEADERS) - sizeof(IMAGE_OPTIONAL_HEADER));

        var data_table = GetImageDataTable(sizeOfOptionalHeader, optional_header_ptr);

        ref readonly var resource_header = ref data_table[(int)ImageDirectoryEntry.Resource];

        var section_table = MemoryMarshal.Cast<byte, ImageSectionHeader>(optional_header_ptr.Slice(sizeOfOptionalHeader))
            .Slice(0, header.FileHeader.NumberOfSections);

        ref readonly var section_header = ref FindResourceSection(section_table);

        var raw = fileData.Slice((int)section_header.PointerToRawData);

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
                    throw new FileNotFoundException("Unsupported version resource in PE file");
                }

                return found_res;
            }
        }

        throw new FileNotFoundException("No version resource in PE file");
    }

    /// <summary>
    /// Locates directory entry in a PE image
    /// </summary>
    /// <param name="fileData">Pointer to raw or mapped exe or dll</param>
    /// <param name="imageDirectoryEntry"></param>
    /// <returns>Reference to located certificate</returns>
    public static unsafe ImageDataDirectory GetRawFileDirectoryEntry(ReadOnlySpan<byte> fileData, ImageDirectoryEntry imageDirectoryEntry)
    {
        ref readonly var dos_header = ref fileData.CastRef<IMAGE_DOS_HEADER>();

        if (dos_header.e_magic != IMAGE_DOS_HEADER.ExpectedMagic)
        {
            throw new BadImageFormatException();
        }

        var header_ptr = fileData.Slice(dos_header.e_lfanew);

        ref readonly var header = ref header_ptr.CastRef<IMAGE_NT_HEADERS>();

        var sizeOfOptionalHeader = header.FileHeader.SizeOfOptionalHeader;

        if (header.Signature != IMAGE_NT_HEADERS.ExpectedSignature
            || sizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        var optional_header_ptr = fileData.Slice(dos_header.e_lfanew + sizeof(IMAGE_NT_HEADERS) - sizeof(IMAGE_OPTIONAL_HEADER));

        var data_table = GetImageDataTable(sizeOfOptionalHeader, optional_header_ptr);

        return data_table[(int)imageDirectoryEntry];
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

        ref readonly var dos_header = ref fileSpan.CastRef<IMAGE_DOS_HEADER>();

        if (dos_header.e_magic != IMAGE_DOS_HEADER.ExpectedMagic)
        {
            throw new BadImageFormatException();
        }

        var header_ptr = fileSpan.Slice(dos_header.e_lfanew);

        ref readonly var header = ref header_ptr.CastRef<IMAGE_NT_HEADERS>();

        var sizeOfOptionalHeader = header.FileHeader.SizeOfOptionalHeader;

        if (header.Signature != IMAGE_NT_HEADERS.ExpectedSignature
            || sizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        var optional_header_ptr = fileSpan.Slice(dos_header.e_lfanew + sizeof(IMAGE_NT_HEADERS) - sizeof(IMAGE_OPTIONAL_HEADER));

        ReadOnlySpan<ImageDataDirectory> data_table;

        int checksumFieldOffset;

        if (sizeOfOptionalHeader == sizeof(ImageOptionalHeader32) + 16 * sizeof(ImageDataDirectory))
        {
            ref readonly var optional_header = ref optional_header_ptr.CastRef<ImageOptionalHeader32>();
            checksumFieldOffset = (int)Unsafe.ByteOffset(ref Unsafe.AsRef(in fileSpan[0]), ref Unsafe.AsRef(in MemoryMarshal.AsBytes(BufferExtensions.CreateReadOnlySpan(in optional_header.CheckSum, 1))[0]));
            var data_directory_ptr = optional_header_ptr.Slice(sizeof(ImageOptionalHeader32));
            data_table = MemoryMarshal.Cast<byte, ImageDataDirectory>(data_directory_ptr);
        }
        else if (sizeOfOptionalHeader == sizeof(ImageOptionalHeader64) + 16 * sizeof(ImageDataDirectory))
        {
            ref readonly var optional_header = ref optional_header_ptr.CastRef<ImageOptionalHeader64>();
            checksumFieldOffset = (int)Unsafe.ByteOffset(ref Unsafe.AsRef(in fileSpan[0]), ref Unsafe.AsRef(in MemoryMarshal.AsBytes(BufferExtensions.CreateReadOnlySpan(in optional_header.CheckSum, 1))[0]));
            var data_directory_ptr = optional_header_ptr.Slice(sizeof(ImageOptionalHeader64));
            data_table = MemoryMarshal.Cast<byte, ImageDataDirectory>(data_directory_ptr);
        }
        else
        {
            throw new BadImageFormatException();
        }

        hashAlgorithm.Initialize();

        hashAlgorithm.TransformBlock(fileData, 0, checksumFieldOffset, null, 0);

        var dataTableEntryOffset = (int)Unsafe.ByteOffset(ref Unsafe.AsRef(in fileSpan[0]), ref Unsafe.AsRef(in MemoryMarshal.AsBytes(data_table.Slice((int)ImageDirectoryEntry.Security, 1))[0]));

        var afterChecksumFieldOffset = checksumFieldOffset + sizeof(uint);

        hashAlgorithm.TransformBlock(fileData, afterChecksumFieldOffset, dataTableEntryOffset - afterChecksumFieldOffset, null, 0);

        ref readonly var security_header = ref data_table[(int)ImageDirectoryEntry.Security];

        var afterDataTableEntryOffset = dataTableEntryOffset + sizeof(ImageDataDirectory);

        if (security_header.Size == 0)
        {
            hashAlgorithm.TransformBlock(fileData, afterDataTableEntryOffset, fileLength - afterDataTableEntryOffset, null, 0);
        }
        else
        {
            hashAlgorithm.TransformBlock(fileData, afterDataTableEntryOffset, (int)(security_header.RelativeVirtualAddress - afterDataTableEntryOffset), null, 0);
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
        .Slice(0, MemoryMarshal.Read<WinCertificateHeader>(certificateSection).Length)
        .Slice(sizeof(WinCertificateHeader));

    private static ref readonly ImageSectionHeader FindResourceSection(ReadOnlySpan<ImageSectionHeader> section_table)
    {
        for (var i = 0; i < section_table.Length; i++)
        {
            if (section_table[i].Name.SequenceEqual(RsrcId))
            {
                return ref section_table[i];
            }
        }

        throw new BadImageFormatException("No resource section found in PE file");
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

    private static unsafe ReadOnlySpan<ImageDataDirectory> GetImageDataTable(ushort sizeOfOptionalHeader, ReadOnlySpan<byte> optional_header_ptr)
    {
        if (sizeOfOptionalHeader == sizeof(ImageOptionalHeader32) + 16 * sizeof(ImageDataDirectory))
        {
            var data_directory_ptr = optional_header_ptr.Slice(sizeof(ImageOptionalHeader32));
            var data_directory = MemoryMarshal.Cast<byte, ImageDataDirectory>(data_directory_ptr);
            return data_directory;
        }
        else if (sizeOfOptionalHeader == sizeof(ImageOptionalHeader64) + 16 * sizeof(ImageDataDirectory))
        {
            var data_directory_ptr = optional_header_ptr.Slice(sizeof(ImageOptionalHeader64));
            var data_directory = MemoryMarshal.Cast<byte, ImageDataDirectory>(data_directory_ptr);
            return data_directory;
        }

        throw new BadImageFormatException($"Unsupported size of optional header ({sizeOfOptionalHeader})");
    }
}

