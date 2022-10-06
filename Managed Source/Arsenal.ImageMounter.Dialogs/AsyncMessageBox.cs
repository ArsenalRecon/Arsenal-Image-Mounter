using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Dialogs;

[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
public partial class AsyncMessageBox : Form
{
    private static readonly Font MaxFont = new("Tahoma", 96f);
    private Brush m_ForegroundBrush;
    private Brush m_BackgroundBrush;

    private RectangleF TextRectangle;

    public AsyncMessageBox()
    {

        // This call is required by the designer.
        InitializeComponent();

        // Add any initialization after the InitializeComponent() call.

    }

    public AsyncMessageBox(string message) : this()
    {
        MsgText = message;
        base.Show();
        Activate();
    }

    public AsyncMessageBox(IWin32Window owner, string message) : this()
    {
        MsgText = message;
        Show(owner);
        Activate();
    }

    public string MsgText
    {
        [DebuggerHidden()]
        get => m_Text;
        set
        {
            m_Text = value;
            if (m_CurrentFont is not null)
            {
                m_CurrentFont.Dispose();
                m_CurrentFont = null;
            }
            OnResize(EventArgs.Empty);
        }
    }

    private string m_Text;
    private Font m_CurrentFont;
    private bool m_Sized;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (m_Sized == false)
        {
            Size = new(Screen.FromControl(this).Bounds.Size.Width / 4, Screen.FromControl(this).Bounds.Size.Height / 8);
            Location = new((Screen.FromControl(this).Bounds.Size.Width - Width) / 2, (Screen.FromControl(this).Bounds.Size.Height - Height) / 2);
            m_Sized = true;
        }

        // ' Yes, properties assigned to themselves. Just triggers properties changed events.

#pragma warning disable CA2245 // Do not assign a property to itself
        ForeColor = ForeColor;
        BackColor = BackColor;
#pragma warning restore CA2245 // Do not assign a property to itself

        OnResize(EventArgs.Empty);
    }

    public new void Show()
    {
        base.Show();
        Activate();
        Update();
    }

    protected override void OnShown(EventArgs e)
    {
        try
        {
            base.OnShown(e);

            OnResize(e);
        }

        catch
        {

        }

        Update();
    }

    private Image m_ImageBuffer;

    protected override void OnResize(EventArgs e)
    {
        do
        {
            try
            {
                base.OnResize(e);

                if (m_ForegroundBrush is null || m_BackgroundBrush is null)
                {
                    break;
                }

                m_ImageBuffer = new Bitmap(ClientSize.Width, ClientSize.Height);
                TextRectangle = new(ClientRectangle.Width / 12, ClientRectangle.Height / 8, ClientRectangle.Width * 10 / 12, ClientRectangle.Height * 6 / 8);

                using var g = Graphics.FromImage(m_ImageBuffer);

                g.PageUnit = GraphicsUnit.Pixel;

                g.FillRectangle(m_BackgroundBrush, new(Point.Empty, m_ImageBuffer.Size));

                if (m_CurrentFont is not null)
                {
                    m_CurrentFont.Dispose();
                    m_CurrentFont = null;
                }

                if (string.IsNullOrEmpty(m_Text))
                {
                    return;
                }

                m_CurrentFont = FindLargestFont(g, MaxFont.FontFamily, MaxFont.Size, MaxFont.Style, MaxFont.Unit, TextRectangle, m_Text);

                g.DrawString(m_Text, m_CurrentFont, m_ForegroundBrush, TextRectangle, sftCentered);
            }
            catch
            {
            }
        }

        while (false);
    }

    private static readonly StringFormat sftCentered = new() { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };

    private static Font FindLargestFont(Graphics Graphics, FontFamily FontFamily, float MaxFontSize, FontStyle FontStyle, GraphicsUnit FontUnit, RectangleF TextRectangle, string Text)
    {
        Font FindLargestFontRet = null;

        for (var FontSize = MaxFontSize; FontSize >= 1f; FontSize += -2)
        {
            FindLargestFontRet = new(FontFamily, FontSize, FontStyle, FontUnit);

            var RequiredRectSize = Graphics.MeasureString(Text, FindLargestFontRet, (int)Math.Round(TextRectangle.Width));

            if (RequiredRectSize.Height < TextRectangle.Height)
            {
                break;
            }

            FindLargestFontRet.Dispose();
        }

        return FindLargestFontRet;

    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        try
        {
            if (m_ImageBuffer is null)
            {
                base.OnPaintBackground(e);
                return;
            }

            e.Graphics.DrawImage(m_ImageBuffer, Point.Empty);
        }

        catch
        {

        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        try
        {
            if (m_ImageBuffer is null)
            {
                base.OnPaint(e);
                return;
            }
        }

        catch
        {

        }
    }

    public override Color ForeColor
    {
        [DebuggerHidden()]
        get => base.ForeColor;
        set
        {
            if (m_ForegroundBrush is not null)
            {
                m_ForegroundBrush.Dispose();
                m_ForegroundBrush = null;
            }
            base.ForeColor = value;
            m_ForegroundBrush = new SolidBrush(value);
        }
    }

    public override Color BackColor
    {
        get => base.BackColor;
        set
        {
            if (m_BackgroundBrush is not null)
            {
                m_BackgroundBrush.Dispose();
                m_BackgroundBrush = null;
            }
            base.BackColor = value;
            m_BackgroundBrush = new SolidBrush(value);
        }
    }

    public void SetProgressMessage(string msg)
    {
        Invoke(new Action(() =>
            {
                MsgText = msg;
                Refresh();
            }));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && Modal)
        {

            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        m_CurrentFont?.Dispose();
        m_ForegroundBrush?.Dispose();
        m_BackgroundBrush?.Dispose();
        m_ImageBuffer?.Dispose();
    }

}