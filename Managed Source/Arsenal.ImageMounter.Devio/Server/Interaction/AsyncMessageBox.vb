Imports System.Diagnostics.CodeAnalysis
Imports System.Drawing
Imports System.Windows.Forms

<SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")>
Public Class AsyncMessageBox

    Private Shared ReadOnly MaxFont As New Font("Tahoma", 96)
    Private m_ForegroundBrush As Brush
    Private m_BackgroundBrush As Brush

    Private TextRectangle As RectangleF

    Public Sub New(message As String)
        Me.New()

        MsgText = message
        MyBase.Show()
        MyBase.Activate()

    End Sub

    Public Sub New(owner As IWin32Window, message As String)
        Me.New()

        MsgText = message
        MyBase.Show(owner)
        MyBase.Activate()

    End Sub

    Public Property MsgText() As String
        <DebuggerHidden()>
        Get
            Return m_Text
        End Get
        Set
            m_Text = Value
            If m_CurrentFont IsNot Nothing Then
                m_CurrentFont.Dispose()
                m_CurrentFont = Nothing
            End If
            OnResize(EventArgs.Empty)
        End Set
    End Property

    Private m_Text As String
    Private m_CurrentFont As Font
    Private m_Sized As Boolean

    <SuppressMessage("Usage", "CA2245:Do not assign a property to itself.")>
    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)

        If m_Sized = False Then
            Size = New Size(Screen.FromControl(Me).Bounds.Size.Width \ 4, Screen.FromControl(Me).Bounds.Size.Height \ 8)
            Location = New Point((Screen.FromControl(Me).Bounds.Size.Width - Width) \ 2, (Screen.FromControl(Me).Bounds.Size.Height - Height) \ 2)
            m_Sized = True
        End If

        '' Yes, properties assigned to themselves. Just triggers properties changed events.
        ForeColor = ForeColor
        BackColor = BackColor

        OnResize(EventArgs.Empty)

    End Sub

    Public Overloads Sub Show()
        MyBase.Show()
        MyBase.Activate()
        MyBase.Update()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        Try
            MyBase.OnShown(e)

            OnResize(e)

        Catch

        End Try

        Update()
    End Sub

    Private m_ImageBuffer As Image

    Protected Overrides Sub OnResize(e As EventArgs)
        Try
            MyBase.OnResize(e)

            If m_ForegroundBrush Is Nothing OrElse m_BackgroundBrush Is Nothing Then
                Exit Try
            End If

            m_ImageBuffer = New Bitmap(ClientSize.Width, ClientSize.Height)
            TextRectangle = New Rectangle(ClientRectangle.Width \ 12, ClientRectangle.Height \ 8, ClientRectangle.Width * 10 \ 12, ClientRectangle.Height * 6 \ 8)

            Using g = Graphics.FromImage(m_ImageBuffer)

                g.PageUnit = GraphicsUnit.Pixel

                g.FillRectangle(m_BackgroundBrush, New Rectangle(Point.Empty, m_ImageBuffer.Size))

                If m_CurrentFont IsNot Nothing Then
                    m_CurrentFont.Dispose()
                    m_CurrentFont = Nothing
                End If

                If String.IsNullOrEmpty(m_Text) Then
                    Return
                End If

                m_CurrentFont = FindLargestFont(g, MaxFont.FontFamily, MaxFont.Size, MaxFont.Style, MaxFont.Unit, TextRectangle, m_Text)

                g.DrawString(m_Text, m_CurrentFont, m_ForegroundBrush, TextRectangle, sftCentered)

            End Using

        Catch

        End Try
    End Sub

    Private Shared ReadOnly sftCentered As New StringFormat With {.LineAlignment = StringAlignment.Center, .Alignment = StringAlignment.Center}

    Private Shared Function FindLargestFont(Graphics As Graphics,
                                           FontFamily As FontFamily,
                                           MaxFontSize As Single,
                                           FontStyle As FontStyle,
                                           FontUnit As GraphicsUnit,
                                           TextRectangle As RectangleF,
                                           Text As String) As Font

        FindLargestFont = Nothing

        For FontSize = MaxFontSize To 1 Step -2
            FindLargestFont = New Font(FontFamily, FontSize, FontStyle, FontUnit)

            Dim RequiredRectSize = Graphics.MeasureString(Text, FindLargestFont, CInt(TextRectangle.Width))

            If RequiredRectSize.Height < TextRectangle.Height Then
                Exit For
            End If

            FindLargestFont.Dispose()
        Next

    End Function

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        Try
            If m_ImageBuffer Is Nothing Then
                MyBase.OnPaintBackground(e)
                Return
            End If

            e.Graphics.DrawImage(m_ImageBuffer, Point.Empty)

        Catch

        End Try
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Try
            If m_ImageBuffer Is Nothing Then
                MyBase.OnPaint(e)
                Return
            End If

        Catch

        End Try
    End Sub

    Public Overrides Property ForeColor() As Color
        <DebuggerHidden()>
        Get
            Return MyBase.ForeColor
        End Get
        Set(value As Color)
            If m_ForegroundBrush IsNot Nothing Then
                m_ForegroundBrush.Dispose()
                m_ForegroundBrush = Nothing
            End If
            MyBase.ForeColor = value
            m_ForegroundBrush = New SolidBrush(value)
        End Set
    End Property

    Public Overrides Property BackColor As Color
        Get
            Return MyBase.BackColor
        End Get
        Set(value As Color)
            If m_BackgroundBrush IsNot Nothing Then
                m_BackgroundBrush.Dispose()
                m_BackgroundBrush = Nothing
            End If
            MyBase.BackColor = value
            m_BackgroundBrush = New SolidBrush(value)
        End Set
    End Property

    Public Sub SetProgressMessage(msg As String)
        Invoke(Sub()
                   MsgText = msg
                   Refresh()
               End Sub)
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If e.CloseReason = CloseReason.UserClosing AndAlso
                Modal Then

            e.Cancel = True
            Return
        End If

        MyBase.OnFormClosing(e)
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        MyBase.OnClosed(e)

        m_CurrentFont?.Dispose()
        m_ForegroundBrush?.Dispose()
        m_BackgroundBrush?.Dispose()
        m_ImageBuffer?.Dispose()
    End Sub

End Class