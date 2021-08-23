
/// debugmg.cpp
/// Handles debug messages sent from library routines, by sending to attached debugger
/// or to application registered callback.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///


#include "stdafx.h"

#include "aimapi.h"

pfImScsiDebugMessageCallback DebugMessageCallback = NULL;
LPVOID DebugMessageCallbackContext = NULL;

AIMAPI_API
VOID
WINAPI
ImScsiSetDebugMessageCallback(LPVOID Context,
pfImScsiDebugMessageCallback Callback)
{
    DebugMessageCallback = Callback;
    DebugMessageCallbackContext = Context;
}

VOID
CDECL
ImScsiDebugMessage(LPCWSTR FormatString, ...)
{
    va_list param_list;
    LPWSTR lpBuf = NULL;

    va_start(param_list, FormatString);

    if (!FormatMessage(
        FORMAT_MESSAGE_ALLOCATE_BUFFER |
        FORMAT_MESSAGE_FROM_STRING, FormatString, 0, 0,
        (LPWSTR)&lpBuf, 0, &param_list))
    {
        return;
    }

    OutputDebugString(lpBuf);
    OutputDebugStringA("\n");

    if (DebugMessageCallback != NULL)
    {
        DebugMessageCallback(DebugMessageCallbackContext, lpBuf);
    }

    LocalFree(lpBuf);

    return;
}

