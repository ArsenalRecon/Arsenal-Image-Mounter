using DiscUtils.Streams;
using System.IO;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO;

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

                if (data.WriteFromStream(0, SourceStream, SourceStream.Length) < SourceStream.Length)
                {
                    throw new EndOfStreamException($"Unexpected end of stream '{StreamName}'");
                }

                return new SparseMemoryStream(data, FileAccess.Read);
            }
            else
            {
                var data = new byte[SourceStream.Length];

                if (SourceStream.Read(data, 0, data.Length) < SourceStream.Length)
                {
                    throw new EndOfStreamException($"Unexpected end of stream '{StreamName}'");
                }

                return new MemoryStream(data, writable: false);
            }
        }
    }
}
