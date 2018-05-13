Imports Arsenal.ImageMounter.PSDisk

Namespace PSDisk

    Public Class DiskStateView
        Implements INotifyPropertyChanged

        Private _DetailsVisible As Boolean
        Private _Selected As Boolean
        Public DeviceProperties As ScsiAdapter.DeviceProperties
        Public RawDiskSignature As UInt32?

        Public Sub New()
        End Sub

        Public Property DevicePath As String

        Public Property DeviceName As String

        Public ReadOnly Property ScsiId As String
            Get
                Return DeviceProperties.DeviceNumber.ToString("X6")
            End Get
        End Property

        Public ReadOnly Property ImagePath As String
            Get
                Return DeviceProperties.Filename
            End Get
        End Property

        Public NativePropertyDiskOffline As Boolean?

        Public ReadOnly Property IsOffline As Boolean?
            Get
                Return NativePropertyDiskOffline
            End Get
        End Property

        Public ReadOnly Property OfflineString As String
            Get
                Dim state = IsOffline
                If Not state.HasValue Then
                    Return Nothing
                ElseIf state.Value Then
                    Return "Offline"
                Else
                    Return "Online"
                End If
            End Get
        End Property

        Public NativePartitionLayout As IO.NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE?

        Public ReadOnly Property PartitionLayout As String
            Get
                Select Case NativePartitionLayout.GetValueOrDefault()
                    Case IO.NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE.PARTITION_STYLE_GPT
                        Return "GPT"
                    Case IO.NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE.PARTITION_STYLE_MBR
                        Return "MBR"
                    Case IO.NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE.PARTITION_STYLE_RAW
                        Return "RAW"
                    Case Else
                        Return "Unknown"
                End Select
            End Get
        End Property

        Public ReadOnly Property Signature As String
            Get
                Return RawDiskSignature?.ToString("X8")
            End Get
        End Property

        Public ReadOnly Property DiskSizeNumeric As ULong?
            Get
                If DeviceProperties IsNot Nothing Then
                    Return Convert.ToUInt64(DeviceProperties.DiskSize)
                Else
                    Return Nothing
                End If
            End Get
        End Property

        Public ReadOnly Property DiskSize As String
            Get
                Dim size = DiskSizeNumeric
                If Not size.HasValue Then
                    Return Nothing
                End If

                Return API.FormatFileSize(size.Value)
            End Get
        End Property

        Public NativePropertyDiskOReadOnly As Boolean?

        Public Property Volumes As String()

        Public ReadOnly Property VolumesString As String
            Get
                Return String.Join(Environment.NewLine, _Volumes)
            End Get
        End Property

        Public Property MountPoints As String()

        Public ReadOnly Property MountPointsString As String
            Get
                Return String.Join(Environment.NewLine, _MountPoints)
            End Get
        End Property

        Public ReadOnly Property IsReadOnly As Boolean?
            Get
                If NativePropertyDiskOReadOnly.HasValue Then
                    Return NativePropertyDiskOReadOnly.Value
                ElseIf DeviceProperties IsNot Nothing Then
                    Return (DeviceProperties.Flags And DeviceFlags.ReadOnly) = DeviceFlags.ReadOnly
                Else
                    Return Nothing
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
