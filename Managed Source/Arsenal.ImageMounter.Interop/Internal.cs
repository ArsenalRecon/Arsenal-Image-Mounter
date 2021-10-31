using WORD = System.UInt16;
using DWORD = System.UInt32;
using BYTE = System.Byte;
using ULONGLONG = System.UInt64;

#pragma warning disable 0649

namespace Arsenal.ImageMounter.IO.Internal
{
    internal unsafe struct IMAGE_OPTIONAL_HEADER32
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
        public fixed DWORD DataDirectory[32];
    }

    internal unsafe struct IMAGE_OPTIONAL_HEADER64
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
        public fixed DWORD DataDirectory[32];
    }

    internal struct IMAGE_SECTION_HEADER
    {
        public readonly long Name;
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

    internal struct IMAGE_RESOURCE_DIRECTORY
    {
        public readonly DWORD Characteristics;
        public readonly DWORD TimeDateStamp;
        public readonly WORD MajorVersion;
        public readonly WORD MinorVersion;
        public readonly WORD NumberOfNamedEntries;
        public readonly WORD NumberOfIdEntries;
        //  IMAGE_RESOURCE_DIRECTORY_ENTRY DirectoryEntries[];
    }

    internal struct IMAGE_RESOURCE_DIRECTORY_ENTRY
    {
        readonly DWORD NameId;
        readonly DWORD Offset;

        public bool NameIsString => (NameId & 0x80000000) != 0;
        public ushort Id => (ushort)NameId;
        public uint NameOffset => NameId & 0x7fffffffu;
        public bool DataIsDirectory => (Offset & 0x80000000u) != 0;
        public uint OffsetToData => Offset;
        public uint OffsetToDirectory => Offset & 0x7fffffffu;
    }

    internal struct IMAGE_RESOURCE_DATA_ENTRY
    {
        public readonly DWORD OffsetToData;
        public readonly DWORD Size;
        public readonly DWORD CodePage;
        public readonly DWORD Reserved;
    }

}

