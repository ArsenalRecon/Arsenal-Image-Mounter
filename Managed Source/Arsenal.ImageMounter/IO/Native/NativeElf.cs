using DiscUtils.Streams;
using LTRData.Extensions.Formatting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Arsenal.ImageMounter.IO.Native;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1069 // Enums values should not be duplicated

public enum ElfClass : byte
{
    ELFCLASSNONE = 0,
    ELFCLASS32 = 1,
    ELFCLASS64 = 2
}

/// <summary>
/// ELF e_machine identifiers (architecture types)
/// Source: ELF specification and extensions.
/// </summary>
public enum ElfMachine : ushort
{
    /// <summary>No machine</summary>
    EM_NONE = 0,

    /// <summary>AT&amp;T WE 32100</summary>
    EM_M32 = 1,

    /// <summary>SPARC</summary>
    EM_SPARC = 2,

    /// <summary>Intel 80386</summary>
    EM_386 = 3,

    /// <summary>Motorola 68000</summary>
    EM_68K = 4,

    /// <summary>Motorola 88000</summary>
    EM_88K = 5,

    /// <summary>Intel MCU</summary>
    EM_IAMCU = 6,

    /// <summary>Intel 80860</summary>
    EM_860 = 7,

    /// <summary>MIPS I Architecture</summary>
    EM_MIPS = 8,

    /// <summary>IBM System/370 Processor</summary>
    EM_S370 = 9,

    /// <summary>MIPS RS3000 Little-endian</summary>
    EM_MIPS_RS3_LE = 10,

    // 11–14 Reserved for future use

    /// <summary>Hewlett-Packard PA-RISC</summary>
    EM_PARISC = 15,

    // 16 Reserved for future use

    /// <summary>Fujitsu VPP500</summary>
    EM_VPP500 = 17,

    /// <summary>Enhanced instruction set SPARC</summary>
    EM_SPARC32PLUS = 18,

    /// <summary>Intel 80960</summary>
    EM_960 = 19,

    /// <summary>PowerPC</summary>
    EM_PPC = 20,

    /// <summary>64-bit PowerPC</summary>
    EM_PPC64 = 21,

    /// <summary>IBM System/390 Processor</summary>
    EM_S390 = 22,

    /// <summary>IBM SPU/SPC</summary>
    EM_SPU = 23,

    // 24–35 Reserved for future use

    /// <summary>NEC V800</summary>
    EM_V800 = 36,

    /// <summary>Fujitsu FR20</summary>
    EM_FR20 = 37,

    /// <summary>TRW RH-32</summary>
    EM_RH32 = 38,

    /// <summary>Motorola RCE</summary>
    EM_RCE = 39,

    /// <summary>ARM 32-bit architecture (AARCH32)</summary>
    EM_ARM = 40,

    /// <summary>Digital Alpha</summary>
    EM_ALPHA = 41,

    /// <summary>Hitachi SH</summary>
    EM_SH = 42,

    /// <summary>SPARC Version 9</summary>
    EM_SPARCV9 = 43,

    /// <summary>Siemens TriCore embedded processor</summary>
    EM_TRICORE = 44,

    /// <summary>Argonaut RISC Core</summary>
    EM_ARC = 45,

    /// <summary>Hitachi H8/300</summary>
    EM_H8_300 = 46,

    /// <summary>Hitachi H8/300H</summary>
    EM_H8_300H = 47,

    /// <summary>Hitachi H8S</summary>
    EM_H8S = 48,

    /// <summary>Hitachi H8/500</summary>
    EM_H8_500 = 49,

    /// <summary>Intel IA-64 processor architecture</summary>
    EM_IA_64 = 50,

    /// <summary>Stanford MIPS-X</summary>
    EM_MIPS_X = 51,

    /// <summary>Motorola ColdFire</summary>
    EM_COLDFIRE = 52,

    /// <summary>Motorola M68HC12</summary>
    EM_68HC12 = 53,

    /// <summary>Fujitsu MMA Multimedia Accelerator</summary>
    EM_MMA = 54,

    /// <summary>Siemens PCP</summary>
    EM_PCP = 55,

    /// <summary>Sony nCPU embedded RISC processor</summary>
    EM_NCPU = 56,

    /// <summary>Denso NDR1 microprocessor</summary>
    EM_NDR1 = 57,

    /// <summary>Motorola Star*Core processor</summary>
    EM_STARCORE = 58,

    /// <summary>Toyota ME16 processor</summary>
    EM_ME16 = 59,

    /// <summary>STMicroelectronics ST100 processor</summary>
    EM_ST100 = 60,

    /// <summary>Advanced Logic Corp. TinyJ embedded processor family</summary>
    EM_TINYJ = 61,

