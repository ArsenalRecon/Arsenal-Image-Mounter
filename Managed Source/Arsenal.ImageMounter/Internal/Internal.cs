//  
//  Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using BYTE = System.Byte;
using DWORD = System.UInt32;
using ULONGLONG = System.UInt64;
using WORD = System.UInt16;

#pragma warning disable 0649

namespace Arsenal.ImageMounter.IO.Internal;

internal readonly unsafe struct IMAGE_OPTIONAL_HEADER32
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
    public DWORD BaseOfData { get; }

    //
    // NT additional fields.
    //

    public DWORD ImageBase { get; }
    public DWORD SectionAlignment { get; }
    public DWORD FileAlignment { get; }
    public WORD MajorOperatingSystemVersion { get; }
    public WORD MinorOperatingSystemVersion { get; }
    public WORD MajorImageVersion { get; }
    public WORD MinorImageVersion { get; }
    public WORD MajorSubsystemVersion { get; }
    public WORD MinorSubsystemVersion { get; }
    public DWORD Win32VersionValue { get; }
    public DWORD SizeOfImage { get; }
    public DWORD SizeOfHeaders { get; }
    public DWORD CheckSum { get; }
    public WORD Subsystem { get; }
    public WORD DllCharacteristics { get; }
    public DWORD SizeOfStackReserve { get; }
    public DWORD SizeOfStackCommit { get; }
    public DWORD SizeOfHeapReserve { get; }
    public DWORD SizeOfHeapCommit { get; }
    public DWORD LoaderFlags { get; }
    public DWORD NumberOfRvaAndSizes { get; }

    // Here follows 16 IMAGE_DATA_DIRECTORY entries
}

internal readonly unsafe struct IMAGE_OPTIONAL_HEADER64
{
    public WORD Magic { get; }
    public BYTE MajorLinkerVersion { get; }
    public BYTE MinorLinkerVersion { get; }
    public DWORD SizeOfCode { get; }
    public DWORD SizeOfInitializedData { get; }
    public DWORD SizeOfUninitializedData { get; }
    public DWORD AddressOfEntryPoint { get; }
    public DWORD BaseOfCode { get; }
    public ULONGLONG ImageBase { get; }
    public DWORD SectionAlignment { get; }
    public DWORD FileAlignment { get; }
    public WORD MajorOperatingSystemVersion { get; }
    public WORD MinorOperatingSystemVersion { get; }
    public WORD MajorImageVersion { get; }
    public WORD MinorImageVersion { get; }
    public WORD MajorSubsystemVersion { get; }
    public WORD MinorSubsystemVersion { get; }
    public DWORD Win32VersionValue { get; }
    public DWORD SizeOfImage { get; }
    public DWORD SizeOfHeaders { get; }
    public DWORD CheckSum { get; }
    public WORD Subsystem { get; }
    public WORD DllCharacteristics { get; }
    public ULONGLONG SizeOfStackReserve { get; }
    public ULONGLONG SizeOfStackCommit { get; }
    public ULONGLONG SizeOfHeapReserve { get; }
    public ULONGLONG SizeOfHeapCommit { get; }
    public DWORD LoaderFlags { get; }
    public DWORD NumberOfRvaAndSizes { get; }

    // Here follows 16 IMAGE_DATA_DIRECTORY entries
}

internal readonly struct IMAGE_SECTION_HEADER
{
    public long Name { get; }
    public DWORD VirtualSize { get; }
    public DWORD VirtualAddress { get; }
    public DWORD SizeOfRawData { get; }
    public DWORD PointerToRawData { get; }
    public DWORD PointerToRelocations { get; }
    public DWORD PointerToLinenumbers { get; }
    public WORD NumberOfRelocations { get; }
    public WORD NumberOfLinenumbers { get; }
    public DWORD Characteristics { get; }
}

internal readonly struct IMAGE_RESOURCE_DIRECTORY
{
    public DWORD Characteristics { get; }
    public DWORD TimeDateStamp { get; }
    public WORD MajorVersion { get; }
    public WORD MinorVersion { get; }
    public WORD NumberOfNamedEntries { get; }
    public WORD NumberOfIdEntries { get; }
    //  IMAGE_RESOURCE_DIRECTORY_ENTRY DirectoryEntries[];
}

internal readonly struct IMAGE_RESOURCE_DIRECTORY_ENTRY
{
    private DWORD NameId { get; }
    public uint OffsetToData { get; }

    public bool NameIsString => (NameId & 0x80000000) != 0;
    public ushort Id => (ushort)NameId;
    public uint NameOffset => NameId & 0x7fffffffu;
    public bool DataIsDirectory => (OffsetToData & 0x80000000u) != 0;
    public uint OffsetToDirectory => OffsetToData & 0x7fffffffu;
}

internal readonly struct IMAGE_RESOURCE_DATA_ENTRY
{
    public DWORD OffsetToData { get; }
    public DWORD Size { get; }
    public DWORD CodePage { get; }
    public DWORD Reserved { get; }
}

