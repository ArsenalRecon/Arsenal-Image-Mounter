//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Microsoft.Win32;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

namespace Arsenal.ImageMounter.Dialogs;

/// <summary>
/// Extension methods for Windows Forms.
/// </summary>
public static class FormsExtensions
{
    /// <summary>
    /// Gets topmost owner window for a window
    /// </summary>
    /// <param name="form">Window where search should start</param>
    /// <returns>Topmost window that owns child windows down to supplied window</returns>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    [return: NotNullIfNotNull(nameof(form))]
#endif
    public static Form? GetTopMostOwner(this Form? form)
    {
        while (form?.Owner is not null)
        {
            form = form.Owner;
        }

        return form;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="RegKey"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public static void SetValueSafe<T>(this RegistryKey RegKey, string name, T value) where T : class
    {
        if (value is null)
        {
            RegKey?.DeleteValue(name, throwOnMissingValue: false);
        }
        else
        {
            RegKey?.SetValue(name, value);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="RegKey"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public static void SetValueSafe<T>(this RegistryKey RegKey, string name, T? value) where T : struct
    {
        if (value is null)
        {
            RegKey?.DeleteValue(name, throwOnMissingValue: false);
        }
        else
        {
            RegKey?.SetValue(name, value);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="RegKey"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <param name="valueKind"></param>
    public static void SetValueSafe<T>(this RegistryKey RegKey, string name, T value, RegistryValueKind valueKind) where T : class
    {
        if (value is null)
        {
            RegKey?.DeleteValue(name, throwOnMissingValue: false);
        }
        else
        {
            RegKey?.SetValue(name, value, valueKind);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="RegKey"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <param name="valueKind"></param>
    public static void SetValueSafe<T>(this RegistryKey RegKey, string name, T? value, RegistryValueKind valueKind) where T : struct
    {
        if (value is null)
        {
            RegKey?.DeleteValue(name, throwOnMissingValue: false);
        }
        else
        {
            RegKey?.SetValue(name, value, valueKind);
        }
    }
}
