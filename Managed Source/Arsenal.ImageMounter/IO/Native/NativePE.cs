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
using Arsenal.ImageMounter.IO.Internal;
using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Buffers;
using System;
using System.Buffers;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression

#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.IO.Native;

public static class NativePE
{
    private static readonly long RsrcId = 0x000000637273722E; // ".rsrc\0\0\0"

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
    public static IMAGE_NT_HEADERS GetImageNtHeaders(ReadOnlySpan<byte> fileData)
    {
        var dos_header = MemoryMarshal.Read<IMAGE_DOS_HEADER>(fileData);
        var header = MemoryMarshal.Read<IMAGE_NT_HEADERS>(fileData.Slice(dos_header.e_lfanew));

        return header.Signature == 0x4550 && header.FileHeader.SizeOfOptionalHeader != 0
            ? header
            : throw new BadImageFormatException();
    }

    /// <summary>
    /// Returns a copy of fixed file version fields in a PE image
    /// </summary>
    /// <param name="fileData">Pointer to raw or mapped exe or dll</param>
    /// <returns>Copy of data from located version resource</returns>
    public static FixedFileVerInfo GetFixedFileVerInfo(ReadOnlySpan<byte> fileData) =>
        GetRawFileVersionResource(fileData, out _).FixedFileInfo;

    /// <summary>
    /// Locates version resource in a PE image
    /// </summary>
    /// <param name="fileData">Pointer to raw or mapped exe or dll</param>
    /// <param name="resourceSize">Returns size of found resource</param>
    /// <returns>Reference to located version resource</returns>
    public static unsafe ref readonly VS_VERSIONINFO GetRawFileVersionResource(ReadOnlySpan<byte> fileData, out int resourceSize)
    {
        resourceSize = 0;

        ref readonly var dos_header = ref fileData.CastRef<IMAGE_DOS_HEADER>();

        var header_ptr = fileData.Slice(dos_header.e_lfanew);

        ref readonly var header = ref header_ptr.CastRef<IMAGE_NT_HEADERS>();

        var sizeOfOptionalHeader = header.FileHeader.SizeOfOptionalHeader;

        if (header.Signature != 0x4550 || sizeOfOptionalHeader == 0)
        {
            throw new BadImageFormatException();
        }

        var optional_header_ptr = fileData.Slice(dos_header.e_lfanew + sizeof(IMAGE_NT_HEADERS) - sizeof(IMAGE_OPTIONAL_HEADER));

        ref readonly var resource_header = ref FindImageDataDirectory(sizeOfOptionalHeader, optional_header_ptr);

        var section_table = MemoryMarshal.Cast<byte, IMAGE_SECTION_HEADER>(optional_header_ptr.Slice(sizeOfOptionalHeader))
            .Slice(0, header.FileHeader.NumberOfSections);

        ref readonly var section_header = ref FindResourceSection(section_table);

        var raw = fileData.Slice((int)section_header.PointerToRawData);

        var resource_section = raw.Slice((int)(resource_header.VirtualAddress - section_header.VirtualAddress));
        ref readonly var resource_dir = ref resource_section.CastRef<IMAGE_RESOURCE_DIRECTORY>();
        var resource_dir_entry = MemoryMarshal.Cast<byte, IMAGE_RESOURCE_DIRECTORY_ENTRY>(raw.Slice((int)(resource_header.VirtualAddress - section_header.VirtualAddress) + sizeof(IMAGE_RESOURCE_DIRECTORY)));

        for (var i = 0; i < resource_dir.NumberOfNamedEntries + resource_dir.NumberOfIdEntries; i++)
        {
            if (!resource_dir_entry[i].NameIsString &&
                resource_dir_entry[i].Id == RT_VERSION &&
                resource_dir_entry[i].DataIsDirectory)
            {
                ref readonly var found_entry = ref resource_dir_entry[i];

                var found_dir = resource_section.Slice((int)found_entry.OffsetToDirectory);
                ref readonly var found_dir_header = ref found_dir.CastRef<IMAGE_RESOURCE_DIRECTORY>();

                if (found_dir_header.NumberOfIdEntries + found_dir_header.NumberOfNamedEntries == 0)
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
                    ref readonly var found_subdir_header = ref found_subdir.CastRef<IMAGE_RESOURCE_DIRECTORY>();

                    if (found_subdir_header.NumberOfIdEntries + found_subdir_header.NumberOfNamedEntries == 0)
                    {
                        continue;
                    }

                    var found_subdir_entry = found_subdir.Slice(sizeof(IMAGE_RESOURCE_DIRECTORY));
                    ref readonly var found_subdir_entry_header = ref found_subdir_entry.CastRef<IMAGE_RESOURCE_DIRECTORY_ENTRY>();

                    if (found_subdir_entry_header.DataIsDirectory)
                    {
                        continue;
                    }

                    var found_data_entry = resource_section.Slice((int)found_subdir_entry_header.OffsetToData);
                    ref readonly var found_data_entry_header = ref found_data_entry.CastRef<IMAGE_RESOURCE_DATA_ENTRY>();

                    var found_res = raw.Slice((int)(found_data_entry_header.OffsetToData - section_header.VirtualAddress));
                    ref readonly var found_res_block = ref found_res.CastRef<VS_VERSIONINFO>();

                    if (found_res_block.Type != 0 ||
                        !found_res_block.Key.Equals("VS_VERSION_INFO\0".AsSpan(), StringComparison.Ordinal) ||
                        found_res_block.FixedFileInfo.StructVersion == 0 ||
                        found_res_block.FixedFileInfo.Signature != FixedFileVerInfo.FixedFileVerSignature)
                    {
                        throw new BadImageFormatException("No valid version resource in PE file");
                    }

                    resourceSize = (int)found_data_entry_header.Size;

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
            if (section_table[i].Name != RsrcId)
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
            ref readonly var optional_header = ref optional_header_ptr.CastRef<IMAGE_OPTIONAL_HEADER32>();
            var data_directory_ptr = optional_header_ptr.Slice(sizeof(IMAGE_OPTIONAL_HEADER32));
            var data_directory = MemoryMarshal.Cast<byte, IMAGE_DATA_DIRECTORY>(data_directory_ptr);
            return ref data_directory[2];
        }
        else if (sizeOfOptionalHeader == sizeof(IMAGE_OPTIONAL_HEADER64) + 16 * sizeof(IMAGE_DATA_DIRECTORY))
        {
            ref readonly var optional_header = ref optional_header_ptr.CastRef<IMAGE_OPTIONAL_HEADER64>();
            var data_directory_ptr = optional_header_ptr.Slice(sizeof(IMAGE_OPTIONAL_HEADER64));
            var data_directory = MemoryMarshal.Cast<byte, IMAGE_DATA_DIRECTORY>(data_directory_ptr);
            return ref data_directory[2];
        }

        throw new BadImageFormatException();
    }
}

