﻿//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;



namespace Arsenal.ImageMounter.Collections;

/// <summary>
/// A System.Collections.Generic.Dictionary(Of TKey, TValue) extended with IDisposable implementation that disposes each
/// value object in the dictionary when the dictionary is disposed.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
[Serializable]
public class DisposableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDisposable
    where TKey : notnull
    where TValue : IDisposable?
{
    // IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose each object in list
            foreach (var value in Values)
            {
                value?.Dispose();
            }
        }

        // Clear list
        Clear();
    }

    // This code added by Visual Basic to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~DisposableDictionary()
    {
        Dispose(false);
    }

    public DisposableDictionary()
        : base()
    {

    }

    public DisposableDictionary(int capacity)
        : base(capacity)
    {

    }

    public DisposableDictionary(IDictionary<TKey, TValue> dictionary)
        : base(dictionary)
    {

    }

    public DisposableDictionary(IEqualityComparer<TKey> comparer)
        : base(comparer)
    {

    }

    public DisposableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        : base(dictionary, comparer)
    {

    }

    public DisposableDictionary(int capacity, IEqualityComparer<TKey> comparer)
        : base(capacity, comparer)
    {

    }

#if NET8_0 || NET8_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
        => base.GetObjectData(info, context);

#if NET5_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
    protected DisposableDictionary(SerializationInfo si, StreamingContext context)
        : base(si, context)
    {
    }
}