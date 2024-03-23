//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using DiscUtils.Streams;
using System.IO;



namespace Arsenal.ImageMounter.IO.Streams;

public static class SeekableBufferedStream
{
    public static int FileDatabufferChunkSize { get; set; } = 32 * 1024 * 1024;

    public static Stream BufferIfNotSeekable(Stream SourceStream, string StreamName)
    {
        if (SourceStream is null)
        {
            return Stream.Null;
        }
        else if (SourceStream.CanSeek)
        {
            return SourceStream;
        }

        using (SourceStream)
        {
            if (SourceStream.Length >= FileDatabufferChunkSize)
            {
                var data = new SparseMemoryBuffer(FileDatabufferChunkSize);

                return data.WriteFromStream(0, SourceStream, SourceStream.Length) < SourceStream.Length
                    ? throw new EndOfStreamException($"Unexpected end of stream '{StreamName}'")
                    : (Stream)new SparseMemoryStream(data, FileAccess.Read);
            }
            else
            {
                var data = new byte[SourceStream.Length];

                return SourceStream.Read(data, 0, data.Length) < SourceStream.Length
                    ? throw new EndOfStreamException($"Unexpected end of stream '{StreamName}'")
                    : (Stream)new MemoryStream(data, writable: false);
            }
        }
    }
}