    /// <summary>AMD x86-64 architecture</summary>
    EM_X86_64 = 62,

    /// <summary>Sony DSP Processor</summary>
    EM_PDSP = 63,

    /// <summary>Digital Equipment Corp. PDP-10</summary>
    EM_PDP10 = 64,

    /// <summary>Digital Equipment Corp. PDP-11</summary>
    EM_PDP11 = 65,

    /// <summary>Siemens FX66 microcontroller</summary>
    EM_FX66 = 66,

    /// <summary>STMicroelectronics ST9+ 8/16 bit microcontroller</summary>
    EM_ST9PLUS = 67,

    /// <summary>STMicroelectronics ST7 8-bit microcontroller</summary>
    EM_ST7 = 68,

    /// <summary>Motorola MC68HC16 Microcontroller</summary>
    EM_68HC16 = 69,

    /// <summary>Motorola MC68HC11 Microcontroller</summary>
    EM_68HC11 = 70,

    /// <summary>Motorola MC68HC08 Microcontroller</summary>
    EM_68HC08 = 71,

    /// <summary>Motorola MC68HC05 Microcontroller</summary>
    EM_68HC05 = 72,

    /// <summary>Silicon Graphics SVx</summary>
    EM_SVX = 73,

    /// <summary>STMicroelectronics ST19 8-bit microcontroller</summary>
    EM_ST19 = 74,

    /// <summary>Digital VAX</summary>
    EM_VAX = 75,

    /// <summary>Axis Communications 32-bit embedded processor</summary>
    EM_CRIS = 76,

    /// <summary>Infineon Technologies 32-bit embedded processor</summary>
    EM_JAVELIN = 77,

    /// <summary>Element 14 64-bit DSP Processor</summary>
    EM_FIREPATH = 78,

    /// <summary>LSI Logic 16-bit DSP Processor</summary>
    EM_ZSP = 79,

    /// <summary>Donald Knuth’s educational 64-bit processor</summary>
    EM_MMIX = 80,

    /// <summary>Harvard University machine-independent object files</summary>
    EM_HUANY = 81,

    /// <summary>SiTera Prism</summary>
    EM_PRISM = 82,

    /// <summary>Atmel AVR 8-bit microcontroller</summary>
    EM_AVR = 83,

    /// <summary>Fujitsu FR30</summary>
    EM_FR30 = 84,

    /// <summary>Mitsubishi D10V</summary>
    EM_D10V = 85,

    /// <summary>Mitsubishi D30V</summary>
    EM_D30V = 86,

    /// <summary>NEC v850</summary>
    EM_V850 = 87,

    /// <summary>Mitsubishi M32R</summary>
    EM_M32R = 88,

    /// <summary>Matsushita MN10300</summary>
    EM_MN10300 = 89,

    /// <summary>Matsushita MN10200</summary>
    EM_MN10200 = 90,

    /// <summary>picoJava</summary>
    EM_PJ = 91,

    /// <summary>OpenRISC 32-bit embedded processor</summary>
    EM_OPENRISC = 92,

    /// <summary>ARC International ARCompact processor</summary>
    EM_ARC_COMPACT = 93,

    /// <summary>Tensilica Xtensa Architecture</summary>
    EM_XTENSA = 94,

    /// <summary>Alphamosaic VideoCore processor</summary>
    EM_VIDEOCORE = 95,

    /// <summary>Thompson Multimedia General Purpose Processor</summary>
    EM_TMM_GPP = 96,

    /// <summary>National Semiconductor 32000 series</summary>
    EM_NS32K = 97,

    /// <summary>Tenor Network TPC processor</summary>
    EM_TPC = 98,

    /// <summary>Trebia SNP 1000 processor</summary>
    EM_SNP1K = 99,

    /// <summary>STMicroelectronics ST200 microcontroller</summary>
    EM_ST200 = 100,

    /// <summary>Ubicom IP2xxx microcontroller family</summary>
    EM_IP2K = 101,

    /// <summary>MAX Processor</summary>
    EM_MAX = 102,

    /// <summary>National Semiconductor CompactRISC microprocessor</summary>
    EM_CR = 103,

    /// <summary>Fujitsu F2MC16</summary>
    EM_F2MC16 = 104,

    /// <summary>Texas Instruments embedded microcontroller MSP430</summary>
    EM_MSP430 = 105,

    /// <summary>Analog Devices Blackfin (DSP) processor</summary>
    EM_BLACKFIN = 106,

    /// <summary>S1C33 Family of Seiko Epson processors</summary>
    EM_SE_C33 = 107,

    /// <summary>Sharp embedded microprocessor</summary>
    EM_SEP = 108,

    /// <summary>Arca RISC Microprocessor</summary>
    EM_ARCA = 109,

