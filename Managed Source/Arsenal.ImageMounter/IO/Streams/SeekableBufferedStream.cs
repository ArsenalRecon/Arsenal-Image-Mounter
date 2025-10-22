//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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

    public static Stream BufferIfNotSeekable(Stream sourceStream)
    {
        if (sourceStream is null)
        {
            return Stream.Null;
        }
        else if (sourceStream.CanSeek)
        {
            return sourceStream;
        }

        return new ProgressiveCachingStream(sourceStream);
    }
}
