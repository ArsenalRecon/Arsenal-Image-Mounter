﻿using Microsoft.Win32;
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