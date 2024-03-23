//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Extensions;
using LTRData.Extensions.Buffers;
using System;
using System.IO;
using System.Text;



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
        baseStream.SetLength(0);
        baseStream.Position = 0;

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
        ms.SetLength(0);
        ms.Position = 0;

        return ToArrayRet;
    }

    /// <summary>
    /// Provides direct access to the byte buffer used by this instance.
    /// Only valid until next Write/Clear/ToArray etc modifying methods.
    /// </summary>
    /// <returns>Span access to byte buffer</returns>
    public Span<byte> AsSpan() => ((MemoryStream)BaseStream).AsSpan();

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
        ms.SetLength(0);
        ms.Position = 0;
    }

    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;

        base.Dispose(disposing);
    }
}