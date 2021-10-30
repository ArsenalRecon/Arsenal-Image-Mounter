using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WORD = System.Int16;
using DWORD = System.Int32;
using BYTE = System.Byte;
using ULONGLONG = System.Int64;

#pragma warning disable 0649

namespace Arsenal.ImageMounter.IO
{
    using Internal;
    using System.ComponentModel;
    using System.Text;

    namespace Internal
    {
        struct LARGE_INTEGER
        {
            public uint LowPart;
            public int HighPart;

            public long QuadPart => LowPart | ((long)HighPart << 32);
        }

        struct IMAGE_FILE_HEADER
        {
            public readonly WORD Machine;
            public readonly WORD NumberOfSections;
            public readonly DWORD TimeDateStamp;
            public readonly DWORD PointerToSymbolTable;
            public readonly DWORD NumberOfSymbols;
            public readonly WORD SizeOfOptionalHeader;
            public readonly WORD Characteristics;
        }

        struct IMAGE_DATA_DIRECTORY
        {
            public readonly DWORD VirtualAddress;
            public readonly DWORD Size;
        }

        unsafe struct IMAGE_OPTIONAL_HEADER32
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

        unsafe struct IMAGE_OPTIONAL_HEADER64
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

        struct IMAGE_NT_HEADERS
        {
            public readonly int Signature;
            public readonly IMAGE_FILE_HEADER FileHeader;
            public readonly byte OptionalHeader;
        }

        struct IMAGE_SECTION_HEADER
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

        struct IMAGE_RESOURCE_DIRECTORY
        {
            public readonly DWORD Characteristics;
            public readonly DWORD TimeDateStamp;
            public readonly WORD MajorVersion;
            public readonly WORD MinorVersion;
            public readonly WORD NumberOfNamedEntries;
            public readonly WORD NumberOfIdEntries;
            //  IMAGE_RESOURCE_DIRECTORY_ENTRY DirectoryEntries[];
        }

        struct IMAGE_RESOURCE_DIRECTORY_ENTRY
        {
            readonly DWORD NameId;
            readonly DWORD Offset;

            public bool NameIsString => (NameId & 0x80000000) != 0;
            public ushort Id => (ushort)NameId;
            public int NameOffset => NameId & 0x7fffffff;
            public bool DataIsDirectory => (Offset & 0x80000000) != 0;
            public int OffsetToData => Offset;
            public int OffsetToDirectory => Offset & 0x7fffffff;
        }

        struct IMAGE_RESOURCE_DATA_ENTRY
        {
            public readonly DWORD OffsetToData;
            public readonly DWORD Size;
            public readonly DWORD CodePage;
            public readonly DWORD Reserved;
        }

        internal unsafe struct VS_VERSIONINFO
        {
            public readonly ushort wLength;
            public readonly ushort wValueLength;
            public readonly ushort wType;
            public fixed char szKey[16];
            public fixed ushort Padding1[1];
            public FixedFileVerInfo FixedFileInfo { get; }
        }
    }

    public struct FixedFileVerInfo
    {
        public const uint FixedFileVerSignature = 0xFEEF04BD;

        public uint Signature { get; }            /* e.g. 0xfeef04bd */
        public int StrucVersion { get; }         /* e.g. 0x00000042 = "0.42" */
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
    }

    public class NativeFileVersion
    {
        [DllImport("kernel32.dll")]
        private extern static void SetLastError(int errcode);

        [DllImport("dbghelp.dll")]
        private extern unsafe static IMAGE_NT_HEADERS* ImageNtHeader(void* Base);

        [DllImport("version.dll", CharSet = CharSet.Unicode)]
        private extern unsafe static bool VerQueryValue(void* pBlock, string lpSubBlock, out char* lplpBuffer, out int puLen);

        [DllImport("version.dll", CharSet = CharSet.Unicode)]
        private extern unsafe static bool VerQueryValue(void* pBlock, string lpSubBlock, out int* lplpBuffer, out int puLen);

        [DllImport("version.dll", CharSet = CharSet.Unicode)]
        private extern unsafe static DWORD VerLanguageName(DWORD wLang, char *szLang, DWORD cchLang);

        public FixedFileVerInfo Fixed { get; }

        public Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase);

        private readonly static long _rsrc_id = BitConverter.ToInt64(Encoding.ASCII.GetBytes(".rsrc\0\0\0"), 0);

        private const int ERROR_RESOURCE_DATA_NOT_FOUND = 1812;
        private const int ERROR_RESOURCE_TYPE_NOT_FOUND = 1813;
        private const int ERROR_NO_MORE_ITEMS = 259;

        private const ushort RT_VERSION = 16;

        private static ushort LOWORD(int value) => (ushort)(value & 0xffff);
        private static ushort HIWORD(int value) => (ushort)((value >> 16) & 0xffff);
        private static ushort LOWORD(uint value) => (ushort)(value & 0xffff);
        private static ushort HIWORD(uint value) => (ushort)((value >> 16) & 0xffff);

