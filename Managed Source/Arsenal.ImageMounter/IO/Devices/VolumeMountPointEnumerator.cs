//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Arsenal.ImageMounter.IO.Native.NativeConstants;
using static Arsenal.ImageMounter.IO.Native.NativeFileIO;
using static Arsenal.ImageMounter.IO.Native.NativeFileIO.UnsafeNativeMethods;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO.Devices;

[SupportedOSPlatform(SUPPORTED_WINDOWS_PLATFORM)]
public readonly struct VolumeMountPointEnumerator : IEnumerable<string>
{
    public string VolumePath { get; }

    public IEnumerator<string> GetEnumerator() => new Enumerator(VolumePath);

    private IEnumerator IEnumerable_GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => IEnumerable_GetEnumerator();

    public VolumeMountPointEnumerator(string VolumePath)
    {
        this.VolumePath = VolumePath;
    }

    private sealed class Enumerator : IEnumerator<string>
    {
        private readonly string volumePath;

        public SafeFindVolumeMountPointHandle? SafeHandle { get; private set; }

        private char[] sb = ArrayPool<char>.Shared.Rent(32767);

        public Enumerator(string VolumePath)
        {
            volumePath = VolumePath;
        }

        public string Current => disposedValue
            ? throw new ObjectDisposedException("VolumeMountPointEnumerator.Enumerator")
            : sb.AsSpan().ReadNullTerminatedUnicodeString();

        private object IEnumerator_Current => Current;

        object IEnumerator.Current => IEnumerator_Current;

        public bool MoveNext()
        {
            if (disposedValue)
            {
                throw new ObjectDisposedException("VolumeMountPointEnumerator.Enumerator");
            }

            if (SafeHandle is null)
            {
                SafeHandle = FindFirstVolumeMountPointW(volumePath.AsRef(), out sb[0], sb.Length);
                if (!SafeHandle.IsInvalid)
                {
                    return true;
                }
                else
                {
                    return Marshal.GetLastWin32Error() == ERROR_NO_MORE_FILES ? false : throw new Win32Exception();
                }
            }
            else if (FindNextVolumeMountPointW(SafeHandle, out sb[0], sb.Length))
            {
                return true;
            }
            else
            {
                return Marshal.GetLastWin32Error() == ERROR_NO_MORE_FILES ? false : throw new Win32Exception();
            }
        }

        void IEnumerator.Reset() => throw new NotImplementedException();

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        // IDisposable
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    SafeHandle?.Dispose();
                    ArrayPool<char>.Shared.Return(sb);
                }

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

                // TODO: set large fields to null.
                sb = null!;
                SafeHandle = null;
            }

            disposedValue = true;
        }

        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}