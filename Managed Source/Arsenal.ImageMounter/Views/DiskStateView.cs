using System;
using System.ComponentModel;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Views;

public class DiskStateView : INotifyPropertyChanged
{

    public DiskStateView()
    {
    }

    private bool _DetailsVisible;
    private bool _Selected;
    private string? _ImagePath;
    private long? _DiskSizeNumeric;

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
        get => _ImagePath ?? (DeviceProperties?.Filename);
        set => _ImagePath = value;
    }

    public bool? NativePropertyDiskOffline { get; set; }

    public bool? IsOffline => NativePropertyDiskOffline;

    public string OfflineString
    {
        get
        {
            var state = IsOffline;
            if (!state.HasValue)
            {
                return "N/A";
            }
            else
            {
                return state.Value ? "Offline" : "Online";
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

                switch (NativePartitionLayout.Value)
                {

                    case IO.Native.PARTITION_STYLE.GPT:
                        {
                            return "GPT";
                        }

                    case IO.Native.PARTITION_STYLE.MBR:
                        {
                            return "MBR";
                        }

                    case IO.Native.PARTITION_STYLE.RAW:
                        {
                            return "RAW";
                        }

                    default:
                        {
                            return "Unknown";
                        }
                }
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
            if (_DiskSizeNumeric.HasValue)
            {
                return _DiskSizeNumeric;
            }
            else
            {
                return DeviceProperties is not null ? DeviceProperties.DiskSize : (long?)default;
            }
        }
        set => _DiskSizeNumeric = value;
    }

    public string? DiskSize
    {
        get
        {
            var size = DiskSizeNumeric;
            return !size.HasValue ? null : IO.Native.NativeStruct.FormatBytes(size.Value);
        }
    }

    public bool? NativePropertyDiskReadOnly { get; set; }

    public bool FakeDiskSignature { get; set; }

    public bool FakeMBR { get; set; }

    public string[]? Volumes { get; set; }

    public string? VolumesString => Volumes is null ? null : string.Join(Environment.NewLine, Volumes);

    public string[]? MountPoints { get; set; }

    public string MountPointsString => MountPoints is null || MountPoints.Length == 0 ? string.Empty : string.Join(Environment.NewLine, MountPoints);

    public string MountPointsSequenceString => MountPoints is null || MountPoints.Length == 0 ? string.Empty : $"Mount Points: {string.Join(", ", MountPoints)}";

    public bool? IsReadOnly => NativePropertyDiskReadOnly ?? (DeviceProperties?.Flags.HasFlag(DeviceFlags.ReadOnly));

    public string? ReadOnlyString
    {
        get
        {
            var state = IsReadOnly;
            if (!state.HasValue)
            {
                return null;
            }
            else
            {
                return state.Value ? "RO" : "RW";
            }
        }
    }

    public string? ReadWriteString
    {
        get
        {
            var state = IsReadOnly;
            if (!state.HasValue)
            {
                return null;
            }
            else
            {
                return state.Value ? "Read only" : "Read write";
            }
        }
    }

    public bool DetailsVisible
    {
        get => _DetailsVisible;
        set
        {
            if (!(_DetailsVisible == value))
            {
                _DetailsVisible = value;
                NotifyPropertyChanged("DetailsVisible");
                NotifyPropertyChanged("DetailsHidden");
            }
        }
    }

    public bool DetailsHidden
    {
        get => !_DetailsVisible;
        set
        {
            if (_DetailsVisible == value)
            {
                _DetailsVisible = !value;
                NotifyPropertyChanged("DetailsVisible");
                NotifyPropertyChanged("DetailsHidden");
            }
        }
    }

    public bool Selected
    {
        get => _Selected;
        set
        {
            if (!(_Selected == value))
            {
                _Selected = value;
                NotifyPropertyChanged("Selected");
            }
        }
    }

    private void NotifyPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;

}