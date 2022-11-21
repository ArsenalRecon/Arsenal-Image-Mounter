using System;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.GenericProviders;

public sealed class DummyProvider : IDevioProvider
{

    /// <summary>
    /// Event when object is about to be disposed
    /// </summary>
    public event EventHandler? Disposing;

    /// <summary>
    /// Event when object has been disposed
    /// </summary>
    public event EventHandler? Disposed;

    public DummyProvider(long Length)
    {

        this.Length = Length;

    }

    public long Length { get; }

    public uint SectorSize => 512U;

    public bool CanWrite => true;

    bool IDevioProvider.SupportsShared => false;

    void IDevioProvider.SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys) => throw new NotImplementedException();

    int IDevioProvider.Read(IntPtr buffer, int bufferoffset, int count, long fileoffset) => throw new NotImplementedException();

    int IDevioProvider.Read(byte[] buffer, int bufferoffset, int count, long fileoffset) => throw new NotImplementedException();

    int IDevioProvider.Read(Span<byte> buffer, long fileoffset) => throw new NotImplementedException();

    int IDevioProvider.Write(IntPtr buffer, int bufferoffset, int count, long fileoffset) => throw new NotImplementedException();

    int IDevioProvider.Write(byte[] buffer, int bufferoffset, int count, long fileoffset) => throw new NotImplementedException();

    int IDevioProvider.Write(ReadOnlySpan<byte> buffer, long fileoffset) => throw new NotImplementedException();

    public void Dispose()
    {
        Disposing?.Invoke(this, EventArgs.Empty);
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}