    /// <summary>Microprocessor series from PKU-Unity Ltd.</summary>
    EM_UNICORE = 110,

    /// <summary>eXcess configurable embedded CPU</summary>
    EM_EXCESS = 111,

    /// <summary>Icera Deep Execution Processor</summary>
    EM_DXP = 112,

    /// <summary>Altera Nios II soft-core processor</summary>
    EM_ALTERA_NIOS2 = 113,

    /// <summary>National Semiconductor CompactRISC CRX microprocessor</summary>
    EM_CRX = 114,

    /// <summary>Motorola XGATE embedded processor</summary>
    EM_XGATE = 115,

    /// <summary>Infineon C16x/XC16x processor</summary>
    EM_C166 = 116,

    /// <summary>Renesas M16C series microprocessors</summary>
    EM_M16C = 117,

    /// <summary>Microchip dsPIC30F Digital Signal Controller</summary>
    EM_DSPIC30F = 118,

    /// <summary>Freescale Communication Engine RISC core</summary>
    EM_CE = 119,

    /// <summary>Renesas M32C series microprocessors</summary>
    EM_M32C = 120,

    // 121–130 Reserved

    /// <summary>Altium TSK3000 core</summary>
    EM_TSK3000 = 131,

    /// <summary>Freescale RS08 embedded processor</summary>
    EM_RS08 = 132,

    /// <summary>Analog Devices SHARC family</summary>
    EM_SHARC = 133,

    /// <summary>Cyan Technology eCOG2 microprocessor</summary>
    EM_ECOG2 = 134,

    /// <summary>Sunplus S+core7 RISC processor</summary>
    EM_SCORE7 = 135,

    /// <summary>New Japan Radio 24-bit DSP Processor</summary>
    EM_DSP24 = 136,

    /// <summary>Broadcom VideoCore III processor</summary>
    EM_VIDEOCORE3 = 137,

    /// <summary>Lattice FPGA RISC processor</summary>
    EM_LATTICEMICO32 = 138,

    /// <summary>Seiko Epson C17 family</summary>
    EM_SE_C17 = 139,

    /// <summary>TI TMS320C6000 DSP family</summary>
    EM_TI_C6000 = 140,

    /// <summary>TI TMS320C2000 DSP family</summary>
    EM_TI_C2000 = 141,

    /// <summary>TI TMS320C55x DSP family</summary>
    EM_TI_C5500 = 142,

    /// <summary>TI Application Specific RISC Processor (32-bit fetch)</summary>
    EM_TI_ARP32 = 143,

    /// <summary>TI Programmable Realtime Unit</summary>
    EM_TI_PRU = 144,

    // 145–159 Reserved

    /// <summary>STMicroelectronics 64bit VLIW DSP</summary>
    EM_MMDSP_PLUS = 160,

    /// <summary>Cypress M8C microprocessor</summary>
    EM_CYPRESS_M8C = 161,

    /// <summary>Renesas R32C series microprocessors</summary>
    EM_R32C = 162,

    /// <summary>NXP TriMedia architecture</summary>
    EM_TRIMEDIA = 163,

    /// <summary>Qualcomm DSP6 Processor</summary>
    EM_QDSP6 = 164,

    /// <summary>Intel 8051 and variants</summary>
    EM_8051 = 165,

    /// <summary>STMicroelectronics STxP7x family</summary>
    EM_STXP7X = 166,

    /// <summary>Andes NDS32 embedded RISC processor</summary>
    EM_NDS32 = 167,

    /// <summary>Cyan Technology eCOG1 family</summary>
    EM_ECOG1 = 168,

    /// <summary>Dallas Semiconductor MAXQ30 Core</summary>
    EM_MAXQ30 = 169,

    /// <summary>New Japan Radio 16-bit DSP Processor</summary>
    EM_XIMO16 = 170,

    /// <summary>M2000 Reconfigurable RISC Microprocessor</summary>
    EM_MANIK = 171,

    /// <summary>Cray Inc. NV2 vector architecture</summary>
    EM_CRAYNV2 = 172,

    /// <summary>Renesas RX family</summary>
    EM_RX = 173,

    /// <summary>Imagination META architecture</summary>
    EM_METAG = 174,

    /// <summary>MCST Elbrus architecture</summary>
    EM_MCST_ELBRUS = 175,

    /// <summary>Cyan Technology eCOG16 family</summary>
    EM_ECOG16 = 176,

    /// <summary>National Semiconductor CompactRISC CR16</summary>
    EM_CR16 = 177,

    /// <summary>Freescale Extended Time Processing Unit</summary>
    EM_ETPU = 178,

    /// <summary>Infineon SLE9X core</summary>
    EM_SLE9X = 179,

