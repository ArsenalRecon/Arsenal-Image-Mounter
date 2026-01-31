//  
//  Copyright (c) 2012-2026, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using LTRData.Extensions.Formatting;
using System;
using System.ComponentModel;



namespace Arsenal.ImageMounter.Views;

public class DiskStateView : INotifyPropertyChanged
{
    public DiskStateView()
    {
    }

    private bool detailsVisible;
    private bool selected;
    private string? imagePath;
    private long? diskSizeNumeric;

    public DeviceProperties? DeviceProperties { get; set; }

    public uint? RawDiskSignature { get; set; }

    public Guid? DiskId { get; set; }

    public string? DevicePath { get; set; }

    public string? DeviceName { get; set; }

    public IO.Native.STORAGE_DEVICE_NUMBER? StorageDeviceNumber { get; set; }

    public string? DriveNumberString => StorageDeviceNumber?.DeviceNumber.ToString();

    public string ScsiId => DeviceProperties?.DeviceNumber.ToString("X6") ?? "N/A";

    public string? ImagePath
    {
        get => imagePath ?? DeviceProperties?.Filename;
        set => imagePath = value;
    }

    public bool? NativePropertyDiskOffline { get; set; }

    public bool? IsOffline => NativePropertyDiskOffline;

    public string OfflineString
    {
        get
        {
            var state = IsOffline;
            
            if (state.HasValue)
            {
                return state.Value ? "Offline" : "Online";
            }
            else
            {
                return "N/A";
            }
        }
    }

    public IO.Native.PARTITION_STYLE? NativePartitionLayout { get; set; }

    public string PartitionLayout
    {
        get
        {
            if (NativePartitionLayout.HasValue)
            {
                return NativePartitionLayout.Value switch
                {
                    IO.Native.PARTITION_STYLE.GPT => "GPT",
                    IO.Native.PARTITION_STYLE.MBR => "MBR",
                    IO.Native.PARTITION_STYLE.RAW => "RAW",
                    _ => "Unknown",
                };
            }
            else
            {
                return "None";
            }
        }
    }

    public string Signature
    {
        get
        {
            if (DiskId.HasValue)
            {
                return DiskId.Value.ToString("b");
            }
            else if (RawDiskSignature.HasValue && (FakeDiskSignature || FakeMBR))
            {
                return $"{RawDiskSignature:X8} (faked)";
            }
            else
            {
                return RawDiskSignature?.ToString("X8") ?? "N/A";
            }
        }
    }

    public long? DiskSizeNumeric
    {
        get
        {
            if (diskSizeNumeric.HasValue)
            {
                return diskSizeNumeric;
            }
            else
            {
                return DeviceProperties?.DiskSize;
            }
        }
        set => diskSizeNumeric = value;
    }

    public string? DiskSize
        => DiskSizeNumeric is { } size
        ? SizeFormatting.FormatBytes(size)
        : null;

    public bool? NativePropertyDiskReadOnly { get; set; }

    public bool FakeDiskSignature { get; set; }

    public bool FakeMBR { get; set; }

    public string[]? Volumes { get; set; }

    public string? VolumesString
        => Volumes is not null
        ? string.Join(Environment.NewLine, Volumes)
        : null;

    public string[]? MountPoints { get; set; }

    public string MountPointsString
        => MountPoints is null || MountPoints.Length == 0 ? string.Empty : string.Join(Environment.NewLine, MountPoints);

    public string MountPointsSequenceString
        => MountPoints is null || MountPoints.Length == 0 ? string.Empty : $"Mount Points: {string.Join(", ", MountPoints)}";

    public bool? IsReadOnly
        => !DeviceProperties?.IsWritable
        ?? NativePropertyDiskReadOnly;

    public string? ReadOnlyString
        => IsReadOnly is { } state
        ? state ? "RO" : "RW"
        : null;

    public string? ReadWriteString
        => IsReadOnly is { } state
        ? state ? "Read only" : "Read write"
        : null;

    public bool DetailsVisible
    {
        get => detailsVisible;
        set
        {
            if (detailsVisible != value)
            {
                detailsVisible = value;
                NotifyPropertyChanged("DetailsVisible");
                NotifyPropertyChanged("DetailsHidden");
            }
        }
    }

    public bool DetailsHidden
    {
        get => !detailsVisible;
        set
        {
            if (detailsVisible == value)
            {
                detailsVisible = !value;
                NotifyPropertyChanged("DetailsVisible");
                NotifyPropertyChanged("DetailsHidden");
            }
        }
    }

    public bool Selected
    {
        get => selected;
        set
        {
            if (selected == value)
            {
                return;
            }

            selected = value;
            NotifyPropertyChanged("Selected");
        }
    }

    private void NotifyPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
