//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System;
using System.Windows.Forms;

namespace Arsenal.ImageMounter.Dialogs;

/// <summary>
/// Implements <see cref="IWin32Window"/> using a native window handle
/// </summary>
public readonly struct NativeWindowHandle : IWin32Window
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