    /// <summary>Intel L10M</summary>
    EM_L10M = 180,

    /// <summary>Intel K10M</summary>
    EM_K10M = 181,

    // 182 Reserved

    /// <summary>ARM 64-bit architecture (AARCH64)</summary>
    EM_AARCH64 = 183,

    // 184 Reserved for future ARM use

    /// <summary>Atmel 32-bit microprocessor family</summary>
    EM_AVR32 = 185,

    /// <summary>STMicroelectronics STM8 8-bit microcontroller</summary>
    EM_STM8 = 186,

    /// <summary>Tilera TILE64 multicore architecture</summary>
    EM_TILE64 = 187,

    /// <summary>Tilera TILEPro multicore architecture</summary>
    EM_TILEPRO = 188,

    /// <summary>Xilinx MicroBlaze 32-bit RISC core</summary>
    EM_MICROBLAZE = 189,

    /// <summary>NVIDIA CUDA architecture</summary>
    EM_CUDA = 190,

    /// <summary>Tilera TILE-Gx multicore architecture</summary>
    EM_TILEGX = 191,

    /// <summary>CloudShield architecture family</summary>
    EM_CLOUDSHIELD = 192,

    /// <summary>KIPO-KAIST Core-A 1st generation processor</summary>
    EM_COREA_1ST = 193,

    /// <summary>KIPO-KAIST Core-A 2nd generation processor</summary>
    EM_COREA_2ND = 194,

    /// <summary>Synopsys ARCompact V2</summary>
    EM_ARC_COMPACT2 = 195,

    /// <summary>Open8 8-bit RISC soft processor</summary>
    EM_OPEN8 = 196,

    /// <summary>Renesas RL78 family</summary>
    EM_RL78 = 197,

    /// <summary>Broadcom VideoCore V processor</summary>
    EM_VIDEOCORE5 = 198,

    /// <summary>Renesas 78KOR family</summary>
    EM_78KOR = 199,

    /// <summary>Freescale 56800EX DSC</summary>
    EM_56800EX = 200,

    /// <summary>Beyond BA1 CPU architecture</summary>
    EM_BA1 = 201,

    /// <summary>Beyond BA2 CPU architecture</summary>
    EM_BA2 = 202,

    /// <summary>XMOS xCORE processor family</summary>
    EM_XCORE = 203,

    /// <summary>Microchip 8-bit PIC family</summary>
    EM_MCHP_PIC = 204,

    /// <summary>Reserved by Intel</summary>
    EM_INTEL205 = 205,
    EM_INTEL206 = 206,
    EM_INTEL207 = 207,
    EM_INTEL208 = 208,
    EM_INTEL209 = 209,

    /// <summary>KM211 KM32 32-bit processor</summary>
    EM_KM32 = 210,

    /// <summary>KM211 KMX32 32-bit processor</summary>
    EM_KMX32 = 211,

    /// <summary>KM211 KMX16 16-bit processor</summary>
    EM_KMX16 = 212,

    /// <summary>KM211 KMX8 8-bit processor</summary>
    EM_KMX8 = 213,

    /// <summary>KM211 KVARC processor</summary>
    EM_KVARC = 214,

    /// <summary>Paneve CDP architecture family</summary>
    EM_CDP = 215,
}

public enum ElfMachineFlags : uint
{
    EF_NONE = 0
}

/// <summary>
/// Identifies the operating system and ABI extensions to which an ELF file conforms.
/// Corresponds to the e_ident[EI_OSABI] field in the ELF header.
/// </summary>
public enum ElfOsAbi : byte
{
    /// <summary>
    /// No extensions or unspecified system.
    /// </summary>
    ELFOSABI_NONE = 0,

    /// <summary>
    /// Hewlett-Packard HP-UX.
    /// </summary>
    ELFOSABI_HPUX = 1,

    /// <summary>
    /// NetBSD.
    /// </summary>
    ELFOSABI_NETBSD = 2,

    /// <summary>
    /// Linux.
    /// </summary>
    ELFOSABI_LINUX = 3,

    /// <summary>
    /// Sun Solaris.
    /// </summary>
    ELFOSABI_SOLARIS = 6,

    /// <summary>
    /// IBM AIX.
    /// </summary>
    ELFOSABI_AIX = 7,

    /// <summary>
    /// SGI IRIX.
    /// </summary>
    ELFOSABI_IRIX = 8,

    /// <summary>
    /// FreeBSD.
    /// </summary>
    ELFOSABI_FREEBSD = 9,

    /// <summary>
    /// Compaq TRU64 UNIX.
    /// </summary>
    ELFOSABI_TRU64 = 10,

    /// <summary>
    /// Novell Modesto.
    /// </summary>
    ELFOSABI_MODESTO = 11,

