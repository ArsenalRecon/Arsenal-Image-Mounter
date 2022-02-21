using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Arsenal.ImageMounter.Dialogs;

/// <summary>
/// Simple InputBox implementation for C#.
/// Based on: https://stackoverflow.com/questions/97097/what-is-the-c-sharp-version-of-vb-nets-inputdialog
/// </summary>
public static class InputBox
{
    /// <summary>
    /// Simple InputBox implementation for C#.
    /// </summary>
    /// <param name="owner">Owning window</param>
    /// <param name="input">Input/output string</param>
    /// <param name="title">Dialog title</param>
    /// <param name="labelText">Text on label in dialog box</param>
    /// <returns>DialogResult indicating which button user clicked</returns>
    public static DialogResult ShowInputDialog(IWin32Window owner, string title, string labelText, ref string input)
    {
        var size = new Size(400, 90);
        using var inputBox = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = size,
            StartPosition = FormStartPosition.CenterParent
        };

        var label = new Label
        {
            Size = new(size.Width - 10, 20),
            Location = new(5, 5),
            Text = labelText
        };
        inputBox.Controls.Add(label);

        var textBox = new TextBox
        {
            Size = new(size.Width - 10, 23),
            Location = new(5, 25),
            Text = input
        };
        inputBox.Controls.Add(textBox);

        var okButton = new Button
        {
            DialogResult = DialogResult.OK,
            Name = "okButton",
            Size = new(75, 23),
            Text = "&OK",
            Location = new(size.Width - 80 - 80, 59)
        };
        inputBox.Controls.Add(okButton);

        var cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Name = "cancelButton",
            Size = new(75, 23),
            Text = "&Cancel",
            Location = new(size.Width - 80, 59)
        };
        inputBox.Controls.Add(cancelButton);

        inputBox.AcceptButton = okButton;
        inputBox.CancelButton = cancelButton;

        var result = inputBox.ShowDialog(owner);
        if (result == DialogResult.OK)
        {
            input = textBox.Text;
        }

        return result;
    }
}
