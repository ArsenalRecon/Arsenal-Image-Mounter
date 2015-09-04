// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#include "targetver.h"

#include <ntstatus.h>

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
#define WIN32_NO_STATUS
// Windows Header Files:

#include <windows.h>
#include <objbase.h>
#include <shellapi.h>
#include <devioctl.h>
#include <ntdddisk.h>
#include <ntddscsi.h>
#include <winioctl.h>

#include "..\phdskmnt\inc\common.h"
#include "..\phdskmnt\inc\phdskmntver.h"

VOID
WINAPI
ImScsiMsgBoxLastError(HWND hWnd, LPCWSTR Prefix);

int
CDECL
ImScsiMsgBoxPrintF(HWND hWnd, UINT uStyle, LPCWSTR lpTitle,
LPCWSTR lpMessage, ...);
