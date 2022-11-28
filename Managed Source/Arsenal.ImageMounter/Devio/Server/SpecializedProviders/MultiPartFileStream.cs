// '''' MultiPartFileStream.vb
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.IO.Streams;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.SpecializedProviders;

public class MultiPartFileStream : CombinedSeekStream
{

    public MultiPartFileStream(string[] Imagefiles, FileAccess DiskAccess)
        : this(Imagefiles, DiskAccess, FileShare.Read | FileShare.Delete)
    {

    }

    public MultiPartFileStream(string[] Imagefiles, FileAccess DiskAccess, FileShare ShareMode)
        : base(DiskAccess.HasFlag(FileAccess.Write), OpenImagefiles(Imagefiles, DiskAccess, ShareMode))
    {

    }

    private static FileStream[] OpenImagefiles(string[] Imagefiles, FileAccess DiskAccess, FileShare ShareMode)
    {
        if (Imagefiles is null)
        {
            throw new ArgumentNullException(nameof(Imagefiles));
        }

        if (Imagefiles.Length == 0)
        {
            throw new ArgumentException("No image file names provided.", nameof(Imagefiles));
        }

        FileStream[]? imagestreams = null;

        try
        {
            imagestreams = Array.ConvertAll(Imagefiles, Imagefile =>
            {
                Trace.WriteLine($"Opening image {Imagefile}");
                return new FileStream(Imagefile, FileMode.Open, DiskAccess, ShareMode);
            });

            return imagestreams;
        }
        catch (Exception ex)
        {
            if (imagestreams is not null)
            {
                Array.ForEach(imagestreams, imagestream => imagestream.Close());
            }

            throw new IOException($"Error opening image files '{Imagefiles.FirstOrDefault()}'", ex);
        }
    }

    public MultiPartFileStream(string FirstImagefile, FileAccess DiskAccess)
        : this(ProviderSupport.GetMultiSegmentFiles(FirstImagefile), DiskAccess)
    {
    }

    public MultiPartFileStream(string FirstImagefile, FileAccess DiskAccess, FileShare ShareMode)
        : this(ProviderSupport.GetMultiSegmentFiles(FirstImagefile), DiskAccess, ShareMode)
    {
    }
}