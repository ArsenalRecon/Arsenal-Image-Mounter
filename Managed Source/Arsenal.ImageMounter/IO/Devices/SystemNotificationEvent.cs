//  SystemNotificationEvent.vb
//  Represents a system notification event object. Well known paths are available as constants of SystemNotificationEvent class.
//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Threading;

namespace Arsenal.ImageMounter.IO.Devices;

/// <summary>
/// Represents a system notification event object. Well known paths are available as constants of SystemNotificationEvent class.
/// </summary>
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public class SystemNotificationEvent : WaitHandle
{
    /// <summary>
    /// Opens a system notification event object. Well known paths are available as constants of SystemNotificationEvent class.
    /// </summary>
    /// <param name="EventName">NT name and path to event to open</param>
    public SystemNotificationEvent(string EventName)
    {
        SafeWaitHandle = NativeFileIO.NtOpenEvent(EventName, 0, FileSystemRights.Synchronize | NativeConstants.EVENT_QUERY_STATE, null);
    }

    public const string PrefetchTracesReady = @"\KernelObjects\PrefetchTracesReady";
    public const string MemoryErrors = @"\KernelObjects\MemoryErrors";
    public const string LowNonPagedPoolCondition = @"\KernelObjects\LowNonPagedPoolCondition";
    public const string SuperfetchScenarioNotify = @"\KernelObjects\SuperfetchScenarioNotify";
    public const string SuperfetchParametersChanged = @"\KernelObjects\SuperfetchParametersChanged";
    public const string SuperfetchTracesReady = @"\KernelObjects\SuperfetchTracesReady";
    public const string PhysicalMemoryChange = @"\KernelObjects\PhysicalMemoryChange";
    public const string HighCommitCondition = @"\KernelObjects\HighCommitCondition";
    public const string HighMemoryCondition = @"\KernelObjects\HighMemoryCondition";
    public const string HighNonPagedPoolCondition = @"\KernelObjects\HighNonPagedPoolCondition";
    public const string SystemErrorPortReady = @"\KernelObjects\SystemErrorPortReady";
    public const string MaximumCommitCondition = @"\KernelObjects\MaximumCommitCondition";
    public const string LowCommitCondition = @"\KernelObjects\LowCommitCondition";
    public const string HighPagedPoolCondition = @"\KernelObjects\HighPagedPoolCondition";
    public const string LowMemoryCondition = @"\KernelObjects\LowMemoryCondition";
    public const string LowPagedPoolCondition = @"\KernelObjects\LowPagedPoolCondition";
    public const string AIMWrFltrDiffFullEvent = @"\Device\AIMWrFltrDiffFullEvent";
}

public sealed class RegisteredEventHandler : IDisposable
{
    private readonly RegisteredWaitHandle registered_wait_handle;

    public WaitHandle WaitHandle { get; }
    public EventHandler EventHandler { get; }

    public RegisteredEventHandler(WaitHandle waitObject, EventHandler handler)
    {
        WaitHandle = waitObject;

        EventHandler = handler;

        registered_wait_handle = ThreadPool.RegisterWaitForSingleObject(waitObject, Callback, this, -1, executeOnlyOnce: true);
    }

    private static void Callback(object? state, bool timedOut)
    {
        var obj = (RegisteredEventHandler?)state;

        obj?.EventHandler?.Invoke(obj.WaitHandle, EventArgs.Empty);
    }

    public void Dispose()
    {
        registered_wait_handle?.Unregister(null);

        GC.SuppressFinalize(this);
    }
}

public class WaitEventHandler(WaitHandle WaitHandle, bool ownsHandle) : IDisposable
{
    public WaitHandle WaitHandle { get; } = WaitHandle;

    private readonly bool ownsHandle = ownsHandle;
    private readonly List<RegisteredEventHandler> event_handlers = [];

    public event EventHandler Signalled
    {
        add
        {
            lock (event_handlers)
            {
                event_handlers.Add(new RegisteredEventHandler(WaitHandle, value));
            }
        }

        remove
        {
            lock (event_handlers)
            {
                event_handlers.RemoveAll(handler =>
                {
                    if (handler.EventHandler.Equals(value))
                    {
                        handler.Dispose();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });
            }
        }
    }

    private void OnSignalled(object sender, EventArgs e)
    {
        lock (event_handlers)
        {
            event_handlers.ForEach(handler => handler.EventHandler?.Invoke(sender, e));
        }
    }

    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                lock (event_handlers)
                {
                    event_handlers.ForEach(handler => handler.Dispose());
                    event_handlers.Clear();
                }

                if (ownsHandle)
                {
                    WaitHandle.Dispose();
                }
            }

            disposedValue = true;
        }
    }

    ~WaitEventHandler()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
