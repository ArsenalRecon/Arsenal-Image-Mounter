﻿//  DevioServiceFactory.vb
//  Support routines for creating provider and service instances given a known
//  proxy provider.
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Client;
using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Devio.Server.Services;
using Arsenal.ImageMounter.Devio.Server.SpecializedProviders;
using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using DiscUtils;
using DiscUtils.Streams;
using LTRData.Extensions.Buffers;
using LTRData.Extensions.Formatting;
using LTRData.Extensions.IO;
using System;
using System.Collections.Generic;
#if NET6_0_OR_GREATER
using System.Collections.Immutable;
using ProviderVirtualDiskAccessDictionary = System.Collections.Immutable.ImmutableDictionary<Arsenal.ImageMounter.Devio.Server.Interaction.DevioServiceFactory.ProviderType, System.Collections.Generic.IReadOnlyCollection<Arsenal.ImageMounter.Devio.Server.Interaction.DevioServiceFactory.VirtualDiskAccess>>;
#else
using ProviderVirtualDiskAccessDictionary = System.Collections.ObjectModel.ReadOnlyDictionary<Arsenal.ImageMounter.Devio.Server.Interaction.DevioServiceFactory.ProviderType, System.Collections.Generic.IReadOnlyCollection<Arsenal.ImageMounter.Devio.Server.Interaction.DevioServiceFactory.VirtualDiskAccess>>;
#endif
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable IDE0305 // Simplify collection initialization

namespace Arsenal.ImageMounter.Devio.Server.Interaction;

/// <summary>
/// Support routines for creating provider and service instances given a known proxy provider.
/// </summary>
public static class DevioServiceFactory
{
    /// <summary>
    /// Supported proxy types.
    /// </summary>
    public enum ProviderType
    {
        None,
        LibEwf,
        DiscUtils,
        MultiPartRaw,
        LibAFF4,
        LibQcow
    }

    /// <summary>
    /// Virtual disk access modes. A list of supported modes for a particular ProviderType
    /// is obtained by calling GetSupportedVirtualDiskAccess().
    /// </summary>
    public enum VirtualDiskAccess
    {
        ReadOnly = 1,
        ReadWriteOriginal = 3,
        ReadWriteOverlay = 7,
        ReadOnlyFileSystem = 9,
        ReadWriteFileSystem = 11
    }

    private static readonly ProviderVirtualDiskAccessDictionary SupportedVirtualDiskAccess
        = new Dictionary<ProviderType, IReadOnlyCollection<VirtualDiskAccess>>()
        {
            { ProviderType.None, new[]
            {
                VirtualDiskAccess.ReadOnly,
                VirtualDiskAccess.ReadWriteOriginal,
                VirtualDiskAccess.ReadOnlyFileSystem,
                VirtualDiskAccess.ReadWriteFileSystem
            }},
            { ProviderType.MultiPartRaw, new[]
            {
                VirtualDiskAccess.ReadOnly,
                VirtualDiskAccess.ReadWriteOriginal,
                VirtualDiskAccess.ReadOnlyFileSystem,
                VirtualDiskAccess.ReadWriteFileSystem
            }},
            { ProviderType.DiscUtils, new[]
            {
                VirtualDiskAccess.ReadOnly,
                VirtualDiskAccess.ReadWriteOriginal,
                VirtualDiskAccess.ReadWriteOverlay,
                VirtualDiskAccess.ReadOnlyFileSystem,
                VirtualDiskAccess.ReadWriteFileSystem
            }},
            { ProviderType.LibEwf, new[]
            {
                VirtualDiskAccess.ReadOnly,
                VirtualDiskAccess.ReadWriteOverlay,
                VirtualDiskAccess.ReadOnlyFileSystem
            }},
            { ProviderType.LibAFF4, new[]
            {
                VirtualDiskAccess.ReadOnly,
                VirtualDiskAccess.ReadOnlyFileSystem
            }},
            { ProviderType.LibQcow, new[]
            {
                VirtualDiskAccess.ReadOnly,
                VirtualDiskAccess.ReadOnlyFileSystem
            }}
        }
#if NET6_0_OR_GREATER
        .ToImmutableDictionary();
#else
        .AsReadOnly();
#endif

    private static readonly string[] NotSupportedFormatsForWriteOverlay = [".vdi", ".xva"];

