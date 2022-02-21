using System;
using System.Windows.Forms;

namespace Arsenal.ImageMounter.Dialogs;

/// <summary>
/// Implements <see cref="IWin32Window"/> using a native window handle
/// </summary>
public partial struct NativeWindowHandle : IWin32Window
{
    /// <summary>
    /// Native window handle
    /// </summary>
    public IntPtr Handle { get; }

    /// <summary>
    /// Initializes a new instance
    /// </summary>
    /// <param name="handle">Native window handle to use</param>
    public NativeWindowHandle(IntPtr handle)
    {
        Handle = handle;
    }

    /// <summary>
    /// Converts the numeric value of the current native window handle to its equivalent string representation.
    /// </summary>
    /// <returns>The string representation of the value of this instance.</returns>
    public override string ToString() => Handle.ToString();
}
