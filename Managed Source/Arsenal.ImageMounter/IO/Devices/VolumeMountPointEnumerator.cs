//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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


namespace Arsenal.ImageMounter.IO.Devices;

[SupportedOSPlatform(SUPPORTED_WINDOWS_PLATFORM)]
public readonly struct VolumeMountPointEnumerator(string VolumePath) : IEnumerable<string>
{
    public string VolumePath { get; } = VolumePath;

    public IEnumerator<string> GetEnumerator() => new Enumerator(VolumePath);

    private IEnumerator IEnumerable_GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => IEnumerable_GetEnumerator();

    private sealed class Enumerator(string VolumePath) : IEnumerator<string>
    {
        private readonly string volumePath = VolumePath;

        public SafeFindVolumeMountPointHandle? SafeHandle { get; private set; }

        private char[] sb = ArrayPool<char>.Shared.Rent(32767);

        public string Current => disposedValue
            ? throw new ObjectDisposedException("VolumeMountPointEnumerator.Enumerator")
            : sb.AsSpan().ReadNullTerminatedUnicodeString();

        private object IEnumerator_Current => Current;

        object IEnumerator.Current => IEnumerator_Current;

        public bool MoveNext()
        {
#if NET7_0_OR_GREATER
            ObjectDisposedException.ThrowIf(disposedValue, this);
#else
            if (disposedValue)
            {
                throw new ObjectDisposedException(typeof(Enumerator).Name);
            }
#endif

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