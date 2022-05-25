using Arsenal.ImageMounter.IO;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable CA1062 // Validate arguments of public methods
#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CA1303 // Do not pass literals as localized parameters
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Extensions;

public static partial class LowLevelExtensions
{
    public static IEnumerable<Exception> Enumerate(this Exception ex)
    {
        while (ex is not null)
        {
            if (ex is TargetInvocationException)
            {
                ex = ex.InnerException;
            }
            else if (ex is AggregateException aex)
            {
                foreach (var iex in aex.InnerExceptions.SelectMany(Enumerate))
                {
                    yield return iex;
                }

                yield break;
            }
            else if (ex is ReflectionTypeLoadException rtlex)
            {
                yield return ex;

                foreach (var iex in rtlex.LoaderExceptions.SelectMany(Enumerate))
                {
                    yield return iex;
                }

                ex = ex.InnerException;
            }
            else
            {
                yield return ex;

                ex = ex.InnerException;
            }
        }
    }

    public static IEnumerable<string> EnumerateMessages(this Exception ex)
    {
        while (ex is not null)
        {
            if (ex is TargetInvocationException)
            {
                ex = ex.InnerException;
            }
            else if (ex is AggregateException agex)
            {
                foreach (var msg in agex.InnerExceptions.SelectMany(EnumerateMessages))
                {
                    yield return msg;
                }

                yield break;
            }
            else if (ex is ReflectionTypeLoadException tlex)
            {
                yield return ex.Message;

                foreach (var msg in tlex.LoaderExceptions.SelectMany(EnumerateMessages))
                {
                    yield return msg;
                }

                ex = ex.InnerException;
            }
            else if (ex is Win32Exception win32ex)
            {
                yield return $"{win32ex.Message} ({win32ex.NativeErrorCode})";

                ex = ex.InnerException;
            }
            else
            {
                yield return ex.Message;

                ex = ex.InnerException;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string JoinMessages(this Exception exception) =>
        exception.JoinMessages(Environment.NewLine + Environment.NewLine);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string JoinMessages(this Exception exception, string separator) =>
        string.Join(separator, exception.EnumerateMessages());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatLogMessages(this Exception exception) =>
#if DEBUG
            System.Diagnostics.Debugger.IsAttached ? exception.JoinMessages() : exception?.ToString();
#else
            exception.JoinMessages();
#endif

    public static string CalculateChecksum<THashAlgorithm>(string file) where THashAlgorithm : HashAlgorithm, new()
    {
        byte[] hash;
        using (var stream = File.OpenRead(file))
        using (var hashprovider = new THashAlgorithm())
        {
            hash = hashprovider.ComputeHash(stream);
        }

        return hash.ToHexString();
    }

    public static string CalculateChecksum<THashAlgorithm>(Stream stream) where THashAlgorithm : HashAlgorithm, new()
    {
        byte[] hash;
        using (var hashprovider = new THashAlgorithm())
        {
            hash = hashprovider.ComputeHash(stream);
        }

        return hash.ToHexString();
    }

    public static string CalculateChecksum<THashAlgorithm>(this byte[] data) where THashAlgorithm : HashAlgorithm, new()
    {
        byte[] hash;
        using (var hashprovider = new THashAlgorithm())
        {
            hash = hashprovider.ComputeHash(data);
        }

        return hash.ToHexString();
    }

    public static IAsyncResult AsAsyncResult<T>(this Task<T> task, AsyncCallback callback, object state)
    {
        var returntask = task.ContinueWith((t, _) => t.Result, state, TaskScheduler.Default);

        if (callback is not null)
        {
            returntask.ContinueWith(callback.Invoke, TaskScheduler.Default);
        }

        return returntask;
    }

    public static IAsyncResult AsAsyncResult(this Task task, AsyncCallback callback, object state)
    {
        var returntask = task.ContinueWith((t, _) => { }, state, TaskScheduler.Default);

        if (callback is not null)
        {
            returntask.ContinueWith(callback.Invoke, TaskScheduler.Default);
        }

        return returntask;
    }

    public static readonly Task<int> ZeroCompletedTask = Task.FromResult(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddRange<T>(this List<T> list, params T[] collection) => list.AddRange(collection);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SynchronizationContext GetSynchronizationContext(this ISynchronizeInvoke owner) =>
        owner.InvokeRequired ?
        owner.Invoke(new Func<SynchronizationContext>(() => SynchronizationContext.Current), null) as SynchronizationContext :
        SynchronizationContext.Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WaitHandleAwaiter GetAwaiterWithTimeout(this WaitHandle handle, TimeSpan timeout) =>
        new(handle, timeout);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WaitHandleAwaiter GetAwaiter(this WaitHandle handle) =>
        new(handle, Timeout.InfiniteTimeSpan);

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

        return await ps;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessAwaiter GetAwaiter(this Process process) =>
        new(process);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<byte> AsSpan(IntPtr ptr, int length) =>
        new(ptr.ToPointer(), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ReadOnlySpan<byte> AsReadOnlySpan(IntPtr ptr, int length) =>
        new(ptr.ToPointer(), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<byte> AsSpan(this SafeBuffer ptr) =>
        new(ptr.DangerousGetHandle().ToPointer(), (int)ptr.ByteLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsSpan(this MemoryStream memoryStream) =>
        memoryStream.GetBuffer().AsSpan(0, checked((int)memoryStream.Length));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<byte> AsMemory(this MemoryStream memoryStream) =>
        memoryStream.GetBuffer().AsMemory(0, checked((int)memoryStream.Length));

#if !NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T AsRef<T>(this ReadOnlySpan<byte> bytes) where T : struct =>
        ref MemoryMarshal.Cast<byte, T>(bytes)[0];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsRef<T>(this Span<byte> bytes) where T : struct =>
        ref MemoryMarshal.Cast<byte, T>(bytes)[0];
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T AsRef<T>(this ReadOnlySpan<byte> bytes) where T : struct =>
        ref MemoryMarshal.AsRef<T>(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsRef<T>(this Span<byte> bytes) where T : struct =>
        ref MemoryMarshal.AsRef<T>(bytes);
#endif

    /// <summary>
    /// Sets a bit to 1 in a bit field.
    /// </summary>
    /// <param name="data">Bit field</param>
    /// <param name="bitnumber">Bit number to set to 1</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBit(this Span<byte> data, int bitnumber) =>
        data[bitnumber >> 3] |= (byte)(1 << ((~bitnumber) & 7));

    /// <summary>
    /// Sets a bit to 0 in a bit field.
    /// </summary>
    /// <param name="data">Bit field</param>
    /// <param name="bitnumber">Bit number to set to 0</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearBit(this Span<byte> data, int bitnumber) =>
        data[bitnumber >> 3] &= unchecked((byte)~(1 << ((~bitnumber) & 7)));

    /// <summary>
    /// Gets a bit from a bit field.
    /// </summary>
    /// <param name="data">Bit field</param>
    /// <param name="bitnumber">Bit number to get</param>
    /// <returns>True if value of specified bit is 1, false if 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetBit(this ReadOnlySpan<byte> data, int bitnumber) =>
        (data[bitnumber >> 3] & (1 << ((~bitnumber) & 7))) != 0;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> CreateReadOnlySpan<T>(in T source, int length) =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(source), length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BinaryCompare<T>(in T s1, in T s2) where T : struct =>
        BinaryCompare(CreateReadOnlySpan(s1, 1), CreateReadOnlySpan(s2, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(in T source) where T : struct =>
        MemoryMarshal.AsBytes(CreateReadOnlySpan(source, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsBytes<T>(ref T source) where T : struct =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref source, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToHexString<T>(in T source) where T : struct =>
        ToHexString(AsReadOnlyBytes(source));

    public static string ToHexString(ReadOnlySpan<byte> bytes)
    {
        var str = new string('\0', bytes.Length << 1);

        var target = MemoryMarshal.AsMemory(str.AsMemory()).Span;

        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i].TryFormat(target.Slice(i << 1, 2), out _, "x2");
        }

        return target.ToString();
    }

    public static int GetHashCode<T>(in T source) where T : struct =>
        GetHashCode(AsReadOnlyBytes(source));
#endif

    public static int GetHashCode(ReadOnlySpan<byte> ptr)
    {
        var result = 0;
        for (var i = 0; i < ptr.Length; i++)
        {
            result ^= ptr[i] << ((i & 0x3) * 8);
        }
        return result;
    }

    [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    private static extern int memcmp(in byte ptr1, in byte ptr2, IntPtr count);

    [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    private static extern ref byte memcpy(out byte ptrTarget, in byte ptrSource, IntPtr count);

    /// <summary>
    /// Compares two byte spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>If sequences are both empty, true is returned. If sequences have different lengths, false is returned.
    /// If lengths are equal and byte sequences are equal, true is returned.</returns>
    public static bool BinaryEqual(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if (first.IsEmpty && second.IsEmpty)
        {
            return true;
        }

        if (first.Length != second.Length)
        {
            return false;
        }

        return first == second ||
            memcmp(first[0], second[0], new IntPtr(first.Length)) == 0;
    }

    /// <summary>
    /// Compares two byte spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>Result of memcmp comparison.</returns>
    public static int BinaryCompare(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if ((first.IsEmpty && second.IsEmpty) || first == second)
        {
            return 0;
        }

        return memcmp(first[0], second[0], new IntPtr(first.Length));
    }

    /// <summary>
    /// Compares two spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>If sequences are both empty, true is returned. If sequences have different lengths, false is returned.
    /// If lengths are equal and byte sequences are equal, true is returned.</returns>
    public static bool BinaryEqual<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : struct
        => BinaryEqual(MemoryMarshal.AsBytes(first), MemoryMarshal.AsBytes(second));

    /// <summary>
    /// Compares two spans using C runtime memcmp function.
    /// </summary>
    /// <param name="first">First span</param>
    /// <param name="second">Second span</param>
    /// <returns>Result of memcmp comparison.</returns>
    public static int BinaryCompare<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : struct
        => BinaryCompare(MemoryMarshal.AsBytes(first), MemoryMarshal.AsBytes(second));

#if !NETCOREAPP

    public static ReadOnlyMemory<char> TrimEnd(this ReadOnlyMemory<char> str, char chr) =>
        str.Slice(0, str.Span.TrimEnd(chr).Length);

    public static ReadOnlyMemory<char> TrimStart(this ReadOnlyMemory<char> str, char chr) =>
        str.Slice(str.Span.TrimStart(chr).Length);

    public static bool Contains(this ReadOnlySpan<char> str, char chr) =>
        str.IndexOf(chr) >= 0;

    public static bool Contains(this string str, char chr) =>
        str.IndexOf(chr) >= 0;

    public static bool Contains(this string str, string substr) =>
        str.IndexOf(substr) >= 0;

    public static bool Contains(this string str, string substr, StringComparison comparison) =>
        str.IndexOf(substr, comparison) >= 0;

#endif

    public static ref char MakeNullTerminated(this ReadOnlyMemory<char> str)
    {
        if (MemoryMarshal.TryGetArray(str, out var chars) &&
            chars.Offset + chars.Count < chars.Array.Length &&
            chars.Array[chars.Offset + chars.Count] == '\0')
        {
            return ref chars.Array[chars.Offset];
        }

        if (MemoryMarshal.TryGetString(str, out var text, out var start, out var length) &&
            start + length == text.Length)
        {
            return ref MemoryMarshal.GetReference(str.Span);
        }

        var buffer = new char[str.Length + 1];
        str.CopyTo(buffer);
        return ref buffer[0];
    }

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static WaitHandle CreateWaitHandle(this Process process, bool inheritable) =>
        NativeWaitHandle.DuplicateExisting(process.Handle, inheritable);

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    private sealed class NativeWaitHandle : WaitHandle
    {
        [DllImport("kernel32")]
        private static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out SafeWaitHandle lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32")]
        private static extern IntPtr GetCurrentProcess();

        public static NativeWaitHandle DuplicateExisting(IntPtr handle, bool inheritable)
        {
            if (!DuplicateHandle(GetCurrentProcess(), handle, GetCurrentProcess(), out var new_handle, 0, inheritable, 0x2))
            {
                throw new Win32Exception();
            }

            return new(new_handle);
        }

        public NativeWaitHandle(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }
    }
}

public readonly struct ProcessAwaiter : INotifyCompletion
{
    public Process Process { get; }

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

    public bool IsCompleted => Process.HasExited;

    public int GetResult() => Process.ExitCode;

    public void OnCompleted(Action continuation)
    {
        var completion_counter = 0;

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

public sealed class WaitHandleAwaiter : INotifyCompletion
{
    private readonly WaitHandle handle;
    private readonly TimeSpan timeout;
    private bool result;

    public WaitHandleAwaiter(WaitHandle handle, TimeSpan timeout)
    {
        this.handle = handle;
        this.timeout = timeout;
    }

    public WaitHandleAwaiter GetAwaiter() => this;

    public bool IsCompleted => handle.WaitOne(0);

    public bool GetResult() => result;

    private sealed class CompletionValues
    {
        public RegisteredWaitHandle callbackHandle;

        public Action continuation;

        public WaitHandleAwaiter awaiter;
    }

    public void OnCompleted(Action continuation)
    {
        var completionValues = new CompletionValues
        {
            continuation = continuation,
            awaiter = this
        };

        completionValues.callbackHandle = ThreadPool.RegisterWaitForSingleObject(
            waitObject: handle,
            callBack: WaitProc,
            state: completionValues,
            timeout: timeout,
            executeOnlyOnce: true);
    }

    private static void WaitProc(object state, bool timedOut)
    {
        var obj = state as CompletionValues;

        obj.awaiter.result = !timedOut;

        while (obj.callbackHandle is null)
        {
            Thread.Sleep(0);
        }

        obj.callbackHandle.Unregister(null);

        obj.continuation();
    }
}
