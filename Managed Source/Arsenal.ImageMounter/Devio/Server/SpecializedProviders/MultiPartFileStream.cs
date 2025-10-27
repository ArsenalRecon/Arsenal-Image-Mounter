//  MultiPartFileStream.vb
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.IO.Streams;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;



namespace Arsenal.ImageMounter.Devio.Server.SpecializedProviders;

public class MultiPartFileStream(IEnumerable<string> Imagefiles, FileAccess DiskAccess, FileShare ShareMode) : CombinedSeekStream(DiskAccess.HasFlag(FileAccess.Write), OpenImagefiles(Imagefiles, DiskAccess, ShareMode))
{

    public MultiPartFileStream(IEnumerable<string> Imagefiles, FileAccess DiskAccess)
        : this(Imagefiles, DiskAccess, FileShare.Read | FileShare.Delete)
    {
    }

    private static IReadOnlyCollection<FileStream> OpenImagefiles(IEnumerable<string> Imagefiles, FileAccess DiskAccess, FileShare ShareMode)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(Imagefiles);
#else
        if (Imagefiles is null)
        {
            throw new ArgumentNullException(nameof(Imagefiles));
        }
#endif

        var imagestreams = new List<FileStream>();

        try
        {
            foreach (var Imagefile in Imagefiles)
            {
                Trace.WriteLine($"Opening image {Imagefile}");
                imagestreams.Add(new FileStream(Imagefile, FileMode.Open, DiskAccess, ShareMode));
            }

            if (imagestreams.Count == 0)
            {
                throw new ArgumentException("No image file names provided.", nameof(Imagefiles));
            }

            return imagestreams;
        }
        catch (Exception ex)
        {
            imagestreams.ForEach(imagestream => imagestream.Close());

            throw new IOException($"Error opening image files '{Imagefiles.FirstOrDefault()}'", ex);
        }
    }

    public MultiPartFileStream(string FirstImagefile, FileAccess DiskAccess)
        : this(ProviderSupport.EnumerateMultiSegmentFiles(FirstImagefile), DiskAccess)
    {
    }

    public MultiPartFileStream(string FirstImagefile, FileAccess DiskAccess, FileShare ShareMode)
        : this(ProviderSupport.EnumerateMultiSegmentFiles(FirstImagefile), DiskAccess, ShareMode)
    {
    }
}