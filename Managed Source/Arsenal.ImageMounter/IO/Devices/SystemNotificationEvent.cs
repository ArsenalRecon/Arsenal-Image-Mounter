//  SystemNotificationEvent.vb
//  Represents a system notification event object. Well known paths are available as constants of SystemNotificationEvent class.
//  
//  Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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

        SafeWaitHandle = NativeFileIO.NtOpenEvent(EventName, 0, (uint)((long)FileSystemRights.Synchronize | NativeConstants.EVENT_QUERY_STATE), null);

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

        var obj = state as RegisteredEventHandler;

        obj?.EventHandler?.Invoke(obj.WaitHandle, EventArgs.Empty);

    }

    public void Dispose()
    {

        registered_wait_handle?.Unregister(null);

        GC.SuppressFinalize(this);

    }
}

public class WaitEventHandler
{

    public WaitHandle WaitHandle { get; }

    private readonly List<RegisteredEventHandler> event_handlers = new();

    public WaitEventHandler(WaitHandle WaitHandle)
    {
        this.WaitHandle = WaitHandle;

    }

    public event EventHandler Signalled
    {
        add => event_handlers.Add(new RegisteredEventHandler(WaitHandle, value));
        remove => event_handlers.RemoveAll(handler =>
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

    private void OnSignalled(object sender, EventArgs e)
        => event_handlers.ForEach(handler => handler.EventHandler?.Invoke(sender, e));

}