using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using WORD = System.UInt16;
using DWORD = System.UInt32;
using LONG = System.Int32;
using BYTE = System.Byte;

#pragma warning disable 0649
#pragma warning disable 1591

namespace Arsenal.ImageMounter.IO
{
    using Internal;

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
        CEE = 0xC0EE
    }

    /// <summary>
    /// PE image header
    /// </summary>
    public struct IMAGE_FILE_HEADER
    {
        public readonly IMAGE_FILE_MACHINE Machine;
        public readonly WORD NumberOfSections;
        public readonly DWORD TimeDateStamp;
        public readonly DWORD PointerToSymbolTable;
        public readonly DWORD NumberOfSymbols;
        public readonly WORD SizeOfOptionalHeader;
        public readonly WORD Characteristics;
    }

    internal struct IMAGE_DATA_DIRECTORY
    {
        public readonly DWORD VirtualAddress;
        public readonly DWORD Size;
    }

    /// <summary>
    /// PE optional header
    /// </summary>
    public struct IMAGE_OPTIONAL_HEADER
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
    public struct IMAGE_NT_HEADERS
    {
        public readonly int Signature;
        public readonly IMAGE_FILE_HEADER FileHeader;
        public readonly IMAGE_OPTIONAL_HEADER OptionalHeader;
    }

    /// <summary>
    /// Version resource header fields
    /// </summary>
    public unsafe struct VS_VERSIONINFO
    {
        public readonly ushort wLength;
        public readonly ushort wValueLength;
        public readonly ushort wType;
        public fixed char szKey[16];
        public fixed ushort Padding1[1];
        public FixedFileVerInfo FixedFileInfo { get; }
    }

    /// <summary>
    /// Fixed numeric fields in file version resource
    /// </summary>
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

    public static class NativePE
    {
        [DllImport("kernel32.dll")]
        private extern static void SetLastError(int errcode);

        [DllImport("version.dll", CharSet = CharSet.Unicode)]
        private extern unsafe static bool VerQueryValue(void* pBlock, string lpSubBlock, out char* lplpBuffer, out int puLen);

        [DllImport("version.dll", CharSet = CharSet.Unicode)]
        private extern unsafe static bool VerQueryValue(void* pBlock, string lpSubBlock, out uint* lplpBuffer, out int puLen);

        [DllImport("version.dll", CharSet = CharSet.Unicode)]
        internal extern unsafe static DWORD VerLanguageName(DWORD wLang, char* szLang, DWORD cchLang);

        private readonly static long _rsrc_id = 0x000000637273722E; // ".rsrc\0\0\0"

        private const int ERROR_RESOURCE_DATA_NOT_FOUND = 1812;
        private const int ERROR_RESOURCE_TYPE_NOT_FOUND = 1813;
        private const int ERROR_NO_MORE_ITEMS = 259;

        private const ushort RT_VERSION = 16;

        internal static ushort LOWORD(this int value) => (ushort)(value & 0xffff);
        internal static ushort HIWORD(this int value) => (ushort)((value >> 16) & 0xffff);
        internal static ushort LOWORD(this uint value) => (ushort)(value & 0xffff);
        internal static ushort HIWORD(this uint value) => (ushort)((value >> 16) & 0xffff);
        internal static long LARGE_INTEGER(uint LowPart, int HighPart) => LowPart | ((long)HighPart << 32);

        /// <summary>
        /// Gets IMAGE_NT_HEADERS structure from raw PE image
        /// </summary>
        /// <param name="rawFile">Raw exe or dll data</param>
        /// <returns>IMAGE_NT_HEADERS structure</returns>
        public static IMAGE_NT_HEADERS GetImageNtHeaders(byte[] rawFile)
        {
            using var FileData = PinnedBuffer.Create(rawFile);
            
            var dos_header = FileData.Read<IMAGE_DOS_HEADER>(0);
            var header = FileData.Read<IMAGE_NT_HEADERS>((ulong)dos_header.e_lfanew);

            if (header.Signature != 0x4550 || header.FileHeader.SizeOfOptionalHeader == 0)
            {
                throw new BadImageFormatException();
            }

            return header;
        }

        /// <summary>
        /// Locates version resource in a PE image
        /// </summary>
        /// <param name="FileData">Pointer to raw or mapped exe or dll</param>
        /// <param name="ResourceSize">Returns size of found resource</param>
        /// <returns>Pointer to located version resource or null if none found</returns>
        public unsafe static VS_VERSIONINFO* GetRawFileVersionResource(void* FileData, out int ResourceSize)
        {
            ResourceSize = 0;

            var dos_header = (IMAGE_DOS_HEADER*)FileData;

            var header = (IMAGE_NT_HEADERS*)((byte*)FileData + dos_header->e_lfanew);

            if (header == null || header->Signature != 0x4550 || header->FileHeader.SizeOfOptionalHeader == 0)
            {
                SetLastError(ERROR_RESOURCE_TYPE_NOT_FOUND);
                return null;
            }

            IMAGE_DATA_DIRECTORY* resource_header;

            if (header->FileHeader.SizeOfOptionalHeader == sizeof(IMAGE_OPTIONAL_HEADER32))
            {
                IMAGE_OPTIONAL_HEADER32* optional_header = (IMAGE_OPTIONAL_HEADER32*)&header->OptionalHeader;
                var data_directory = (IMAGE_DATA_DIRECTORY*)&optional_header->DataDirectory[0];
                resource_header = data_directory + 2;
            }
            else if (header->FileHeader.SizeOfOptionalHeader == sizeof(IMAGE_OPTIONAL_HEADER64))
            {
                IMAGE_OPTIONAL_HEADER64* optional_header = (IMAGE_OPTIONAL_HEADER64*)&header->OptionalHeader;
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

                        ResourceSize = (int)found_data_entry->Size;

                        return resptr;
                    }
                }
            }

            SetLastError(ERROR_RESOURCE_TYPE_NOT_FOUND);
            return null;
        }

        /// <summary>
        /// Gets fixed numeric fields from PE version resource
        /// </summary>
        /// <param name="versionResource">Pointer to version resource</param>
        /// <returns>FixedFileVerInfo structure with fixed numeric version fields</returns>
        public unsafe static FixedFileVerInfo GetFixedVersionInfo(VS_VERSIONINFO* versionResource)
        {
            if (versionResource == null ||
                versionResource->wType != 0 ||
                !StringComparer.Ordinal.Equals(new string(versionResource->szKey, 0, 15), "VS_VERSION_INFO") ||
                versionResource->FixedFileInfo.Signature != FixedFileVerInfo.FixedFileVerSignature)
            {
                SetLastError(ERROR_RESOURCE_DATA_NOT_FOUND);
                return default;
            }

            return versionResource->FixedFileInfo;
        }

        /// <summary>
        /// Gets numeric block from PE version resource
        /// </summary>
        /// <param name="versionResource">Pointer to version resource</param>
        /// <param name="SubBlock">Name of sub block</param>
        /// <returns>Located uint value, or null if not found</returns>
        public unsafe static uint? QueryValueInt(VS_VERSIONINFO* versionResource, string SubBlock)
        {
            if (versionResource == null)
            {
                SetLastError(ERROR_NO_MORE_ITEMS);
                return null;
            }

            if (SubBlock == null)
            {
                SubBlock = "\\StringFileInfo\\040904E4\\FileDescription";
            }

            if (!VerQueryValue(versionResource, SubBlock, out uint* lpVerBuf, out var len) ||
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

        /// <summary>
        /// Gets string block from PE version resource
        /// </summary>
        /// <param name="versionResource">Pointer to version resource</param>
        /// <param name="SubBlock">Name of sub block</param>
        /// <returns>Pointer to located string, or null if not found</returns>
        public static unsafe char* QueryValueString(VS_VERSIONINFO* versionResource, string SubBlock)
        {
            if (versionResource == null)
            {
                SetLastError(ERROR_NO_MORE_ITEMS);
                return null;
            }

            if (SubBlock == null)
            {
                SubBlock = "\\StringFileInfo\\040904E4\\FileDescription";
            }

            if (!VerQueryValue(versionResource, SubBlock, out char* lpVerBuf, out _))
            {
                return null;
            }
            else
            {
                return lpVerBuf;
            }
        }

        /// <summary>
        /// Gets string block from PE version resource using default or specific language translation for the version resource
        /// </summary>
        /// <param name="versionResource">Pointer to version resource</param>
        /// <param name="strRecordName">Name of string record</param>
        /// <param name="dwTranslationCode">Translation language code or MaxValue to use default for version resource</param>
        /// <returns>Pointer to located string, or null if not found</returns>
        public static unsafe char* QueryValueWithTranslation(VS_VERSIONINFO* versionResource, string strRecordName, DWORD dwTranslationCode = DWORD.MaxValue)
        {
            if (versionResource == null)
            {
                SetLastError(ERROR_NO_MORE_ITEMS);
                return null;
            }

            const DWORD dwDefaultTranslationCode = 0x04E40409;
            if (dwTranslationCode == DWORD.MaxValue)
            {
                var lpwTranslationCode = QueryValueInt(versionResource, "\\VarFileInfo\\Translation");
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

            return QueryValueString(versionResource, SubBlock);
        }
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
        public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// File version from fixed numeric fields
        /// </summary>
        public Version FileVersion => new(Fixed.FileVersionMS.HIWORD(), Fixed.FileVersionMS.LOWORD(), Fixed.FileVersionLS.HIWORD(), Fixed.FileVersionLS.LOWORD());

        /// <summary>
        /// Product version from fixed numeric fields
        /// </summary>
        public Version ProductVersion => new(Fixed.ProductVersionMS.HIWORD(), Fixed.ProductVersionMS.LOWORD(), Fixed.ProductVersionLS.HIWORD(), Fixed.ProductVersionLS.LOWORD());

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

                if (filetime > 0)
                {
                    return DateTime.FromFileTime(filetime);
                }

                return null;
            }
        }

        /// <summary>
        /// Parses raw file data into a NativeFileVersion structure
        /// </summary>
        /// <param name="rawFile">Raw exe or dll file data with a version resource</param>
        public static unsafe NativeFileVersion GetNativeFileVersion(byte[] rawFile)
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
                return new(file_data);
            }
        }

        /// <summary>
        /// Parses raw or mapped file data into a NativeFileVersion structure
        /// </summary>
        /// <param name="fileData">Raw or mapped exe or dll file data with a version resource</param>
        public unsafe NativeFileVersion(byte* fileData)
        {
            var ptr = NativePE.GetRawFileVersionResource(fileData, out _);
            if (ptr == null)
            {
                throw new Win32Exception();
            }

            Fixed = NativePE.GetFixedVersionInfo(ptr);
            if (Fixed.StrucVersion == 0)
            {
                throw new Win32Exception();
            }

            var lpdwTranslationCode = NativePE.QueryValueInt(ptr, "\\VarFileInfo\\Translation");

            DWORD dwTranslationCode;
            if (lpdwTranslationCode.HasValue)
            {
                dwTranslationCode = lpdwTranslationCode.Value;
                char* tcLanguageName = stackalloc char[128];
                if (NativePE.VerLanguageName(dwTranslationCode.LOWORD(), tcLanguageName, 128) != 0)
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
                var fieldvalue = NativePE.QueryValueWithTranslation(ptr, fieldname, dwTranslationCode);

                if (fieldvalue != null)
                {
                    Fields.Add(fieldname, new string(fieldvalue));
                }
            }
        }
    }
}

