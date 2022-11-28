using System.IO;
using System.Text;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO.Streams;

/// <summary>
/// Buffered version of the BinaryWriter class. Writes to a MemoryStream internally and flushes
/// writes out contents of MemoryStream when WriteTo() or ToArray() are called.
/// </summary>
public class BufferedBinaryWriter : BinaryWriter
{

    /// <summary>
    /// Creates a new instance of BufferedBinaryWriter.
    /// </summary>
    /// <param name="encoding">Specifies which text encoding to use.</param>
    public BufferedBinaryWriter(Encoding encoding)
        : base(new MemoryStream(), encoding)
    {
    }

    /// <summary>
    /// Creates a new instance of BufferedBinaryWriter using System.Text.Encoding.Unicode text encoding.
    /// </summary>
    public BufferedBinaryWriter()
        : base(new MemoryStream(), Encoding.Unicode)
    {
    }

    /// <summary>
    /// Writes current contents of internal MemoryStream to another stream and resets
    /// this BufferedBinaryWriter to empty state.
    /// </summary>
    /// <param name="stream"></param>
    public void WriteTo(Stream stream)
    {
        Flush();

        var baseStream = (MemoryStream)BaseStream;
        baseStream.WriteTo(stream);
        baseStream.SetLength(0L);
        baseStream.Position = 0L;

        stream.Flush();
    }

    /// <summary>
    /// Extracts current contents of internal MemoryStream to a new byte array and resets
    /// this BufferedBinaryWriter to empty state.
    /// </summary>
    public byte[] ToArray()
    {
        Flush();

        var ms = (MemoryStream)BaseStream;
        var ToArrayRet = ms.ToArray();
        ms.SetLength(0L);
        ms.Position = 0L;

        return ToArrayRet;
    }

    /// <summary>
    /// Clears contents of internal MemoryStream.
    /// </summary>
    public void Clear()
    {
        if (IsDisposed == true)
        {
            return;
        }

        var ms = (MemoryStream)BaseStream;
        ms.SetLength(0L);
        ms.Position = 0L;
    }

    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;

        base.Dispose(disposing);
    }
}