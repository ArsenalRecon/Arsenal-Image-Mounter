Imports System.ComponentModel
Imports System.Diagnostics.CodeAnalysis
Imports Arsenal.ImageMounter.PSDisk

Namespace PSDisk

    Public Class DiskStateView
        Implements INotifyPropertyChanged

        Public Sub New()
        End Sub

        Private _DetailsVisible As Boolean
        Private _Selected As Boolean
        Private _ImagePath As String
        Private _DiskSizeNumeric As Long?

        Public Property DeviceProperties As ScsiAdapter.DeviceProperties

        Public Property RawDiskSignature As UInt32?

        Public Property DiskId As Guid?

        Public Property DevicePath As String

        Public Property DeviceName As String

        Public Property StorageDeviceNumber As IO.NativeFileIO.STORAGE_DEVICE_NUMBER?

        Public ReadOnly Property DriveNumberString As String
            Get
                Return _StorageDeviceNumber?.DeviceNumber.ToString()
            End Get
        End Property

        Public ReadOnly Property ScsiId As String
            Get
                If _DeviceProperties IsNot Nothing Then
                    Return _DeviceProperties.DeviceNumber.ToString("X6")
                Else
                    Return "N/A"
                End If
            End Get
        End Property

        Public Property ImagePath As String
            Get
                Return If(_ImagePath, _DeviceProperties?.Filename)
            End Get
            Set
                _ImagePath = Value
            End Set
        End Property

        Public Property NativePropertyDiskOffline As Boolean?

        Public ReadOnly Property IsOffline As Boolean?
            Get
                Return NativePropertyDiskOffline
            End Get
        End Property

        Public ReadOnly Property OfflineString As String
            Get
                Dim state = IsOffline
                If Not state.HasValue Then
                    Return "N/A"
                ElseIf state.Value Then
                    Return "Offline"
                Else
                    Return "Online"
                End If
            End Get
        End Property

        Public Property NativePartitionLayout As IO.NativeFileIO.PARTITION_STYLE?

        Public ReadOnly Property PartitionLayout As String
            Get
                If _NativePartitionLayout.HasValue Then

                    Select Case _NativePartitionLayout.Value
                        Case IO.NativeFileIO.PARTITION_STYLE.PARTITION_STYLE_GPT
                            Return "GPT"
                        Case IO.NativeFileIO.PARTITION_STYLE.PARTITION_STYLE_MBR
                            Return "MBR"
                        Case IO.NativeFileIO.PARTITION_STYLE.PARTITION_STYLE_RAW
                            Return "RAW"
                        Case Else
                            Return "Unknown"
                    End Select
                Else
                    Return "None"
                End If
            End Get
        End Property

        Public ReadOnly Property Signature As String
            Get
                If _DiskId.HasValue Then
                    Return _DiskId.Value.ToString("b")
                ElseIf _RawDiskSignature.HasValue AndAlso (_FakeDiskSignature OrElse _FakeMBR) Then
                    Return $"{_RawDiskSignature:X8} (faked)"
                ElseIf _RawDiskSignature.HasValue Then
                    Return _RawDiskSignature.Value.ToString("X8")
                Else
                    Return "N/A"
                End If
            End Get
        End Property

        Public Property DiskSizeNumeric As Long?
            Get
                If _DiskSizeNumeric.HasValue Then
                    Return _DiskSizeNumeric
                ElseIf _DeviceProperties IsNot Nothing Then
                    Return _DeviceProperties.DiskSize
                Else
                    Return Nothing
                End If
            End Get
            Set
                _DiskSizeNumeric = Value
            End Set
        End Property

        Public ReadOnly Property DiskSize As String
            Get
                Dim size = DiskSizeNumeric
                If Not size.HasValue Then
                    Return Nothing
                End If

                Return API.FormatBytes(size.Value)
            End Get
        End Property

        Public Property NativePropertyDiskReadOnly As Boolean?

        Public Property FakeDiskSignature As Boolean

        Public Property FakeMBR As Boolean

        Public Property Volumes As String()

        Public ReadOnly Property VolumesString As String
            Get
                If _Volumes Is Nothing Then
                    Return Nothing
                End If
                Return String.Join(Environment.NewLine, _Volumes)
            End Get
        End Property

        Public Property MountPoints As String()

        Public ReadOnly Property MountPointsString As String
            Get
                If _MountPoints Is Nothing OrElse _MountPoints.Length = 0 Then
                    Return String.Empty
                End If

                Return String.Join(Environment.NewLine, _MountPoints)
            End Get
        End Property

        Public ReadOnly Property IsReadOnly As Boolean?
            Get
                If _NativePropertyDiskReadOnly.HasValue Then
                    Return _NativePropertyDiskReadOnly.Value
                Else
                    Return _DeviceProperties?.Flags.HasFlag(DeviceFlags.ReadOnly)
                End If
            End Get
        End Property

        Public ReadOnly Property ReadOnlyString As String
            Get
                Dim state = IsReadOnly
                If Not state.HasValue Then
                    Return Nothing
                ElseIf state.Value Then
                    Return "RO"
                Else
                    Return "RW"
                End If
            End Get
        End Property

        Public ReadOnly Property ReadWriteString As String
            Get
                Dim state = IsReadOnly
                If Not state.HasValue Then
                    Return Nothing
                ElseIf state.Value Then
                    Return "Read only"
                Else
                    Return "Read write"
                End If
            End Get
        End Property

        Public Property DetailsVisible As Boolean
            Get
                Return _DetailsVisible
            End Get
            Set
                If Not _DetailsVisible = Value Then
                    _DetailsVisible = Value
                    NotifyPropertyChanged("DetailsVisible")
                    NotifyPropertyChanged("DetailsHidden")
                End If
            End Set
        End Property

        Public Property DetailsHidden As Boolean
            Get
                Return Not _DetailsVisible
            End Get
            Set
                If _DetailsVisible = Value Then
                    _DetailsVisible = Not Value
                    NotifyPropertyChanged("DetailsVisible")
                    NotifyPropertyChanged("DetailsHidden")
                End If
            End Set
        End Property

        Public Property Selected As Boolean
            Get
                Return _Selected
            End Get
            Set
                If Not _Selected = Value Then
                    _Selected = Value
                    NotifyPropertyChanged("Selected")
                End If
            End Set
        End Property

        Private Sub NotifyPropertyChanged(ByVal name As String)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(name))
        End Sub

        Public Event PropertyChanged(sender As Object, e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

    End Class

End Namespace
