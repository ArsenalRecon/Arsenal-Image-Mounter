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
using System.Collections.Generic;
using System.Runtime.Serialization;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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
            // ' Dispose each object in list
            foreach (var value in Values)
            {
                value?.Dispose();
            }
        }

        // ' Clear list
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

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
        => base.GetObjectData(info, context);

    protected DisposableDictionary(SerializationInfo si, StreamingContext context)
        : base(si, context)
    {
    }
}