        private unsafe static VS_VERSIONINFO* GetRawFileVersionResource(void* FileData, out int ResourceSize)
        {
            ResourceSize = 0;

            var header = ImageNtHeader(FileData);

            if (header == null || header->Signature != 0x4550 || header->FileHeader.SizeOfOptionalHeader == 0)
            {
                SetLastError(ERROR_RESOURCE_TYPE_NOT_FOUND);
                return null;
            }

            IMAGE_DATA_DIRECTORY *resource_header;

            if (header->FileHeader.SizeOfOptionalHeader == sizeof(IMAGE_OPTIONAL_HEADER32))
            {
                IMAGE_OPTIONAL_HEADER32 *optional_header = (IMAGE_OPTIONAL_HEADER32*) & header->OptionalHeader;
                var data_directory = (IMAGE_DATA_DIRECTORY*)&optional_header->DataDirectory[0];
                resource_header = data_directory + 2;
            }
            else if (header->FileHeader.SizeOfOptionalHeader == sizeof(IMAGE_OPTIONAL_HEADER64))
            {
                IMAGE_OPTIONAL_HEADER64 *optional_header = (IMAGE_OPTIONAL_HEADER64*) & header->OptionalHeader;
                var data_directory = (IMAGE_DATA_DIRECTORY*)&optional_header->DataDirectory[0];
                resource_header = data_directory + 2;
            }
            else
            {
                SetLastError(ERROR_RESOURCE_TYPE_NOT_FOUND);
                return null;
            }

            var section_table = (IMAGE_SECTION_HEADER*)((BYTE*)&header->OptionalHeader + header->FileHeader.SizeOfOptionalHeader);
            IMAGE_SECTION_HEADER* section_header = null;

            for (int i = 0; i < header->FileHeader.NumberOfSections; i++)
            {
                if (section_table[i].Name != _rsrc_id)
                {
                    continue;
                }

                section_header = section_table + i;
                break;
            }

            if (section_header == null)
            {
                SetLastError(ERROR_RESOURCE_DATA_NOT_FOUND);
                return null;
            }

            var raw = (BYTE*)FileData + section_header->PointerToRawData;

            var resource_section = (IMAGE_RESOURCE_DIRECTORY*)(raw + (resource_header->VirtualAddress - section_header->VirtualAddress));
            var resource_dir_entry = (IMAGE_RESOURCE_DIRECTORY_ENTRY*)(resource_section + 1);

            for (int i = 0; i < resource_section->NumberOfNamedEntries + resource_section->NumberOfIdEntries; i++)
            {
                if (!resource_dir_entry[i].NameIsString &&
                    resource_dir_entry[i].Id == RT_VERSION &&
                    resource_dir_entry[i].DataIsDirectory)
                {
                    var found_entry = resource_dir_entry + i;

                    var found_dir = (IMAGE_RESOURCE_DIRECTORY*)((BYTE*)resource_section + found_entry->OffsetToDirectory);

                    if ((found_dir->NumberOfIdEntries + found_dir->NumberOfNamedEntries) == 0)
                    {
                        continue;
                    }

                    var found_dir_entry = (IMAGE_RESOURCE_DIRECTORY_ENTRY*)(found_dir + 1);

                    for (int j = 0; j < found_dir->NumberOfNamedEntries + found_dir->NumberOfIdEntries; j++)
                    {
                        if (!found_dir_entry[j].DataIsDirectory)
                        {
                            continue;
                        }

                        var found_subdir = (IMAGE_RESOURCE_DIRECTORY*)((BYTE*)resource_section + found_dir_entry->OffsetToDirectory);

                        if ((found_subdir->NumberOfIdEntries + found_subdir->NumberOfNamedEntries) == 0)
                        {
                            continue;
                        }

                        var found_subdir_entry = (IMAGE_RESOURCE_DIRECTORY_ENTRY*)(found_subdir + 1);

                        if (found_subdir_entry->DataIsDirectory)
                        {
                            continue;
                        }

                        var found_data_entry = (IMAGE_RESOURCE_DATA_ENTRY*)((BYTE*)resource_section + found_subdir_entry->OffsetToData);

                        var resptr = (VS_VERSIONINFO*)(raw + (found_data_entry->OffsetToData - section_header->VirtualAddress));

                        if (resptr->wType != 0 ||
                            !StringComparer.Ordinal.Equals(new string(resptr->szKey, 0, 15), "VS_VERSION_INFO") ||
                            resptr->FixedFileInfo.Signature != FixedFileVerInfo.FixedFileVerSignature)
                        {
                            SetLastError(ERROR_RESOURCE_DATA_NOT_FOUND);
                            return null;
                        }

                        ResourceSize = found_data_entry->Size;

                        return resptr;
                    }
                }
            }

            SetLastError(ERROR_RESOURCE_TYPE_NOT_FOUND);
            return null;
        }

        unsafe static FixedFileVerInfo GetFixedVersionInfo(void* VersionResource)
        {
            var resptr = (VS_VERSIONINFO*)VersionResource;

            if (resptr == null ||
                resptr->wType != 0 ||
                !StringComparer.Ordinal.Equals(new string(resptr->szKey, 0, 15), "VS_VERSION_INFO") ||
                resptr->FixedFileInfo.Signature != FixedFileVerInfo.FixedFileVerSignature)
            {
                SetLastError(ERROR_RESOURCE_DATA_NOT_FOUND);
                return default;
            }

            return resptr->FixedFileInfo;
        }