    /// <summary>
    /// OpenBSD.
    /// </summary>
    ELFOSABI_OPENBSD = 12,

    /// <summary>
    /// OpenVMS.
    /// </summary>
    ELFOSABI_OPENVMS = 13,

    /// <summary>
    /// Hewlett-Packard Non-Stop Kernel.
    /// </summary>
    ELFOSABI_NSK = 14
}

public enum ElfData : byte
{
    ELFDATANONE = 0,
    ELFDATA2LSB = 1,
    ELFDATA2MSB = 2
}

public enum ElfVersion : uint
{
    EV_NONE = 0,
    EV_CURRENT = 1
}

/// <summary>
/// Identifies the type of an ELF file.
/// Corresponds to the <c>e_type</c> field in the ELF header.
/// </summary>
public enum ElfFileType : ushort
{
    /// <summary>
    /// No file type.
    /// </summary>
    ET_NONE = 0,

    /// <summary>
    /// Relocatable file (object file).
    /// </summary>
    ET_REL = 1,

    /// <summary>
    /// Executable file.
    /// </summary>
    ET_EXEC = 2,

    /// <summary>
    /// Shared object file (shared library).
    /// </summary>
    ET_DYN = 3,

    /// <summary>
    /// Core dump file.
    /// </summary>
    ET_CORE = 4,

    /// <summary>
    /// Operating system-specific range start.
    /// </summary>
    ET_LOOS = 0xFE00,

    /// <summary>
    /// Operating system-specific range end.
    /// </summary>
    ET_HIOS = 0xFEFF,

    /// <summary>
    /// Processor-specific range start.
    /// </summary>
    ET_LOPROC = 0xFF00,

    /// <summary>
    /// Processor-specific range end.
    /// </summary>
    ET_HIPROC = 0xFFFF
}

public unsafe struct ElfUInt16 : IEquatable<ElfUInt16>
{
    private fixed byte value[sizeof(ushort)];

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    private ReadOnlySpan<byte> Span => MemoryMarshal.CreateReadOnlySpan(ref value[0], sizeof(ushort));
#else
    private ReadOnlySpan<byte> Span => new(Unsafe.AsPointer(ref value[0]), sizeof(ushort));
#endif

    public ushort Parse(bool asLittleEndian)
    {
        if (asLittleEndian)
        {
            return EndianUtilities.ToUInt16LittleEndian(Span);
        }
        else
        {
            return EndianUtilities.ToUInt16BigEndian(Span);
        }
    }

    public static ushort Parse(ReadOnlySpan<byte> span, bool asLittleEndian)
    {
        if (asLittleEndian)
        {
            return EndianUtilities.ToUInt16LittleEndian(span);
        }
        else
        {
            return EndianUtilities.ToUInt16BigEndian(span);
        }
    }

    public override bool Equals(object? obj) => obj is ElfUInt16 @int && Equals(@int);
    public unsafe bool Equals(ElfUInt16 other) => Unsafe.As<byte, ushort>(ref value[0]) == Unsafe.As<byte, ushort>(ref other.value[0]);
    public override int GetHashCode() => Unsafe.As<byte, ushort>(ref value[0]);

    public static bool operator ==(ElfUInt16 left, ElfUInt16 right) => left.Equals(right);
    public static bool operator !=(ElfUInt16 left, ElfUInt16 right) => !(left == right);

    public override string ToString() => Span.ToHexString();
}

public unsafe struct ElfUInt32 : IEquatable<ElfUInt32>
{
    private fixed byte value[sizeof(uint)];

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    private ReadOnlySpan<byte> Span => MemoryMarshal.CreateReadOnlySpan(ref value[0], sizeof(uint));
#else
    private ReadOnlySpan<byte> Span => new(Unsafe.AsPointer(ref value[0]), sizeof(uint));
#endif

    public uint Parse(bool asLittleEndian)
    {
        if (asLittleEndian)
        {
            return EndianUtilities.ToUInt32LittleEndian(Span);
        }
        else
        {
            return EndianUtilities.ToUInt32BigEndian(Span);
        }
    }

    public static uint Parse(ReadOnlySpan<byte> span, bool asLittleEndian)
    {
        if (asLittleEndian)
        {
            return EndianUtilities.ToUInt32LittleEndian(span);
        }
        else
        {
            return EndianUtilities.ToUInt32BigEndian(span);
        }
    }

    public override bool Equals(object? obj) => obj is ElfUInt32 @int && Equals(@int);
    public unsafe bool Equals(ElfUInt32 other) => Unsafe.As<byte, uint>(ref value[0]) == Unsafe.As<byte, uint>(ref other.value[0]);
    public override int GetHashCode() => Unsafe.As<byte, int>(ref value[0]);

