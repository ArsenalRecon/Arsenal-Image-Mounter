//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using LTRData.Extensions.Buffers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Arsenal.ImageMounter.IO.Native.NativeConstants;
using static Arsenal.ImageMounter.IO.Native.NativeFileIO.UnsafeNativeMethods;



namespace Arsenal.ImageMounter.IO.Devices;

[SupportedOSPlatform(SUPPORTED_WINDOWS_PLATFORM)]
public readonly struct FileStreamsEnumerator(string filePath) : IEnumerable<FindStreamData>
{
    public string FilePath { get; } = filePath;

    public IEnumerator<FindStreamData> GetEnumerator() => new Enumerator(FilePath);

    private IEnumerator IEnumerable_GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => IEnumerable_GetEnumerator();

    public sealed class Enumerator(string filePath) : IEnumerator<FindStreamData>
    {
        public string FilePath { get; } = filePath;

        public SafeFindHandle? SafeHandle { get; private set; }

        private FindStreamData current;

        public FindStreamData Current => disposedValue
            ? throw new ObjectDisposedException("FileStreamsEnumerator.Enumerator")
            : current;

        private object IEnumerator_Current => Current;

        object IEnumerator.Current => IEnumerator_Current;

        public bool MoveNext()
        {
            if (disposedValue)
            {
                throw new ObjectDisposedException("FileStreamsEnumerator.Enumerator");
            }

            if (SafeHandle is null)
            {
                SafeHandle = FindFirstStreamW(FilePath.AsRef(), 0, out current, 0);

                if (!SafeHandle.IsInvalid)
                {
                    return true;
                }
                else
                {
                    return Marshal.GetLastWin32Error() == ERROR_HANDLE_EOF ? false : throw new Win32Exception();
                }
            }
            else if (FindNextStream(SafeHandle, out current))
            {
                return true;
            }
            else
            {
                return Marshal.GetLastWin32Error() == ERROR_HANDLE_EOF ? false : throw new Win32Exception();
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
                }

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                SafeHandle = null;

                // TODO: set large fields to null.
                current = default;
            }

            disposedValue = true;
        }

        // TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
        // Protected Overrides Sub Finalize()
        // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        // Dispose(False)
        // MyBase.Finalize()
        // End Sub

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