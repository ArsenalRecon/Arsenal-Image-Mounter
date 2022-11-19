using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Reflection;

[ComVisible(false)]
public sealed class SequenceEqualityComparer<T> : IEqualityComparer<IEnumerable<T>>
{

    public IEqualityComparer<T> ItemComparer { get; set; }

    public SequenceEqualityComparer(IEqualityComparer<T> comparer)
    {
        ItemComparer = comparer;
    }

    public SequenceEqualityComparer()
    {
        ItemComparer = EqualityComparer<T>.Default;
    }

    public bool Equals(IEnumerable<T>? x, IEnumerable<T>? y)
        => ReferenceEquals(x, y)
        || (x is not null && y is not null && x.SequenceEqual(y, ItemComparer));

    public int GetHashCode(IEnumerable<T> obj)
    {
        var result = new HashCode();
        foreach (var item in obj)
        {
            result.Add(item, ItemComparer);
        }

        return result.ToHashCode();
    }
}

[ComVisible(false)]
public sealed class SequenceComparer<T> : IComparer<IEnumerable<T>>
{

    public IComparer<T> ItemComparer { get; set; }

    public SequenceComparer(IComparer<T> comparer)
    {
        ItemComparer = comparer;
    }

    public SequenceComparer()
    {
        ItemComparer = Comparer<T>.Default;
    }

    public int Compare(IEnumerable<T>? x, IEnumerable<T>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        var value = 0;
        using var enumx = x.GetEnumerator();
        using var enumy = y.GetEnumerator();
        while (enumx.MoveNext() && enumy.MoveNext())
        {
            value = ItemComparer.Compare(enumx.Current, enumy.Current);
            if (value != 0)
            {
                break;
            }
        }

        return value;
    }
}