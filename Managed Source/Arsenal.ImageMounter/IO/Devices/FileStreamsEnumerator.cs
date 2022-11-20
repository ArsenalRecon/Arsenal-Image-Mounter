using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Arsenal.ImageMounter.IO.Native.NativeConstants;
using static Arsenal.ImageMounter.IO.Native.NativeFileIO.UnsafeNativeMethods;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO.Devices;

[SupportedOSPlatform(SUPPORTED_WINDOWS_PLATFORM)]
public class FileStreamsEnumerator : IEnumerable<FindStreamData>
{

    public ReadOnlyMemory<char> FilePath { get; set; }

    public IEnumerator<FindStreamData> GetEnumerator() => new Enumerator(FilePath);

    private IEnumerator IEnumerable_GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => IEnumerable_GetEnumerator();

    public FileStreamsEnumerator(ReadOnlyMemory<char> FilePath)
    {
        this.FilePath = FilePath;
    }

    public sealed class Enumerator : IEnumerator<FindStreamData>
    {

        public ReadOnlyMemory<char> FilePath { get; private set; }

        public SafeFindHandle? SafeHandle { get; private set; }

        private FindStreamData _current;

        public Enumerator(ReadOnlyMemory<char> FilePath)
        {
            this.FilePath = FilePath;
        }

        public FindStreamData Current => disposedValue
            ? throw new ObjectDisposedException("FileStreamsEnumerator.Enumerator")
            : _current;

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
                SafeHandle = FindFirstStreamW(FilePath.MakeNullTerminated(), 0, out _current, 0);

                if (!SafeHandle.IsInvalid)
                {
                    return true;
                }
                else
                {
                    return Marshal.GetLastWin32Error() == ERROR_HANDLE_EOF ? false : throw new Win32Exception();
                }
            }
            else if (FindNextStream(SafeHandle, out _current))
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
                _current = default;
            }

            disposedValue = true;
        }

        // ' TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
        // Protected Overrides Sub Finalize()
        // ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
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