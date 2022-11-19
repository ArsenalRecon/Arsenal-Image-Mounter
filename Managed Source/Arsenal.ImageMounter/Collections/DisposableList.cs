using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Collections;

/// <summary>
/// A System.Collections.Generic.List(Of T) extended with IDisposable implementation that disposes each
/// object in the list when the list is disposed.
/// </summary>
[ComVisible(false)]
public class DisposableList : DisposableList<IDisposable>
{

    public DisposableList()
        : base()
    {

    }

    public DisposableList(int capacity)
        : base(capacity)
    {

    }

    public DisposableList(IEnumerable<IDisposable> collection)
        : base(collection)
    {

    }
}

/// <summary>
/// A System.Collections.Generic.List(Of T) extended with IDisposable implementation that disposes each
/// object in the list when the list is disposed.
/// </summary>
/// <typeparam name="T">Type of elements in list. Type needs to implement IDisposable interface.</typeparam>
[ComVisible(false)]

public class DisposableList<T> : List<T>, IDisposable where T : IDisposable
{

    // IDisposable
    protected virtual void Dispose(bool disposing)
    {

        if (disposing)
        {
            // ' Dispose each object in list
            foreach (var obj in this)
            {
                obj?.Dispose();
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

    public DisposableList()
        : base()
    {

    }

    public DisposableList(int capacity)
        : base(capacity)
    {

    }

    public DisposableList(IEnumerable<T> collection)
        : base(collection)
    {

    }
}