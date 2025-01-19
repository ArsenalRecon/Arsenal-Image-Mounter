//  API.vb
//  API for manipulating flag values, issuing SCSI bus rescans and similar
//  tasks.
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System;
using System.Threading;



namespace Arsenal.ImageMounter;

public sealed class GlobalCriticalMutex : IDisposable
{

    private const string GlobalCriticalSectionMutexName = @"Global\AIMCriticalOperation";

    private Mutex mutex;

    public bool WasAbandoned { get; }

    public GlobalCriticalMutex()
    {

        mutex = new Mutex(initiallyOwned: true, name: GlobalCriticalSectionMutexName, createdNew: out var createdNew);

        try
        {
            if (!createdNew)
            {
                mutex.WaitOne();
            }
        }

        catch (AbandonedMutexException)
        {
            WasAbandoned = true;
        }

        catch (Exception ex)
        {
            mutex.Dispose();

            throw new Exception("Error entering global critical section for Arsenal Image Mounter driver", ex);

        }
    }

    private bool disposedValue; // To detect redundant calls

    // IDisposable
    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

            // TODO: set large fields to null.
            mutex = null!;
        }

        disposedValue = true;
    }

    // TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
    // Protected Overrides Sub Finalize()
    // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
    // Dispose(False)
    // MyBase.Finalize()
    // End Sub

    // This code added by Visual Basic to correctly implement the disposable pattern.
    public void Dispose() =>
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(true);// TODO: uncomment the following line if Finalize() is overridden above.// GC.SuppressFinalize(Me)

}