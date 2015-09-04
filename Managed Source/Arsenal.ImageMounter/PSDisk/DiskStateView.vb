Imports Arsenal.ImageMounter.PSDisk

Namespace PSDisk

    Public Class DiskStateView
        Implements INotifyPropertyChanged

        Private _DetailsVisible As Boolean
        Private _Selected As Boolean
        Public DiskState As PSDiskParser.DiskState
        Public PhysicalDiskState As PSPhysicalDiskParser.PhysicalDiskState
        Public DeviceProperties As ScsiAdapter.DeviceProperties
        Public DevicePath As String
        Public RawDiskSignature As UInt32?
        Public DriveNumber As UInteger?

        Friend Sub New()

        End Sub

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
                If DiskState IsNot Nothing Then
                    Return DiskState.IsOffline
                ElseIf PhysicalDiskState IsNot Nothing Then
                    If PhysicalDiskState.CanPool OrElse
                        Not PhysicalDiskState.CannotPoolReason.
                        Contains(PSPhysicalDiskParser.CannotPoolReason.Offline) Then

                        Return False
                    Else
                        Return True
                    End If
                ElseIf NativePropertyDiskOffline IsNot Nothing Then
                    Return NativePropertyDiskOffline.Value
                Else
                    Return Nothing
                End If
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

        Public NativePartitionLayout As PSDiskParser.PartitionStyle?

        Public ReadOnly Property PartitionLayout As PSDiskParser.PartitionStyle?
            Get
                If DiskState IsNot Nothing AndAlso DiskState.PartitionStyle.HasValue Then
                    Return DiskState.PartitionStyle
                ElseIf NativePartitionLayout.HasValue Then
                    Return NativePartitionLayout
                Else
                    Return Nothing
                End If
            End Get
        End Property

        Public ReadOnly Property DriveNumberString As String
            Get
                If DriveNumber.HasValue Then
                    Return DriveNumber.ToString()
                Else
                    Return Nothing
                End If
            End Get
        End Property

        Public ReadOnly Property Signature As String
            Get
                If DiskState IsNot Nothing AndAlso DiskState.Signature.HasValue Then
                    Return DiskState.Signature.Value.ToString("X8")
                ElseIf RawDiskSignature.HasValue Then
                    Return RawDiskSignature.Value.ToString("X8")
                Else
                    Return Nothing
                End If
            End Get
        End Property

        Public ReadOnly Property DiskSizeNumeric As ULong?
            Get
                If DiskState IsNot Nothing AndAlso DiskState.Size.HasValue Then
                    Return DiskState.Size
                ElseIf PhysicalDiskState IsNot Nothing AndAlso PhysicalDiskState.Size.HasValue Then
                    Return PhysicalDiskState.Size
                ElseIf DeviceProperties IsNot Nothing Then
                    Return Convert.ToUInt64(DeviceProperties.DiskSize)
                Else
                    Return Nothing
                End If
            End Get
        End Property

        Private Shared ReadOnly multipliers As New Dictionary(Of ULong, String) From
            {{1UL << 60, " EB"},
             {1UL << 50, " PB"},
             {1UL << 40, " TB"},
             {1UL << 30, " GB"},
             {1UL << 20, " MB"},
             {1UL << 10, " KB"},
             {0UL, " bytes"}}

        Public ReadOnly Property DiskSize As String
            Get
                Dim size = DiskSizeNumeric
                If Not size.HasValue Then
                    Return Nothing
                End If

                Dim multiplier =
                    Aggregate m In multipliers.Keys
                    Where size.Value >= m
                    Into Max()

                Return (size.Value / multiplier).ToString("0.000") & multipliers(multiplier)

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
                If DiskState IsNot Nothing Then
                    Return DiskState.IsReadOnly
                ElseIf NativePropertyDiskOReadOnly IsNot Nothing Then
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

        Public Property DetailsVisible As Boolean
            Get
                Return _DetailsVisible
            End Get
            Set(value As Boolean)
                If Not _DetailsVisible = value Then
                    _DetailsVisible = value
                    NotifyPropertyChanged("DetailsVisible")
                End If
            End Set
        End Property

        Public Property Selected As Boolean
            Get
                Return _Selected
            End Get
            Set(value As Boolean)
                If Not _Selected = value Then
                    _Selected = value
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