        unsafe static int? QueryValueInt(VS_VERSIONINFO* ptr, string SubBlock)
        {
            if (ptr == null)
            {
                SetLastError(ERROR_NO_MORE_ITEMS);
                return null;
            }

            if (SubBlock == null)
            {
                SubBlock = "\\StringFileInfo\\040904E4\\FileDescription";
            }

            if (!VerQueryValue(ptr, SubBlock, out int* lpVerBuf, out var len) ||
                lpVerBuf == null ||
                len != sizeof(int))
            {
                return null;
            }
            else
            {
                return *lpVerBuf;
            }
        }

        unsafe static char* QueryValueString(VS_VERSIONINFO* ptr, string SubBlock)
        {
            if (ptr == null)
            {
                SetLastError(ERROR_NO_MORE_ITEMS);
                return null;
            }

            if (SubBlock == null)
            {
                SubBlock = "\\StringFileInfo\\040904E4\\FileDescription";
            }

            if (!VerQueryValue(ptr, SubBlock, out char* lpVerBuf, out _))
            {
                return null;
            }
            else
            {
                return lpVerBuf;
            }
        }

        unsafe static char* QueryValueWithTranslation(VS_VERSIONINFO* ptr, string strRecordName, DWORD dwTranslationCode)
        {
            if (ptr == null)
            {
                SetLastError(ERROR_NO_MORE_ITEMS);
                return null;
            }

            const DWORD dwDefaultTranslationCode = 0x04E40409;
            if (dwTranslationCode == -1)
            {
                var lpwTranslationCode = QueryValueInt(ptr, "\\VarFileInfo\\Translation");
                if (lpwTranslationCode.HasValue)
                {
                    dwTranslationCode = lpwTranslationCode.Value;
                }
                else
                {
                    dwTranslationCode = dwDefaultTranslationCode;
                }
            }

            var SubBlock = $"\\StringFileInfo\\{LOWORD(dwTranslationCode):X4}{HIWORD(dwTranslationCode):X4}\\{strRecordName}";

            return QueryValueString(ptr, SubBlock);
        }

        public Version FileVersion => new(HIWORD(Fixed.FileVersionMS), LOWORD(Fixed.FileVersionMS), HIWORD(Fixed.FileVersionLS), LOWORD(Fixed.FileVersionLS));

        public Version ProductVersion => new(HIWORD(Fixed.ProductVersionMS), LOWORD(Fixed.ProductVersionMS), HIWORD(Fixed.ProductVersionLS), LOWORD(Fixed.ProductVersionLS));

        public DateTime? FileDate
        {
            get
            {
                LARGE_INTEGER filetime;
                filetime.LowPart = Fixed.FileDateLS;
                filetime.HighPart = Fixed.FileDateMS;
                if (filetime.QuadPart > 0)
                {
                    return DateTime.FromFileTime(filetime.QuadPart);
                }
                return null;
            }
        }

        public unsafe NativeFileVersion(byte[] rawFile)
        {
            if (rawFile == null)
            {
                throw new ArgumentNullException(nameof(rawFile));
            }
            if (rawFile.Length < 512)
            {
                throw new ArgumentException("Array too short", nameof(rawFile));
            }

            fixed (byte* file_data = rawFile)
            {
                var ptr = GetRawFileVersionResource(file_data, out var res_size);
                if (ptr == null)
                {
                    throw new Win32Exception();
                }

                Fixed = GetFixedVersionInfo(ptr);
                if (Fixed.StrucVersion == 0)
                {
                    throw new Win32Exception();
                }

                var lpdwTranslationCode = QueryValueInt(ptr, "\\VarFileInfo\\Translation");

                DWORD dwTranslationCode;
                if (lpdwTranslationCode.HasValue)
                {
                    dwTranslationCode = lpdwTranslationCode.Value;
                    char* tcLanguageName = stackalloc char[128];
                    if (VerLanguageName(LOWORD(dwTranslationCode), tcLanguageName,
                        128) != 0)
                    {
                        Fields.Add("TranslationCode", dwTranslationCode.ToString("X"));
                        Fields.Add("LanguageName", new string(tcLanguageName));
                    }
                }
                else
                {
                    dwTranslationCode = 0x04E40409;
                }

                var commonfields = new[] {
                    "CompanyName",
                    "FileDescription",
                    "FileVersion",
                    "InternalName",
                    "LegalCopyright",
                    "OriginalFilename",
                    "ProductName",
                    "ProductVersion"
                };

                foreach (var fieldname in commonfields)
                {
                    var fieldvalue = QueryValueWithTranslation(ptr, fieldname,
                        dwTranslationCode);

                    if (fieldvalue != null)
                    {
                        Fields.Add(fieldname, new string(fieldvalue));
                    }
                }
            }
        }
    }
}
