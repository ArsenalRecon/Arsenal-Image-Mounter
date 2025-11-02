//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using LTRData.Extensions.Buffers;
using System;
using BYTE = System.Byte;
using DWORD = System.UInt32;
using ULONGLONG = System.UInt64;
using WORD = System.UInt16;

#pragma warning disable 0649

namespace Arsenal.ImageMounter.Internal;

public readonly unsafe struct ImageOptionalHeader32
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
    public readonly DWORD BaseOfData;

    //
    // NT additional fields.
    //

    public readonly DWORD ImageBase;
    public readonly DWORD SectionAlignment;
    public readonly DWORD FileAlignment;
    public readonly WORD MajorOperatingSystemVersion;
    public readonly WORD MinorOperatingSystemVersion;
    public readonly WORD MajorImageVersion;
    public readonly WORD MinorImageVersion;
    public readonly WORD MajorSubsystemVersion;
    public readonly WORD MinorSubsystemVersion;
    public readonly DWORD Win32VersionValue;
    public readonly DWORD SizeOfImage;
    public readonly DWORD SizeOfHeaders;
    public readonly DWORD CheckSum;
    public readonly WORD Subsystem;
    public readonly WORD DllCharacteristics;
    public readonly DWORD SizeOfStackReserve;
    public readonly DWORD SizeOfStackCommit;
    public readonly DWORD SizeOfHeapReserve;
    public readonly DWORD SizeOfHeapCommit;
    public readonly DWORD LoaderFlags;
    public readonly DWORD NumberOfRvaAndSizes;

    // Here follows 16 IMAGE_DATA_DIRECTORY entries
}

public readonly unsafe struct ImageOptionalHeader64
{
    public readonly WORD Magic;
    public readonly BYTE MajorLinkerVersion;
    public readonly BYTE MinorLinkerVersion;
    public readonly DWORD SizeOfCode;
    public readonly DWORD SizeOfInitializedData;
    public readonly DWORD SizeOfUninitializedData;
    public readonly DWORD AddressOfEntryPoint;
    public readonly DWORD BaseOfCode;
    public readonly ULONGLONG ImageBase;
    public readonly DWORD SectionAlignment;
    public readonly DWORD FileAlignment;
    public readonly WORD MajorOperatingSystemVersion;
    public readonly WORD MinorOperatingSystemVersion;
    public readonly WORD MajorImageVersion;
    public readonly WORD MinorImageVersion;
    public readonly WORD MajorSubsystemVersion;
    public readonly WORD MinorSubsystemVersion;
    public readonly DWORD Win32VersionValue;
    public readonly DWORD SizeOfImage;
    public readonly DWORD SizeOfHeaders;
    public readonly DWORD CheckSum;
    public readonly WORD Subsystem;
    public readonly WORD DllCharacteristics;
    public readonly ULONGLONG SizeOfStackReserve;
    public readonly ULONGLONG SizeOfStackCommit;
    public readonly ULONGLONG SizeOfHeapReserve;
    public readonly ULONGLONG SizeOfHeapCommit;
    public readonly DWORD LoaderFlags;
    public readonly DWORD NumberOfRvaAndSizes;

    // Here follows 16 IMAGE_DATA_DIRECTORY entries
}

public struct ImageSectionHeader
{
    private unsafe fixed byte name[8];
    public unsafe ReadOnlySpan<byte> Name => BufferExtensions.CreateReadOnlySpan(name[0], 8);
    public readonly DWORD VirtualSize;
    public readonly DWORD VirtualAddress;
    public readonly DWORD SizeOfRawData;
    public readonly DWORD PointerToRawData;
    public readonly DWORD PointerToRelocations;
    public readonly DWORD PointerToLinenumbers;
    public readonly WORD NumberOfRelocations;
    public readonly WORD NumberOfLinenumbers;
    public readonly DWORD Characteristics;
}

public readonly struct ImageResourceDirectory
{
    public readonly DWORD Characteristics;
    public readonly DWORD TimeDateStamp;
    public readonly WORD MajorVersion;
    public readonly WORD MinorVersion;
    public readonly WORD NumberOfNamedEntries;
    public readonly WORD NumberOfIdEntries;
    //  IMAGE_RESOURCE_DIRECTORY_ENTRY DirectoryEntries[];
}

public readonly struct ImageResourceDirectoryEntry
{
    private readonly DWORD NameId;

    public readonly uint OffsetToData;

    public bool NameIsString => (NameId & 0x80000000) != 0;
    public ushort Id => (ushort)NameId;
    public uint NameOffset => NameId & 0x7fffffffu;
    public bool DataIsDirectory => (OffsetToData & 0x80000000u) != 0;
    public uint OffsetToDirectory => OffsetToData & 0x7fffffffu;
}

public readonly struct ImageResourceDataEntry
{
    public readonly DWORD OffsetToData;
    public readonly DWORD Size;
    public readonly DWORD CodePage;
    public readonly DWORD Reserved;
}

