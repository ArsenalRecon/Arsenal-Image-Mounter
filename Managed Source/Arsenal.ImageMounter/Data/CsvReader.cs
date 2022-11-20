using Arsenal.ImageMounter.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Data;

public abstract class CsvReader : MarshalByRefObject, IEnumerable, IEnumerator, IDisposable
{

    protected readonly char[] delimiters = { ',' };

    protected readonly char[] textQuotes = { '"' };

    public TextReader BaseReader { get; private set; }

    public ReadOnlyCollection<char> Delimiters => new(delimiters);

    public ReadOnlyCollection<char> TextQuotes => new(textQuotes);

    protected abstract object? IEnumerator_Current { get; }

    object? IEnumerator.Current => IEnumerator_Current;

    protected CsvReader(TextReader Reader, char[]? Delimiters, char[]? TextQuotes)
    {

        if (Delimiters is not null)
        {
            delimiters = (char[])Delimiters.Clone();
        }

        if (TextQuotes is not null)
        {
            textQuotes = (char[])TextQuotes.Clone();
        }

        BaseReader = Reader;

    }

    public static CsvReader<T> Create<T>(string FilePath) where T : class, new() => new(FilePath);

    public static CsvReader<T> Create<T>(string FilePath, params char[] delimiters) where T : class, new() => new(FilePath, delimiters);

    public static CsvReader<T> Create<T>(string FilePath, char[] delimiters, char[] textquotes) where T : class, new() => new(FilePath, delimiters, textquotes);

    public static CsvReader<T> Create<T>(TextReader Reader) where T : class, new() => new(Reader);

    public static CsvReader<T> Create<T>(TextReader Reader, char[] delimiters) where T : class, new() => new(Reader, delimiters);

    public static CsvReader<T> Create<T>(TextReader Reader, char[] delimiters, char[] textquotes) where T : class, new() => new(Reader, delimiters, textquotes);

    #region IDisposable Support
    private bool disposedValue; // To detect redundant calls

    // IDisposable
    protected virtual void Dispose(bool disposing)
    {

        if (!disposedValue)
        {

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                BaseReader?.Dispose();

            }

            // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

            // TODO: set large fields to null.

        }

        disposedValue = true;

    }

    // TODO: override Finalize() only if Dispose( disposing As Boolean) above has code to free unmanaged resources.
    ~CsvReader()
    {
        // Do not change this code.  Put cleanup code in Dispose( disposing As Boolean) above.
        Dispose(false);
    }

    // This code added by Visual Basic to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual IEnumerator GetEnumerator() => this;

    public abstract bool MoveNext();

    protected virtual void Reset() => throw new NotImplementedException();

    void IEnumerator.Reset() => Reset();
    #endregion

}

[ComVisible(false)]
public class CsvReader<T> : CsvReader, IEnumerable<T?>, IEnumerator<T?> where T : class, new()
{

    protected readonly Action<T, string>?[] properties;

    public CsvReader(string FilePath)
        : this(File.OpenText(FilePath))
    {

    }

    public CsvReader(string FilePath, char[] delimiters)
        : this(File.OpenText(FilePath), delimiters)
    {

    }

    public CsvReader(string FilePath, char[] delimiters, char[] textquotes)
        : this(File.OpenText(FilePath), delimiters, textquotes)
    {

    }

    public CsvReader(TextReader Reader)
        : this(Reader, null, null)
    {

    }

    public CsvReader(TextReader Reader, char[] delimiters)
        : this(Reader, delimiters, null)
    {

    }

    public CsvReader(TextReader Reader, char[]? delimiters, char[]? textquotes)
        : base(Reader, delimiters, textquotes)
    {

        var line = BaseReader.ReadLine();

        var field_names = line?.Split(base.delimiters, StringSplitOptions.None) ?? Array.Empty<string>();

        properties = Array.ConvertAll(field_names, MembersStringSetter.GenerateReferenceTypeMemberSetter<T>);

    }

    protected sealed override object? IEnumerator_Current => Current;

    public virtual T? Current { get; protected set; }

    public override bool MoveNext()
    {

        var line = BaseReader.ReadLine();

        if (line is null)
        {
            Current = null;
            return false;
        }

        if (line.Length == 0)
        {
            Current = null;
            return true;
        }

        var fields = new List<string>(properties.Length);
        var startIdx = 0;

        while (startIdx < line.Length)
        {

            var scanIdx = startIdx;
            if (Array.IndexOf(textQuotes, line[scanIdx]) >= 0 && scanIdx + 1 < line.Length)
            {

                scanIdx = line.IndexOfAny(textQuotes, scanIdx + 1);

                if (scanIdx < 0)
                {
                    scanIdx = startIdx;
                }
            }

            var i = line.IndexOfAny(delimiters, scanIdx);

            if (i < 0)
            {

                fields.Add(line.AsSpan(startIdx).Trim(textQuotes).ToString());
                break;

            }

            fields.Add(line.AsSpan(startIdx, i - startIdx).Trim(textQuotes).ToString());
            startIdx = i + 1;

        }

        var obj = new T();

        for (int i = 0, loopTo = Math.Min(fields.Count - 1, properties.GetUpperBound(0)); i <= loopTo; i++)
        {

            if (properties[i] is null)
            {
                continue;
            }

            properties[i]?.Invoke(obj, fields[i]);

        }

        Current = obj;
        return true;

    }

    public new IEnumerator<T?> GetEnumerator() => this;

}