    public static bool operator ==(ElfUInt32 left, ElfUInt32 right) => left.Equals(right);
    public static bool operator !=(ElfUInt32 left, ElfUInt32 right) => !(left == right);

    public override string ToString() => Span.ToHexString();
}

public unsafe struct ElfUInt64 : IEquatable<ElfUInt64>
{
    private fixed byte value[sizeof(ulong)];

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    private ReadOnlySpan<byte> Span => MemoryMarshal.CreateReadOnlySpan(ref value[0], sizeof(ulong));
#else
    private ReadOnlySpan<byte> Span => new(Unsafe.AsPointer(ref value[0]), sizeof(ulong));
#endif

    public ulong Parse(bool asLittleEndian)
    {
        if (asLittleEndian)
        {
            return EndianUtilities.ToUInt64LittleEndian(Span);
        }
        else
        {
            return EndianUtilities.ToUInt64BigEndian(Span);
        }
    }

    public static ulong Parse(ReadOnlySpan<byte> span, bool asLittleEndian)
    {
        if (asLittleEndian)
        {
            return EndianUtilities.ToUInt64LittleEndian(span);
        }
        else
        {
            return EndianUtilities.ToUInt64BigEndian(span);
        }
    }

    public override bool Equals(object? obj) => obj is ElfUInt64 @int && Equals(@int);
    public unsafe bool Equals(ElfUInt64 other) => Unsafe.As<byte, ulong>(ref value[0]) == Unsafe.As<byte, ulong>(ref other.value[0]);
    public override int GetHashCode() => HashCode.Combine(Unsafe.As<byte, ulong>(ref value[0]));

    public static bool operator ==(ElfUInt64 left, ElfUInt64 right) => left.Equals(right);
    public static bool operator !=(ElfUInt64 left, ElfUInt64 right) => !(left == right);

