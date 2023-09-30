//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Extensions;

public static partial class AsyncExtensions
{
    public static readonly Task<int> ZeroCompletedTask = Task.FromResult(0);

#if !NET7_0_OR_GREATER
    public static Task<string?> ReadLineAsync(this TextReader reader, CancellationToken _)
        => reader.ReadLineAsync();

    public static Task WriteLineAsync(this TextWriter writer, ReadOnlyMemory<char> str, CancellationToken _)
        => MemoryMarshal.TryGetString(str, out var text, out int start, out int length) && start == 0 && length == text.Length
        ? writer.WriteLineAsync(text)
        : MemoryMarshal.TryGetArray(str, out var segment)
        ? writer.WriteLineAsync(segment.Array!, segment.Offset, segment.Count)
        : writer.WriteLineAsync(str.ToString());
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SynchronizationContext? GetSynchronizationContext(this ISynchronizeInvoke owner) =>
        owner.InvokeRequired ?
        (SynchronizationContext?)owner.Invoke(() => SynchronizationContext.Current, null) :
        SynchronizationContext.Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WaitHandleAwaiter WithTimeout(this WaitHandle handle, TimeSpan timeout) =>
        new(handle, timeout);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WaitHandleAwaiter WithTimeout(this WaitHandle handle, int mSecTimeout) =>
        new(handle, TimeSpan.FromMilliseconds(mSecTimeout));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<bool> WaitAsync(this WaitHandle handle) =>
        await new WaitHandleAwaiter(handle, Timeout.InfiniteTimeSpan);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<bool> WaitAsync(this WaitHandle handle, TimeSpan timeout) =>
        await new WaitHandleAwaiter(handle, timeout);

    public static async Task<int> RunProcessAsync(string exe, string args)
    {
        using var ps = new Process
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = exe,
                Arguments = args
            }
        };

        ps.Start();

        return await new ProcessAwaiter(ps);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task WaitForExitAsync(this Process process, CancellationToken _ = default)
    {
        process.EnableRaisingEvents = true;
        await new ProcessAwaiter(process);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<int> WaitForResultAsync(this Process process, CancellationToken cancellationToken = default)
    {
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static WaitHandle CreateWaitHandle(this Process process, bool inheritable) =>
        NativeWaitHandle.DuplicateExisting(process.Handle, inheritable);

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    private sealed partial class NativeWaitHandle : WaitHandle
    {
#if NET7_0_OR_GREATER
        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DuplicateHandle(nint hSourceProcessHandle, nint hSourceHandle, nint hTargetProcessHandle, out SafeWaitHandle lpTargetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);

        [LibraryImport("kernel32", SetLastError = true)]
        private static partial nint GetCurrentProcess();
#else
        [DllImport("kernel32", SetLastError = true)]
        private static extern bool DuplicateHandle(nint hSourceProcessHandle, nint hSourceHandle, nint hTargetProcessHandle, out SafeWaitHandle lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32", SetLastError = true)]
        private static extern nint GetCurrentProcess();
#endif

        public static NativeWaitHandle DuplicateExisting(nint handle, bool inheritable) => !DuplicateHandle(GetCurrentProcess(), handle, GetCurrentProcess(), out var new_handle, 0, inheritable, 0x2)
                ? throw new Win32Exception()
                : (new(new_handle));

        public NativeWaitHandle(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }
    }
}

public readonly struct ProcessAwaiter : ICriticalNotifyCompletion
{
    public Process? Process { get; }

    public ProcessAwaiter(Process process)
    {
        try
        {
            if (process is null || process.Handle == IntPtr.Zero)
            {
                Process = null;
                return;
            }

            if (!process.EnableRaisingEvents)
            {
                throw new NotSupportedException("Events not available for this Process object.");
            }
        }
        catch (Exception ex)
        {
            throw new NotSupportedException("ProcessAwaiter requires a local, running Process object with EnableRaisingEvents property set to true when Process object was created.", ex);
        }

        Process = process;
    }

    public ProcessAwaiter GetAwaiter() => this;

    public bool IsCompleted => Process?.HasExited ?? true;

    public int GetResult() => Process?.ExitCode
        ?? throw new NotSupportedException("This instance is not associated with a Process instance");

    public void OnCompleted(Action continuation) => throw new NotSupportedException();

    public void UnsafeOnCompleted(Action continuation)
    {
        var completion_counter = 0;

        if (Process is null)
        {
            throw new NotSupportedException("This instance is not associated with a Process instance");
        }

        Process.Exited += (sender, e) =>
        {
            if (Interlocked.Exchange(ref completion_counter, 1) == 0)
            {
                continuation();
            }
        };

        if (Process.HasExited && Interlocked.Exchange(ref completion_counter, 1) == 0)
        {
            continuation();
        }
    }
}

public sealed class WaitHandleAwaiter : ICriticalNotifyCompletion
{
    private readonly WaitHandle handle;
    private readonly TimeSpan timeout;
    private RegisteredWaitHandle? callbackHandle;
    private Action? continuation;
    private bool result = true;

    public WaitHandleAwaiter(WaitHandle handle, TimeSpan timeout)
    {
        this.handle = handle;
        this.timeout = timeout;
    }

    public WaitHandleAwaiter GetAwaiter() => this;

    public bool IsCompleted => handle.WaitOne(0);

    public bool GetResult() => result;

    public void OnCompleted(Action continuation) => throw new NotSupportedException();

    public void UnsafeOnCompleted(Action continuation)
    {
        this.continuation = continuation;

        callbackHandle = ThreadPool.RegisterWaitForSingleObject(
            waitObject: handle,
            callBack: WaitProc,
            state: this,
            timeout: timeout,
            executeOnlyOnce: true);
    }

    private static void WaitProc(object? state, bool timedOut)
    {
        var obj = state as WaitHandleAwaiter
            ?? throw new InvalidAsynchronousStateException();

        obj.result = !timedOut;

        while (obj.callbackHandle is null)
        {
            Thread.Yield();
        }

        obj.callbackHandle.Unregister(null);

        obj.continuation?.Invoke();
    }
}
