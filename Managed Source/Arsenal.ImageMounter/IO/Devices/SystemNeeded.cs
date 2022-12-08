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
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arsenal.ImageMounter.IO.Devices;

/// <summary>
/// Makes sure that screen stays on or computer does not go into sleep
/// during some work
/// </summary>
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public partial class SystemNeeded : IDisposable
{
    /// <summary>
    /// Flags indicating what system resource and interface are required
    /// </summary>
    [Flags]
    public enum ExecutionState : uint
    {
        /// <summary>
        /// </summary>
        SystemRequired = 0x00000001,
        /// <summary>
        /// </summary>
        DisplayRequired = 0x00000002,
        /// <summary>
        /// </summary>
        UserPresent = 0x00000004,
        /// <summary>
        /// </summary>
        AwaymodeRequired = 0x00000040,
        /// <summary>
        /// </summary>
        Continuous = 0x80000000
    }

#if NET7_0_OR_GREATER
    [LibraryImport("KERNEL32", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static partial ExecutionState SetThreadExecutionState(ExecutionState executionState);

    [LibraryImport("KERNEL32", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static partial uint GetCurrentThreadId();
#else
    [DllImport("KERNEL32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState executionState);

    [DllImport("KERNEL32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern uint GetCurrentThreadId();
#endif

    private readonly ExecutionState previous_state;

    private readonly uint thread_id;

    /// <summary>
    /// Initializes a block of code that is done with SystemRequired and Continous requirements
    /// </summary>
    public SystemNeeded()
        : this(ExecutionState.SystemRequired | ExecutionState.Continuous)
    {
    }

    /// <summary>
    /// Initializes a block of code that is done with certain resource and interface requirements
    /// </summary>
    public SystemNeeded(ExecutionState executionState)
    {
        thread_id = GetCurrentThreadId();
        previous_state = SetThreadExecutionState(executionState);
        if (previous_state == 0)
        {
            throw new Exception("SetThreadExecutionState failed.");
        }
    }

    /// <summary>
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (previous_state != 0 && thread_id == GetCurrentThreadId())
        {
            SetThreadExecutionState(previous_state);
        }
    }

    /// <summary>
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