    public override string ToString() => Span.ToHexString();
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ElfHeaderIdent
{
    private readonly byte magic0;   // 0x7F 'E' 'L' 'F'
    private readonly byte magic1;
    private readonly byte magic2;
    private readonly byte magic3;

    public unsafe bool IsValid =>
        magic0 == 0x7F
        && magic1 == 'E'
        && magic2 == 'L'
        && magic3 == 'F'
        && Class is ElfClass.ELFCLASS32 or ElfClass.ELFCLASS64
        && Version == ElfVersion.EV_CURRENT;

    public readonly ElfClass Class;        // 1=32-bit, 2=64-bit
    public readonly ElfData Data;       // 1=little, 2=big

    private readonly byte version;
    public readonly ElfVersion Version => (ElfVersion)version;

    public readonly ElfOsAbi OsAbi;
    
    public readonly byte AbiVersion;

    private readonly byte pad0;
    private readonly byte pad1;
    private readonly byte pad2;
    private readonly byte pad3;
    private readonly byte pad4;
    private readonly byte pad5;
    private readonly byte pad6;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ElfHeaderRaw
{
    public bool IsValid => Ident.IsValid && Version == ElfVersion.EV_CURRENT;

    public bool IsLittleEndian => Ident.Data == ElfData.ELFDATA2LSB;

    public bool Is64 => Ident.Class == ElfClass.ELFCLASS64;

    public readonly ElfHeaderIdent Ident;

    private readonly ElfUInt16 type;       // 2=executable, 3=shared object

    public unsafe ElfFileType Type => (ElfFileType)type.Parse(IsLittleEndian);

    private readonly ElfUInt16 machine;    // 0x3E=x86-64, 0x28=ARM64

    public unsafe ElfMachine Machine => (ElfMachine)machine.Parse(IsLittleEndian);

    private readonly ElfUInt32 version;

    public unsafe ElfVersion Version => (ElfVersion)version.Parse(IsLittleEndian);
};

public record ElfHeader(
    ElfHeaderIdent Ident,
    ElfFileType Type,
    ElfMachine Machine,
    ElfVersion Version,
    ulong EntryPoint,
    ulong ProgramHeaderOffset,
    ulong SectionHeaderOffset,
    ElfMachineFlags Flags,
    ushort HeaderSize,
    ushort ProgramHeaderEntrySize,
    ushort ProgramHeaderEntryCount,
    ushort SectionHeaderEntrySize,
    ushort SectionHeaderEntryCount,
    ushort SectionHeaderStringTableIndex)
{
    public bool IsValid => Ident.IsValid && Version == ElfVersion.EV_CURRENT;

    public bool IsLittleEndian => Ident.Data == ElfData.ELFDATA2LSB;

    public bool Is64 => Ident.Class == ElfClass.ELFCLASS64;
}

public class ElfReader
{
    public ReadOnlyMemory<byte> FileContents;

    public ElfHeader Header { get; }

    public ImmutableArray<ElfProgramHeader> ProgramHeaders
        => field.IsDefault ? field = ParseProgramHeaders() : field;

    public ElfReader(ReadOnlyMemory<byte> fileContents)
    {
        FileContents = fileContents;

        var raw = MemoryMarshal.Read<ElfHeaderRaw>(fileContents.Span);

        if (!raw.IsValid)
        {
            throw new InvalidDataException("Invalid ELF header");
        }

        if (raw.Is64)
        {
            var elf64 = MemoryMarshal.Read<ElfHeader64>(fileContents.Span);
            Header = elf64.Parse();
        }
        else
        {
            var elf32 = MemoryMarshal.Read<ElfHeader32>(fileContents.Span);
            Header = elf32.Parse();
        }
    }

    /// <summary>
    /// Translates an offset relative to a file loaded in memory for execution
    /// to a physical offset within the file.
    /// </summary>
    /// <param name="va">Address in virtual address space of loaded file</param>
    /// <returns>Address in physical file</returns>
    public long VaToFile(ulong va)
    {
        foreach (var ph in ProgramHeaders)
        {
            var start = ph.Vaddr;
            var end = ph.Vaddr + ph.Memsz;

            if (va >= start && va < end)
            {
                return (long)(ph.Offset + (va - ph.Vaddr));
            }
        }

        return 0;
    }

    const int PT_DYNAMIC = 2;

    public ElfProgramHeader? DynamicHeader => ProgramHeaders
        .Select(ph => (ElfProgramHeader?)ph)
        .SingleOrDefault(ph => ph!.Value.Type == ElfProgramHeaderType.PT_DYNAMIC);

    private ImmutableArray<ElfProgramHeader> ParseProgramHeaders()
    {
        var programHeaders = ImmutableArray.CreateBuilder<ElfProgramHeader>();

        for (ushort i = 0; i < Header.ProgramHeaderEntryCount; i++)
        {
            var entryData = FileContents.Span
                .Slice((int)Header.ProgramHeaderOffset + i * Header.ProgramHeaderEntrySize, Header.ProgramHeaderEntrySize);

            ElfProgramHeader programHeader;

            if (Header.Is64)
            {
                var header = MemoryMarshal.Read<ElfProgramHeader64>(entryData);
                programHeader = header.Parse(Header.IsLittleEndian);
            }
            else
            {
                var header = MemoryMarshal.Read<ElfProgramHeader32>(entryData);
                programHeader = header.Parse(Header.IsLittleEndian);
            }

            programHeaders.Add(programHeader);
        }

        return programHeaders.ToImmutable();
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ElfHeader64
{
    public readonly ElfHeaderRaw Base;

    public readonly ElfUInt64 EntryPoint;
    public readonly ElfUInt64 ProgramHeaderOffset;
    public readonly ElfUInt64 SectionHeaderOffset;
    public readonly ElfUInt32 Flags;
    public readonly ElfUInt16 HeaderSize;
    public readonly ElfUInt16 ProgramHeaderEntrySize;
    public readonly ElfUInt16 ProgramHeaderEntryCount;
    public readonly ElfUInt16 SectionHeaderEntrySize;
    public readonly ElfUInt16 SectionHeaderEntryCount;
    public readonly ElfUInt16 SectionHeaderStringTableIndex;

    public ElfHeader Parse() => new(
        Base.Ident,
        Base.Type,
        Base.Machine,
        Base.Version,
        EntryPoint.Parse(Base.IsLittleEndian),
        ProgramHeaderOffset.Parse(Base.IsLittleEndian),
        SectionHeaderOffset.Parse(Base.IsLittleEndian),
        (ElfMachineFlags)Flags.Parse(Base.IsLittleEndian),
        HeaderSize.Parse(Base.IsLittleEndian),
        ProgramHeaderEntrySize.Parse(Base.IsLittleEndian),
        ProgramHeaderEntryCount.Parse(Base.IsLittleEndian),
        SectionHeaderEntrySize.Parse(Base.IsLittleEndian),
        SectionHeaderEntryCount.Parse(Base.IsLittleEndian),
        SectionHeaderStringTableIndex.Parse(Base.IsLittleEndian));
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ElfHeader32
{
    public readonly ElfHeaderRaw Base;

    public readonly ElfUInt32 EntryPoint;
    public readonly ElfUInt32 ProgramHeaderOffset;
    public readonly ElfUInt32 SectionHeaderOffset;
    public readonly ElfUInt32 Flags;
    public readonly ElfUInt16 HeaderSize;
    public readonly ElfUInt16 ProgramHeaderEntrySize;
    public readonly ElfUInt16 ProgramHeaderEntryCount;
    public readonly ElfUInt16 SectionHeaderEntrySize;
    public readonly ElfUInt16 SectionHeaderEntryCount;
    public readonly ElfUInt16 SectionHeaderStringTableIndex;

    public ElfHeader Parse() => new(
        Base.Ident,
        Base.Type,
        Base.Machine,
        Base.Version,
        EntryPoint.Parse(Base.IsLittleEndian),
        ProgramHeaderOffset.Parse(Base.IsLittleEndian),
        SectionHeaderOffset.Parse(Base.IsLittleEndian),
        (ElfMachineFlags)Flags.Parse(Base.IsLittleEndian),
        HeaderSize.Parse(Base.IsLittleEndian),
        ProgramHeaderEntrySize.Parse(Base.IsLittleEndian),
        ProgramHeaderEntryCount.Parse(Base.IsLittleEndian),
        SectionHeaderEntrySize.Parse(Base.IsLittleEndian),
        SectionHeaderEntryCount.Parse(Base.IsLittleEndian),
        SectionHeaderStringTableIndex.Parse(Base.IsLittleEndian));
};

public readonly record struct ElfProgramHeader(ElfProgramHeaderType Type, ulong Offset, ulong Vaddr, ulong Filesz, ulong Memsz);

/// <summary>
/// Identifies the type of a program segment in an ELF file.
/// Corresponds to the <c>p_type</c> field in the program header table.
/// </summary>
public enum ElfProgramHeaderType : uint
{
    /// <summary>
    /// Unused entry.
    /// </summary>
    PT_NULL = 0,

    /// <summary>
    /// Loadable segment — part of the program image.
    /// </summary>
    PT_LOAD = 1,

    /// <summary>
    /// Dynamic linking information.
    /// </summary>
    PT_DYNAMIC = 2,

    /// <summary>
    /// Interpreter path (usually <c>/lib/ld-linux.so*</c> or similar).
    /// </summary>
    PT_INTERP = 3,

    /// <summary>
    /// Auxiliary information (notes).
    /// </summary>
    PT_NOTE = 4,

    /// <summary>
    /// Reserved but undefined (historically used for shared library).
    /// </summary>
    PT_SHLIB = 5,

    /// <summary>
    /// Program header table itself.
    /// </summary>
    PT_PHDR = 6,

    /// <summary>
    /// Sun-specific: low bound of Sun-specific segment types.
    /// </summary>
    PT_LOSUNW = 0x6FFFFFFA,

    /// <summary>
    /// Sun-specific: uninitialized data segment (BSS).
    /// </summary>
    PT_SUNWBSS = 0x6FFFFFFB,

    /// <summary>
    /// Sun-specific: stack segment (controls stack attributes).
    /// </summary>
    PT_SUNWSTACK = 0x6FFFFFFA,

    /// <summary>
    /// Sun-specific: high bound of Sun-specific segment types.
    /// </summary>
    PT_HISUNW = 0x6FFFFFFF,

    /// <summary>
    /// Processor-specific range start.
    /// </summary>
    PT_LOPROC = 0x70000000,

    /// <summary>
    /// Processor-specific range end.
    /// </summary>
    PT_HIPROC = 0x7FFFFFFF
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ElfProgramHeader64
{
    public readonly ElfUInt32 p_type;
    public readonly ElfUInt32 p_flags;
    public readonly ElfUInt64 p_offset;
    public readonly ElfUInt64 p_vaddr;
    public readonly ElfUInt64 p_paddr;
    public readonly ElfUInt64 p_size;
    public readonly ElfUInt64 p_memsz;
    public readonly ElfUInt64 p_align;

    public ElfProgramHeader Parse(bool isLittleEndian)
    {
        return new(
            (ElfProgramHeaderType)p_type.Parse(isLittleEndian),
            p_offset.Parse(isLittleEndian),
            p_vaddr.Parse(isLittleEndian),
            p_size.Parse(isLittleEndian),
            p_memsz.Parse(isLittleEndian));
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ElfProgramHeader32
{
    public readonly ElfUInt32 p_type;
    public readonly ElfUInt32 p_offset;
    public readonly ElfUInt32 p_vaddr;
    public readonly ElfUInt32 p_paddr;
    public readonly ElfUInt32 p_size;
    public readonly ElfUInt32 p_memsz;
    public readonly ElfUInt32 p_flags;
    public readonly ElfUInt32 p_align;

    public ElfProgramHeader Parse(bool isLittleEndian)
    {
        return new(
            (ElfProgramHeaderType)p_type.Parse(isLittleEndian),
            p_offset.Parse(isLittleEndian),
            p_vaddr.Parse(isLittleEndian),
            p_size.Parse(isLittleEndian),
            p_memsz.Parse(isLittleEndian));
    }
}