    /// <summary>
    /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file. Once that is done, this method
    /// automatically calls Arsenal Image Mounter to create a virtual disk device for this
    /// image file.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="Adapter">Open ScsiAdapter object for communication with Arsenal Image Mounter.</param>
    /// <param name="Flags">Additional flags to pass to ScsiAdapter.CreateDevice(). For example,
    /// this could specify a flag for read-only mounting.</param>
    /// <param name="DiskAccess"></param>
    /// <param name="ProviderType">One of known image libraries that can handle specified image file.</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static DevioServiceBase AutoMount(string Imagefile, ScsiAdapter Adapter, ProviderType ProviderType, DeviceFlags Flags, VirtualDiskAccess DiskAccess)
    {
        if (Imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)
            || Imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase)
            || Imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            Flags |= DeviceFlags.DeviceTypeCD;
        }

        var Service = GetService(Imagefile, DiskAccess, ProviderType);

        Service.StartServiceThreadAndMount(Adapter, Flags);

        return Service;
    }

    /// <summary>
    /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file. Once that is done, this method
    /// automatically calls Arsenal Image Mounter to create a virtual disk device for this
    /// image file.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="Adapter">Open ScsiAdapter object for communication with Arsenal Image Mounter.</param>
    /// <param name="Flags">Additional flags to pass to ScsiAdapter.CreateDevice(). For example,
    /// this could specify a flag for read-only mounting.</param>
    /// <param name="ProviderType">One of known image libraries that can handle specified image file.</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static DevioServiceBase AutoMount(string Imagefile, ScsiAdapter Adapter, ProviderType ProviderType, DeviceFlags Flags)
    {
        FileAccess DiskAccess;

        if (!Flags.HasFlag(DeviceFlags.ReadOnly))
        {
            DiskAccess = FileAccess.ReadWrite;
        }
        else
        {
            DiskAccess = FileAccess.Read;
        }

        if (Imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)
            || Imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase)
            || Imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            Flags |= DeviceFlags.DeviceTypeCD;
        }

        var Service = GetService(Imagefile, DiskAccess, ProviderType);

        Service.StartServiceThreadAndMount(Adapter, Flags);

        return Service;
    }

    public static IReadOnlyCollection<VirtualDiskAccess> GetSupportedVirtualDiskAccess(ProviderType providerType, string imagePath)
    {
        if (!SupportedVirtualDiskAccess.TryGetValue(providerType, out var supportedVirtualDiskAccess))
        {
            throw new ArgumentException($"Provider type not supported: {providerType}", nameof(providerType));
        }

        if (providerType == ProviderType.DiscUtils
            && NotSupportedFormatsForWriteOverlay.Contains(Path.GetExtension(imagePath), StringComparer.OrdinalIgnoreCase))
        {
            supportedVirtualDiskAccess = supportedVirtualDiskAccess
                .Where(acc => acc != VirtualDiskAccess.ReadWriteOverlay)
                .ToList();
        }

        if (File.GetAttributes(imagePath).HasFlag(FileAttributes.ReadOnly))
        {
            supportedVirtualDiskAccess = supportedVirtualDiskAccess
                .Where(acc => acc is not VirtualDiskAccess.ReadWriteFileSystem and not VirtualDiskAccess.ReadWriteOriginal)
                .ToList();
        }

        return supportedVirtualDiskAccess!;
    }

    /// <summary>
    /// Creates an object, of a DiscUtils.VirtualDisk derived class, for any supported image files format.
    /// For image formats not directly supported by DiscUtils.dll, this creates a devio provider first which
    /// then is opened as a DiscUtils.VirtualDisk wrapper object so that DiscUtils virtual disk features can
    /// be used on the image anyway.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    /// <param name="ProviderType">One of known image libraries that can handle specified image file.</param>
    public static VirtualDisk GetDiscUtilsVirtualDisk(string Imagefile, FileAccess DiskAccess, ProviderType ProviderType)
    {
        VirtualDisk virtualdisk;

        switch (ProviderType)
        {
            case ProviderType.DiscUtils:
                if (Imagefile.EndsWith(".ova", StringComparison.OrdinalIgnoreCase))
                {
                    virtualdisk = OpenOVA(Imagefile, DiskAccess);
                }
                else
                {
                    virtualdisk = VirtualDisk.OpenDisk(Imagefile, DiskAccess);
                }

                break;

            default:
                {
                    var provider = GetProvider(Imagefile, DiskAccess, ProviderType)
                        ?? throw new NotSupportedException($"Cannot open '{Imagefile}' with provider {ProviderType}");

                    virtualdisk = GetDiscUtilsVirtualDisk(provider, ownsProvider: true);

                    break;
                }
        }

        return virtualdisk;
    }

    public static VirtualDisk GetDiscUtilsVirtualDisk(IDevioProvider provider, bool ownsProvider)
        => new DiscUtils.Raw.Disk(new DevioDirectStream(provider, ownsProvider),
                                  Ownership.Dispose,
                                  Geometry.FromCapacity(provider.Length, (int)provider.SectorSize));

    /// <summary>
    /// Opens a VMDK image file embedded in an OVA archive.
    /// </summary>
    /// <param name="imagefile">Path to OVA archive file</param>
    /// <param name="diskAccess">Read or read/write access to image file and virtual disk device.</param>
    /// <returns></returns>
    public static VirtualDisk OpenOVA(string imagefile, FileAccess diskAccess)
    {
        if (diskAccess.HasFlag(FileAccess.Write))
        {
            throw new NotSupportedException("Cannot modify OVA files");
        }

        var ova = File.Open(imagefile, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

        try
        {
            var vmdk = DiscUtils.Archives.TarFile.EnumerateFiles(ova).FirstOrDefault(file => file.Name.EndsWith(".vmdk", StringComparison.OrdinalIgnoreCase))
                ?? throw new NotSupportedException($"The OVA file {imagefile} does not contain an embedded vmdk file.");

            var virtual_disk = new DiscUtils.Vmdk.Disk(vmdk.GetStream(), Ownership.Dispose);
            virtual_disk.Disposed += (sender, e) => ova.Dispose();
            return virtual_disk;
        }
        catch (Exception ex)
        {
            ova.Dispose();

            throw new Exception($"Error opening {imagefile}", ex);
        }
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file. This does not create a DevioServiceBase
    /// object that can actually serve incoming requests, it just creates the provider object that can
    /// be used with a later created DevioServiceBase object.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    /// <param name="ProviderType">One of known image libraries that can handle specified image file.</param>
    public static IDevioProvider GetProvider(string Imagefile, FileAccess DiskAccess, ProviderType ProviderType)
        => InstalledProvidersByProxyValueAndFileAccess.TryGetValue(ProviderType, out var GetProviderFunc)
            ? GetProviderFunc(Imagefile, DiskAccess)
            : throw new InvalidOperationException($"Provider '{ProviderType}' not supported.");

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file. This does not create a DevioServiceBase
    /// object that can actually serve incoming requests, it just creates the provider object that can
    /// be used with a later created DevioServiceBase object.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    /// <param name="ProviderType">One of known image libraries that can handle specified image file.</param>
    public static IDevioProvider GetProvider(string Imagefile, VirtualDiskAccess DiskAccess, ProviderType ProviderType)
        => InstalledProvidersByProxyValueAndVirtualDiskAccess.TryGetValue(ProviderType, out var GetProviderFunc)
            ? GetProviderFunc(Imagefile, DiskAccess)
            : throw new InvalidOperationException($"Provider '{ProviderType}' not supported.");

    public static IDevioProvider GetProvider(string Imagefile, FileAccess DiskAccess)
    {
        var provider = GetProviderTypeFromFileName(Imagefile);

        return GetProvider(Imagefile, DiskAccess, provider);
    }

    public static IDevioProvider GetProvider(string imageFile, FileAccess DiskAccess, string ProviderName)
        => InstalledProvidersByNameAndFileAccess.TryGetValue(ProviderName, out var GetProviderFunc)
            ? GetProviderFunc(imageFile, DiskAccess)
            : throw new NotSupportedException($"Provider '{ProviderName}' not supported. Valid values are: {string.Join(", ", InstalledProvidersByNameAndFileAccess.Keys)}.");

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    private static DevioProviderFromStream GetProviderPhysical(uint DeviceNumber, FileAccess DiskAccess)
    {
        var disk = API.OpenDevice(DeviceNumber, DiskAccess);

        return new DevioProviderFromStream(disk.GetRawDiskStream(), ownsStream: true)
        {
            CustomSectorSize = (uint)((disk.Geometry?.BytesPerSector) ?? 512)
        };
    }

    private static DevioProviderFromStream GetProviderPhysical(string DevicePath, FileAccess DiskAccess)
    {
        var disk = new DiskDevice(DevicePath, DiskAccess);

        return new DevioProviderFromStream(disk.GetRawDiskStream(), ownsStream: true)
        {
            CustomSectorSize = (uint)((disk.Geometry?.BytesPerSector) ?? 512)
        };
    }

    private static DevioProviderFromStream GetProviderRaw(string Imagefile, VirtualDiskAccess DiskAccess)
        => GetProviderRaw(Imagefile, GetDirectFileAccessFlags(DiskAccess));

    private static DevioProviderFromStream GetProviderRaw(string Imagefile, FileAccess DiskAccess)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && uint.TryParse(Imagefile, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out var device_number))
        {
            return GetProviderPhysical(device_number, DiskAccess);
        }
        else if (Imagefile.StartsWith("/dev/", StringComparison.Ordinal)
            || (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && (Imagefile.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
            || Imagefile.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
            && !NativeStruct.HasExtension(Imagefile)
            && (!NativeFileIO.TryGetFileAttributes(Imagefile, out var attributes)
            || attributes.HasFlag(FileAttributes.Directory))))
        {
            return GetProviderPhysical(Imagefile, DiskAccess);
        }

        var stream = new FileStream(Imagefile, FileMode.Open, DiskAccess, FileShare.Read | FileShare.Delete, bufferSize: 1, useAsync: false);

        return new DevioProviderFromStream(stream, ownsStream: true)
        {
            CustomSectorSize = NativeStruct.GetSectorSizeFromFileName(Imagefile)
        };
    }

    public static IReadOnlyDictionary<ProviderType, Func<string, VirtualDiskAccess, IDevioProvider>> InstalledProvidersByProxyValueAndVirtualDiskAccess { get; } =
        new Dictionary<ProviderType, Func<string, VirtualDiskAccess, IDevioProvider>>()
        {
            { ProviderType.DiscUtils, GetProviderDiscUtils },
            { ProviderType.LibEwf, GetProviderLibEwf },
            { ProviderType.LibAFF4, GetProviderLibAFF4 },
            { ProviderType.LibQcow, GetProviderLibQcow },
            { ProviderType.MultiPartRaw, GetProviderMultiPartRaw },
            { ProviderType.None, GetProviderRaw }
        }
#if NET6_0_OR_GREATER
        .ToImmutableDictionary();
#else
        .AsReadOnly();
#endif

    public static IReadOnlyDictionary<ProviderType, Func<string, FileAccess, IDevioProvider>> InstalledProvidersByProxyValueAndFileAccess { get; } =
        new Dictionary<ProviderType, Func<string, FileAccess, IDevioProvider>>()
        {
            { ProviderType.DiscUtils, GetProviderDiscUtils },
            { ProviderType.LibEwf, GetProviderLibEwf },
            { ProviderType.LibAFF4, GetProviderLibAFF4 },
            { ProviderType.LibQcow, GetProviderLibQcow },
            { ProviderType.MultiPartRaw, GetProviderMultiPartRaw },
            { ProviderType.None, GetProviderRaw }
        }
#if NET6_0_OR_GREATER
        .ToImmutableDictionary();
#else
        .AsReadOnly();
#endif

    public static IReadOnlyDictionary<string, Func<string, VirtualDiskAccess, IDevioProvider>> InstalledProvidersByNameAndVirtualDiskAccess { get; } =
        new Dictionary<string, Func<string, VirtualDiskAccess, IDevioProvider>>(StringComparer.OrdinalIgnoreCase)
        {
            { "DiscUtils", GetProviderDiscUtils },
            { "LibEwf", GetProviderLibEwf },
            { "LibAFF4", GetProviderLibAFF4 },
            { "LibQcow", GetProviderLibQcow },
            { "MultiPartRaw", GetProviderMultiPartRaw },
            { "None", GetProviderRaw }
        }
#if NET6_0_OR_GREATER
        .ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
#else
        .AsReadOnly();
#endif

    public static IReadOnlyDictionary<string, Func<string, FileAccess, IDevioProvider>> InstalledProvidersByNameAndFileAccess { get; } =
        new Dictionary<string, Func<string, FileAccess, IDevioProvider>>(StringComparer.OrdinalIgnoreCase)
        {
            { "DiscUtils", GetProviderDiscUtils },
            { "LibEwf", GetProviderLibEwf },
            { "LibAFF4", GetProviderLibAFF4 },
            { "LibQcow", GetProviderLibQcow },
            { "MultiPartRaw", GetProviderMultiPartRaw },
            { "None", GetProviderRaw }
        }
#if NET6_0_OR_GREATER
        .ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
#else
        .AsReadOnly();
#endif

    private static readonly Assembly[] DiscUtilsAssemblies = [
        typeof(DiscUtils.Vmdk.Disk).Assembly,
        typeof(DiscUtils.Vhdx.Disk).Assembly,
        typeof(DiscUtils.Vhd.Disk).Assembly,
        typeof(DiscUtils.Vdi.Disk).Assembly,
        typeof(DiscUtils.Dmg.Disk).Assembly,
        typeof(DiscUtils.Xva.Disk).Assembly,
        typeof(DiscUtils.OpticalDisk.Disc).Assembly,
        typeof(DiscUtils.Raw.Disk).Assembly
    ];

    public static bool DiscUtilsInitialized { get; } = InitializeDiscUtils();

    private static bool InitializeDiscUtils()
    {
        var done = false;

        foreach (var asm in DiscUtilsAssemblies.Distinct())
        {
            Trace.WriteLine($"Registering DiscUtils assembly '{asm.FullName}'...");
            DiscUtils.Setup.SetupHelper.RegisterAssembly(asm);
            done = true;
        }

        return done;
    }

    /// <summary>
    /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    /// <param name="ProviderType">One of known image libraries that can handle specified image file.</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static DevioServiceBase GetService(string Imagefile, VirtualDiskAccess DiskAccess, ProviderType ProviderType)
        => GetService(Imagefile, DiskAccess, ProviderType, FakeMBR: false);

    /// <summary>
    /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    /// <param name="ProviderType">One of known image libraries that can handle specified image file.</param>
    /// <param name="FakeMBR">Controls whether to emulate a complete disk by building a fake MBR </param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static DevioServiceBase GetService(string Imagefile, VirtualDiskAccess DiskAccess, ProviderType ProviderType, bool FakeMBR)
    {
        if (ProviderType == ProviderType.None && !FakeMBR)
        {
            return new DevioNoneService(Imagefile, DiskAccess);
        }
        else if (ProviderType == ProviderType.DiscUtils
            && !FakeMBR
            && DiskAccess is VirtualDiskAccess.ReadOnly or VirtualDiskAccess.ReadWriteOriginal or VirtualDiskAccess.ReadWriteOverlay
            && (Imagefile.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase)
            || Imagefile.EndsWith(".avhd", StringComparison.OrdinalIgnoreCase)))
        {
            return new DevioNoneService($@"\\?\vhdaccess{NativeFileIO.GetNtPath(Imagefile)}", DiskAccess);
        }

        var Provider = GetProvider(Imagefile, DiskAccess, ProviderType)
            ?? throw new NotSupportedException($"Cannot open '{Imagefile}' with provider {ProviderType}");

        if (FakeMBR)
        {
            Provider = new DevioProviderWithFakeMBR(Provider);
        }

        var Service = new DevioDrvService(Provider, ownsProvider: true)
        {
            Description = $"Image file {Imagefile}"
        };

        return Service;
    }

    /// <summary>
    /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    /// <param name="ProviderType">One of known image libraries that can handle specified image file.</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static DevioServiceBase GetService(string Imagefile, FileAccess DiskAccess, ProviderType ProviderType)
    {
        DevioServiceBase Service;

        switch (ProviderType)
        {
            case ProviderType.None:
                if (Imagefile.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase)
                    || Imagefile.EndsWith(".avhd", StringComparison.OrdinalIgnoreCase))
                {
                    return new DevioNoneService($@"\\?\vhdaccess{NativeFileIO.GetNtPath(Imagefile)}", DiskAccess);
                }
                else
                {
                    Service = new DevioNoneService(Imagefile, DiskAccess);
                }

                break;

            default:
                var provider = GetProvider(Imagefile, DiskAccess, ProviderType)
                    ?? throw new NotSupportedException($"Cannot open '{Imagefile}' with provider {ProviderType}");

                Service = new DevioDrvService(provider, ownsProvider: true);

                break;
        }

        Service.Description = $"Image file {Imagefile}";

        return Service;
    }

    public static FileAccess GetDirectFileAccessFlags(VirtualDiskAccess DiskAccess) => ((int)DiskAccess & (int)~FileAccess.ReadWrite) != 0
        ? throw new ArgumentException($"Unsupported VirtualDiskAccess flags For direct file access: {DiskAccess}", nameof(DiskAccess))
        : (FileAccess)DiskAccess;

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using DiscUtils library.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    public static IDevioProvider GetProviderDiscUtils(string Imagefile, FileAccess DiskAccess)
    {
        var VirtualDiskAccess = DiskAccess switch
        {
            FileAccess.Read => DevioServiceFactory.VirtualDiskAccess.ReadOnly,
            FileAccess.ReadWrite => DevioServiceFactory.VirtualDiskAccess.ReadWriteOriginal,
            _ => throw new ArgumentException($"Unsupported DiskAccess for DiscUtils: {DiskAccess}", nameof(DiskAccess)),
        };

        return GetProviderDiscUtils(Imagefile, VirtualDiskAccess);
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using DiscUtils library.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    public static IDevioProvider GetProviderDiscUtils(string Imagefile, VirtualDiskAccess DiskAccess)
    {
        var FileAccess = DiskAccess switch
        {
            VirtualDiskAccess.ReadOnly => System.IO.FileAccess.Read,
            VirtualDiskAccess.ReadWriteOriginal => System.IO.FileAccess.ReadWrite,
            VirtualDiskAccess.ReadWriteOverlay => System.IO.FileAccess.Read,
            _ => throw new ArgumentException($"Unsupported DiskAccess for DiscUtils: {DiskAccess}", nameof(DiskAccess)),
        };
        Trace.WriteLine($"Opening image {Imagefile}");

        var Disk = GetDiscUtilsVirtualDisk(Imagefile, FileAccess, ProviderType.DiscUtils);

        if (Disk is null)
        {
            var fs = new FileStream(Imagefile, FileMode.Open, FileAccess, FileShare.Read | FileShare.Delete, bufferSize: 1, useAsync: true);
            try
            {
                Disk = new DiscUtils.Dmg.Disk(fs, Ownership.Dispose);
            }
            catch
            {
                fs.Dispose();
            }
        }

        if (Disk is null)
        {
            throw new NotSupportedException($@"Image not recognized by DiscUtils.

Formats currently supported: {string.Join(", ", VirtualDiskManager.SupportedDiskTypes)}");
        }

        Trace.WriteLine($"Image type class: {Disk.DiskTypeInfo?.Name} ({Disk.DiskTypeInfo?.Variant})");

        var DisposableObjects = new List<IDisposable> { Disk };

        try
        {
            if (Disk.IsPartitioned)
            {
                Trace.WriteLine($"Partition table class: {Disk.Partitions.GetType()}");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Partition table error: {ex.JoinMessages()}");
        }

        try
        {
            Trace.WriteLine($"Image virtual size is {Disk.Capacity} bytes");

            uint SectorSize;

            if (Disk.Geometry is { } diskGeometry)
            {
                SectorSize = (uint)diskGeometry.BytesPerSector;
                Trace.WriteLine($"Image sector size is {SectorSize} bytes");
            }
            else
            {
                SectorSize = 512;
                Trace.WriteLine("Image sector size is unknown, assuming 512 bytes");
            }

            if (DiskAccess == VirtualDiskAccess.ReadWriteOverlay)
            {
                var DifferencingPath = Path.Combine(Path.GetDirectoryName(Imagefile) ?? "",
                    $"{Path.GetFileNameWithoutExtension(Imagefile)}_aimdiff{Path.GetExtension(Imagefile)}");

                Trace.WriteLine($"Using temporary overlay file '{DifferencingPath}'");

                for (; ; )
                {
                    try
                    {
                        if (File.Exists(DifferencingPath))
                        {
                            if (UseExistingDifferencingDisk(ref DifferencingPath))
                            {
                                Disk = VirtualDisk.OpenDisk(DifferencingPath, System.IO.FileAccess.ReadWrite);
                                break;
                            }

                            File.Delete(DifferencingPath);
                        }

                        Disk = Disk.CreateDifferencingDisk(DifferencingPath);
                        break;
                    }
                    catch (Exception ex)
                    when (!ex.Enumerate().OfType<OperationCanceledException>().Any()
                        && HandleDifferencingDiskCreationError(ex, ref DifferencingPath))
                    {
                    }
                }

                DisposableObjects.Add(Disk);
            }

            var DiskStream = Disk.Content;
            Trace.WriteLine($"Used size is {DiskStream.Length} bytes");

            if (DiskStream.CanWrite)
            {
                Trace.WriteLine("Read/write mode.");
            }
            else
            {
                Trace.WriteLine("Read-only mode.");
            }

            var provider = new DevioProviderFromStream(DiskStream, ownsStream: true)
            {
                CustomSectorSize = SectorSize
            };

            provider.Disposed += (sender, e)
                => DisposableObjects.ForEach(obj => obj.Dispose());

            return provider;
        }
        catch (Exception ex)
        {
            DisposableObjects.ForEach(obj => obj.Dispose());

            throw new Exception($"Error opening {Imagefile}", ex);
        }
    }

    public class PathExceptionEventArgs : EventArgs
    {
        public Exception? Exception { get; set; }

        public string? Path { get; set; }

        public bool Handled { get; set; }
    }

    public static event EventHandler<PathExceptionEventArgs>? DifferencingDiskCreationError;

    private static bool HandleDifferencingDiskCreationError(Exception ex, ref string differencingPath)
    {
        var e = new PathExceptionEventArgs
        {
            Exception = ex,
            Path = differencingPath
        };

        DifferencingDiskCreationError?.Invoke(null, e);

        differencingPath = e.Path;

        return e.Handled;
    }

    public class PathRequestEventArgs : EventArgs
    {
        public string? Path { get; set; }

        public bool Response { get; set; }
    }

    public static event EventHandler<PathRequestEventArgs>? UseExistingDifferencingDiskUserRequest;

    private static bool UseExistingDifferencingDisk(ref string differencingPath)
    {
        var e = new PathRequestEventArgs
        {
            Path = differencingPath
        };

        UseExistingDifferencingDiskUserRequest?.Invoke(null, e);

        differencingPath = e.Path;

        return e.Response;
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified set of multi-part raw image files.
    /// </summary>
    /// <param name="Imagefile">First part image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    public static IDevioProvider GetProviderMultiPartRaw(string Imagefile, VirtualDiskAccess DiskAccess)
        => GetProviderMultiPartRaw(Imagefile, GetDirectFileAccessFlags(DiskAccess));

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified set of multi-part raw image files.
    /// </summary>
    /// <param name="Imagefile">First part image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    public static IDevioProvider GetProviderMultiPartRaw(string Imagefile, FileAccess DiskAccess)
    {
        var DiskStream = new MultiPartFileStream(Imagefile, DiskAccess);

        return new DevioProviderFromStream(DiskStream, ownsStream: true)
        {
            CustomSectorSize = NativeStruct.GetSectorSizeFromFileName(Imagefile)
        };
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified set of multi-part raw image files.
    /// </summary>
    /// <param name="Imagefile">First part image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    /// <param name="ShareMode"></param>
    public static IDevioProvider GetProviderMultiPartRaw(string Imagefile, FileAccess DiskAccess, FileShare ShareMode)
    {
        var DiskStream = new MultiPartFileStream(Imagefile, DiskAccess, ShareMode);

        return new DevioProviderFromStream(DiskStream, ownsStream: true)
        {
            CustomSectorSize = NativeStruct.GetSectorSizeFromFileName(Imagefile)
        };
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using libewf library.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    public static IDevioProvider GetProviderLibQcow(string Imagefile, VirtualDiskAccess DiskAccess)
    {
        var FileAccess = DiskAccess switch
        {
            VirtualDiskAccess.ReadOnly => System.IO.FileAccess.Read,
            VirtualDiskAccess.ReadWriteOverlay => System.IO.FileAccess.ReadWrite,
            _ => throw new ArgumentException($"Unsupported VirtualDiskAccess for libewf: {DiskAccess}", nameof(DiskAccess)),
        };

        return GetProviderLibQcow(Imagefile, FileAccess);
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using libqcow library.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    public static IDevioProvider GetProviderLibQcow(string Imagefile, FileAccess DiskAccess)
    {
        byte flags = 0;

        if (DiskAccess.HasFlag(FileAccess.Read))
        {
            flags |= DevioProviderLibQcow.AccessFlagsRead;
        }

        if (DiskAccess.HasFlag(FileAccess.Write))
        {
            flags |= DevioProviderLibQcow.AccessFlagsWrite;
        }

        return new DevioProviderLibQcow(Imagefile, flags);
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using libqcow library.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    public static IDevioProvider GetProviderLibEwf(string Imagefile, VirtualDiskAccess DiskAccess)
    {
        var FileAccess = DiskAccess switch
        {
            VirtualDiskAccess.ReadOnly => System.IO.FileAccess.Read,
            VirtualDiskAccess.ReadWriteOverlay => System.IO.FileAccess.ReadWrite,
            _ => throw new ArgumentException($"Unsupported VirtualDiskAccess for libewf: {DiskAccess}", nameof(DiskAccess)),
        };

        return GetProviderLibEwf(Imagefile, FileAccess);
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using libewf library.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
    public static IDevioProvider GetProviderLibEwf(string Imagefile, FileAccess DiskAccess)
    {
        byte Flags = 0;

        if (DiskAccess.HasFlag(FileAccess.Read))
        {
            Flags |= DevioProviderLibEwf.AccessFlagsRead;
        }

        if (DiskAccess.HasFlag(FileAccess.Write))
        {
            Flags |= DevioProviderLibEwf.AccessFlagsWrite;
        }

        return new DevioProviderLibEwf(Imagefile, Flags);
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using libaff4 library.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Only read access to image file supported.</param>
    public static IDevioProvider GetProviderLibAFF4(string Imagefile, VirtualDiskAccess DiskAccess)
    {
        switch (DiskAccess)
        {
            case VirtualDiskAccess.ReadOnly:
                break;

            default:
                throw new IOException("Only read-only mode supported with libaff4");
        }

        return GetProviderLibAFF4(Imagefile, 0);
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using libaff4 library.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    /// <param name="DiskAccess">Only read access supported.</param>
    public static IDevioProvider GetProviderLibAFF4(string Imagefile, FileAccess DiskAccess)
        => DiskAccess.HasFlag(FileAccess.Write)
        ? throw new IOException("Only read-only mode supported with libaff4")
        : GetProviderLibAFF4(Imagefile, 0);

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using libaff4 library.
    /// </summary>
    /// <param name="Imagefile">Image file.</param>
    public static IDevioProvider[] GetProviderLibAFF4(string Imagefile)
    {
        var number_of_images = (int)DevioProviderLibAFF4.getimagecount(Imagefile);

        var providers = new IDevioProvider[number_of_images];

        try
        {
            for (int i = 0, loopTo = number_of_images - 1; i <= loopTo; i++)
            {
                providers[i] = GetProviderLibAFF4(Imagefile, i);
            }
        }
        catch (Exception ex)
        {
            Array.ForEach(providers, p => p?.Dispose());

            throw new Exception("Error in libaff4.dll", ex);
        }

        return providers;
    }

    /// <summary>
    /// Creates an object, of an IDevioProvider implementing class, to support devio proxy server end
    /// for servicing I/O requests to a specified image file using libaff4 library.
    /// </summary>
    /// <param name="containerfile">Container file containing image to mount.</param>
    /// <param name="index">Index of image to mount within container file.</param>
    public static IDevioProvider GetProviderLibAFF4(string containerfile, int index)
        => new DevioProviderLibAFF4(string.Concat(Path.GetFullPath(containerfile), ContainerIndexSeparator, index.ToString()));

    private const string ContainerIndexSeparator = ":::";

    public static VirtualDisk GetDiscUtilsVirtualDisk(string imagepath, FileAccess access)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && (imagepath.StartsWith(@"\\?\", StringComparison.Ordinal)
            || imagepath.StartsWith(@"\\.\", StringComparison.Ordinal))
            && !NativeStruct.HasExtension(imagepath))
        {
            var vdisk = new DiskDevice(imagepath, access);
            var diskstream = vdisk.GetRawDiskStream();
            return new DiscUtils.Raw.Disk(diskstream, Ownership.Dispose);
        }

        var provider = GetProviderTypeFromFileName(imagepath);

        return GetDiscUtilsVirtualDisk(imagepath, access, provider);
    }

    public static Stream OpenImageAsStream(string imageFile)
        => OpenImageAsStream(imageFile, FileAccess.Read);

    public static Stream OpenImageAsStream(string imageFile, FileAccess access)
    {
        switch (Path.GetExtension(imageFile).ToLowerInvariant())
        {
            case ".vhd":
            case ".avhd":
            case ".vdi":
            case ".vmdk":
            case ".vhdx":
            case ".avhdx":
            case ".dmg":
                {
                    if (!DiscUtilsInitialized)
                    {
                        Trace.WriteLine("DiscUtils not available!");
                    }

                    var provider = GetProviderDiscUtils(imageFile, access)
                        ?? throw new NotSupportedException($"Cannot open '{imageFile}' with provider DiscUtils");

                    Trace.WriteLine($"Image '{imageFile}' sector size: {provider.SectorSize}");
                    return new DevioDirectStream(provider, ownsProvider: true);
                }

            case ".000":
                return File.Exists(Path.ChangeExtension(imageFile, ".001"))
                    ? new DevioDirectStream(GetProviderMultiPartRaw(imageFile, access), ownsProvider: true)
                    : new FileStream(imageFile, FileMode.Open, access, FileShare.Read | FileShare.Delete);

            case ".001":
                return File.Exists(Path.ChangeExtension(imageFile, ".002"))
                    ? new DevioDirectStream(GetProviderMultiPartRaw(imageFile, access), ownsProvider: true)
                    : new FileStream(imageFile, FileMode.Open, access, FileShare.Read | FileShare.Delete);

            case ".raw":
            case ".dd":
            case ".img":
            case ".ima":
            case ".iso":
            case ".bin":
            case ".nrg":
                return new FileStream(imageFile, FileMode.Open, access, FileShare.Read | FileShare.Delete);

            case ".e01":
            case ".aff":
            case ".ex01":
            case ".s01":
            case ".lx01":
                {
                    if (!Console.IsErrorRedirected)
                    {
                        DevioProviderLibEwf.SetNotificationFile(ConsoleSupport.GetConsoleOutputDeviceName());
                    }

                    var provider = GetProviderLibEwf(imageFile, access);
                    Console.WriteLine($"Image '{imageFile}' sector size: {provider.SectorSize}");
                    return new DevioDirectStream(provider, ownsProvider: true);
                }

            case ".qcow":
            case ".qcow2":
            case ".qcow2c":
                {
                    if (!Console.IsErrorRedirected)
                    {
                        DevioProviderLibQcow.SetNotificationFile(ConsoleSupport.GetConsoleOutputDeviceName());
                    }

                    var provider = GetProviderLibEwf(imageFile, access);
                    Console.WriteLine($"Image '{imageFile}' sector size: {provider.SectorSize}");
                    return new DevioDirectStream(provider, ownsProvider: true);
                }

            case ".aff4":
                {
                    var provider = GetProviderLibAFF4(imageFile, access);
                    Console.WriteLine($"Image '{imageFile}' sector size: {provider.SectorSize}");
                    return new DevioDirectStream(provider, ownsProvider: true);
                }

            default:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && (imageFile.StartsWith(@"\\?\", StringComparison.Ordinal)
                    || imageFile.StartsWith(@"\\.\", StringComparison.Ordinal)))
                {
                    var disk = new DiskDevice(imageFile, access);
                    var sector_size = (disk.Geometry?.BytesPerSector) ?? 512;
                    Console.WriteLine($"Physical disk '{imageFile}' sector size: {sector_size}");
                    var diskStream = disk.GetRawDiskStream();
                    diskStream.Disposed += (sender, e) => disk.Dispose();
                    return diskStream;
                }
                else
                {
                    Console.WriteLine($"Unknown image file extension '{imageFile}', using raw device data.");
                    return new FileStream(imageFile, FileMode.Open, access, FileShare.Read | FileShare.Delete);
                }
        }
    }

    public static ProviderType GetProviderTypeFromFileName(string imageFile)
        => Path.GetExtension(imageFile).ToLowerInvariant() switch
        {
            ".vmdk" when imageFile.EndsWith("-flat.vmdk", StringComparison.OrdinalIgnoreCase) => ProviderType.None,
            ".vhd" or ".avhd" or ".vdi" or ".vmdk" or ".vhdx" or ".avhdx" or ".dmg" or ".ova" => ProviderType.DiscUtils,
            ".000" when File.Exists(Path.ChangeExtension(imageFile, ".001")) => ProviderType.MultiPartRaw,
            ".001" when File.Exists(Path.ChangeExtension(imageFile, ".002")) => ProviderType.MultiPartRaw,
            ".e01" or ".aff" or ".ex01" or ".s01" or ".lx01" => ProviderType.LibEwf,
            ".qcow" or ".qcow2" or ".qcow2c" => ProviderType.LibQcow,
            ".aff4" => ProviderType.LibAFF4,
            _ => ProviderType.None,
        };
}
