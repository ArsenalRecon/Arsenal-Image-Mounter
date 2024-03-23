//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;

namespace Arsenal.ImageMounter.Dialogs;

[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
public partial class AsyncMessageBox : Form
{
    private static readonly Font MaxFont = new("Tahoma", 96f);
    private Brush? foregroundBrush;
    private Brush? backgroundBrush;

    private RectangleF textRectangle;

    public AsyncMessageBox()
    {

        // This call is required by the designer.
        InitializeComponent();

        // Add any initialization after the InitializeComponent() call.

    }

    public AsyncMessageBox(string message)
        : this()
    {
        MsgText = message;
        base.Show();
        Activate();
    }

    public AsyncMessageBox(IWin32Window owner, string message)
        : this()
    {
        MsgText = message;
        Show(owner);
        Activate();
    }

    public string? MsgText
    {
        get => text;
        set
        {
            text = value;
            if (currentFont is not null)
            {
                currentFont.Dispose();
                currentFont = null;
            }

            OnResize(EventArgs.Empty);
        }
    }

    private string? text;
    private Font? currentFont;
    private bool sized;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (sized == false)
        {
            Size = new(Screen.FromControl(this).Bounds.Size.Width / 4, Screen.FromControl(this).Bounds.Size.Height / 8);
            Location = new((Screen.FromControl(this).Bounds.Size.Width - Width) / 2, (Screen.FromControl(this).Bounds.Size.Height - Height) / 2);
            sized = true;
        }

        // Yes, properties assigned to themselves. Just triggers properties changed events.

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

    private Image? imageBuffer;

    protected override void OnResize(EventArgs e)
    {
        for (; ; )
        {
            try
            {
                base.OnResize(e);

                if (foregroundBrush is null || backgroundBrush is null)
                {
                    break;
                }

                imageBuffer = new Bitmap(ClientSize.Width, ClientSize.Height);
                textRectangle = new(ClientRectangle.Width / 12, ClientRectangle.Height / 8, ClientRectangle.Width * 10 / 12, ClientRectangle.Height * 6 / 8);

                using var g = Graphics.FromImage(imageBuffer);

                g.PageUnit = GraphicsUnit.Pixel;

                g.FillRectangle(backgroundBrush, new(Point.Empty, imageBuffer.Size));

                if (currentFont is not null)
                {
                    currentFont.Dispose();
                    currentFont = null;
                }

                if (text is null || string.IsNullOrEmpty(text))
                {
                    return;
                }

                currentFont = FindLargestFont(g, MaxFont.FontFamily, MaxFont.Size, MaxFont.Style, MaxFont.Unit, textRectangle, text);

                if (currentFont is null)
                {
                    return;
                }

                g.DrawString(text, currentFont, foregroundBrush, textRectangle, SftCentered);

                return;
            }
            catch
            {
            }
        }
    }

    private static readonly StringFormat SftCentered = new()
    {
        LineAlignment = StringAlignment.Center,
        Alignment = StringAlignment.Center
    };

    private static Font? FindLargestFont(Graphics Graphics,
                                         FontFamily FontFamily,
                                         float MaxFontSize,
                                         FontStyle FontStyle,
                                         GraphicsUnit FontUnit,
                                         RectangleF TextRectangle,
                                         string Text)
    {
        Font? FindLargestFontRet = null;

        for (var FontSize = MaxFontSize; FontSize >= 1f; FontSize -= 2)
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
            if (imageBuffer is null)
            {
                base.OnPaintBackground(e);
                return;
            }

            e.Graphics.DrawImage(imageBuffer, Point.Empty);
        }

        catch
        {

        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        try
        {
            if (imageBuffer is null)
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
            if (foregroundBrush is not null)
            {
                foregroundBrush.Dispose();
                foregroundBrush = null;
            }

            base.ForeColor = value;
            foregroundBrush = new SolidBrush(value);
        }
    }

    public override Color BackColor
    {
        get => base.BackColor;
        set
        {
            if (backgroundBrush is not null)
            {
                backgroundBrush.Dispose();
                backgroundBrush = null;
            }

            base.BackColor = value;
            backgroundBrush = new SolidBrush(value);
        }
    }

    public void SetProgressMessage(string msg)
        => Invoke(() =>
        {
            MsgText = msg;
            Refresh();
        });

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

        currentFont?.Dispose();
        foregroundBrush?.Dispose();
        backgroundBrush?.Dispose();
        imageBuffer?.Dispose();
    }
}