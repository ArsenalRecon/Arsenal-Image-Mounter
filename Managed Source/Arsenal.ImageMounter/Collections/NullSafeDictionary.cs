//  
//  Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Collections;

/// <summary>
/// An extension to Dictionary(Of TKey, TValue) that returns a
/// default item for non-existing keys
/// </summary>
[ComVisible(false)]
public abstract class NullSafeDictionary<TKey, TValue> : IDictionary<TKey, TValue?> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue?> dictionary;

    /// <summary>Gets a value that is returned as item for non-existing
    /// keys in dictionary</summary>
    protected abstract TValue GetDefaultValue(TKey Key);

    protected readonly SemaphoreSlim SyncRoot = new(1);

    /// <summary>
    /// Creates a new NullSafeDictionary object
    /// </summary>
    public NullSafeDictionary()
    {
        dictionary = new();
    }

    /// <summary>
    /// Creates a new NullSafeDictionary object
    /// </summary>
    public NullSafeDictionary(IEqualityComparer<TKey> Comparer)
    {
        dictionary = new(Comparer);
    }

    /// <summary>
    /// Gets or sets the item for a key in dictionary. If no item exists for key, the default
    /// value for this SafeDictionary is returned
    /// </summary>
    /// <param name="key"></param>
    public TValue? this[TKey key]
    {
        get
        {
            SyncRoot.Wait();
            try
            {
                return dictionary.TryGetValue(key, out var ItemRet) ? ItemRet : GetDefaultValue(key);
            }
            finally
            {
                SyncRoot.Release();
            }
        }
        set
        {
            SyncRoot.Wait();
            try
            {
                if (dictionary.ContainsKey(key))
                {
                    dictionary[key] = value;
                }
                else
                {
                    dictionary.Add(key, value);
                }
            }
            finally
            {
                SyncRoot.Release();
            }
        }
    }

    private void ICollection_Add(KeyValuePair<TKey, TValue?> item)
        => ((ICollection<KeyValuePair<TKey, TValue?>>)dictionary).Add(item);

    void ICollection<KeyValuePair<TKey, TValue?>>.Add(KeyValuePair<TKey, TValue?> item)
        => ICollection_Add(item);

    public void Clear() => dictionary.Clear();

    private bool ICollection_Contains(KeyValuePair<TKey, TValue?> item)
        => ((ICollection<KeyValuePair<TKey, TValue?>>)dictionary).Contains(item);

    bool ICollection<KeyValuePair<TKey, TValue?>>.Contains(KeyValuePair<TKey, TValue?> item)
        => ICollection_Contains(item);

    private void ICollection_CopyTo(KeyValuePair<TKey, TValue?>[] array, int arrayIndex)
        => ((ICollection<KeyValuePair<TKey, TValue?>>)dictionary).CopyTo(array, arrayIndex);

    void ICollection<KeyValuePair<TKey, TValue?>>.CopyTo(KeyValuePair<TKey, TValue?>[] array, int arrayIndex)
        => ICollection_CopyTo(array, arrayIndex);

    public int Count => dictionary.Count;

    public bool IsReadOnly => false;

    private bool ICollection_Remove(KeyValuePair<TKey, TValue?> item)
        => ((ICollection<KeyValuePair<TKey, TValue?>>)dictionary).Remove(item);

    bool ICollection<KeyValuePair<TKey, TValue?>>.Remove(KeyValuePair<TKey, TValue?> item)
        => ICollection_Remove(item);

    public void Add(TKey key, TValue? value)
        => dictionary.Add(key, value);

    public bool ContainsKey(TKey key)
        => dictionary.ContainsKey(key);

    public ICollection<TKey> Keys => dictionary.Keys;

    public bool Remove(TKey key)
        => dictionary.Remove(key);

    public bool TryGetValue(TKey key, out TValue? value)
        => dictionary.TryGetValue(key, out value);

    public ICollection<TValue?> Values => dictionary.Values;

    private IEnumerator<KeyValuePair<TKey, TValue?>> ICollection_GetEnumerator()
        => ((ICollection<KeyValuePair<TKey, TValue?>>)dictionary).GetEnumerator();

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
        => ICollection_GetEnumerator();

    public IEnumerator GetEnumerator()
        => dictionary.GetEnumerator();
}

public class NullSafeStringDictionary : NullSafeDictionary<string, string>
{

    public NullSafeStringDictionary()
        : base()
    {
    }

    public NullSafeStringDictionary(IEqualityComparer<string> Comparer)
        : base(Comparer)
    {
    }

    protected override string GetDefaultValue(string Key) => string.Empty;
}