/// No-GUI support for aimapi.dll.
///
/// Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#include "stdafx.h"

#include <windows.h>
#include <winioctl.h>
#include <shellapi.h>
#include <shlobj.h>
#include <dbt.h>

#include <stdio.h>

#include "aimapi.h"

AIMAPI_API
int WINAPI
ImScsiConsoleMessageA(
    HWND,
    LPCSTR lpText,
    LPCSTR lpCaption,
    UINT uType)
{
    FILE *stream;

    if ((uType & MB_SERVICE_NOTIFICATION) ==
        MB_SERVICE_NOTIFICATION)
    {
        stream = stderr;
    }
    else switch (uType & MB_ICONMASK)
    {
    case MB_ICONASTERISK:
    case MB_ICONHAND:
        stream = stderr;
        break;

    default:
        stream = stdout;
    }
    
    LPCSTR prompt = NULL;
    LPCSTR buttons = NULL;

    switch (uType & MB_TYPEMASK)
    {
    case MB_CANCELTRYCONTINUE:
        prompt = "(C)ancel, (T)ry again, Contin(u)e?";
        buttons = "CTU";
        break;

    case MB_RETRYCANCEL:
        prompt = "(R)etry, (C)ancel?";
        buttons = "RC";
        break;

    case MB_YESNO:
        prompt = "(Y)es, (N)o?";
        buttons = "YN";
        break;

    case MB_YESNOCANCEL:
        prompt = "(Y)es, (N)o, (C)ancel?";
        buttons = "YNC";
        break;

    case MB_ABORTRETRYIGNORE:
        prompt = "(A)bort, (R)etry, (I)gnore?";
        buttons = "ARI";
        break;

    case MB_OKCANCEL:
        prompt = "(O)K, (C)ancel?";
        buttons = "OC";
        break;
    }

    fprintf(stream,
        "\r\n"
        " -- %s --\r\n"
        "\n"
        "%s\r\n",
        lpCaption ? lpCaption : "***", lpText);

    fflush(stream);

    if (prompt == NULL || buttons == NULL)
    {
        return IDOK;
    }

    UINT defbtn = (uType & MB_DEFMASK) >> 8;
    if (defbtn >= strlen(buttons))
    {
        defbtn = 0;
    }

    int answer;
    for (;;)
    {
        fprintf(stream,
            "%s %c\b",
            prompt, buttons[defbtn]);

        fflush(stream);

        answer = toupper(_fgetchar());

        if (answer == '\r' || answer == '\n' || answer == 0)
        {
            answer = buttons[defbtn];
        }
        else if (strchr(buttons, answer) == NULL)
        {
            continue;
        }

        switch (answer)
        {
        case 'O':
            return IDOK;

        case 'C':
            return IDCANCEL;

        case 'A':
            return IDABORT;

        case 'R':
            return IDRETRY;

        case 'I':
            return IDIGNORE;

        case 'Y':
            return IDYES;

        case 'N':
            return IDNO;

        case 'T':
            return IDTRYAGAIN;

        case 'U':
            return IDCONTINUE;
        }
    }
}

AIMAPI_API
int WINAPI
ImScsiConsoleMessageW(
    HWND,
    LPCWSTR lpText,
    LPCWSTR lpCaption,
    UINT uType)
{
    FILE *stream;

    if ((uType & MB_SERVICE_NOTIFICATION) ==
        MB_SERVICE_NOTIFICATION)
    {
        stream = stderr;
    }
    else switch (uType & MB_ICONMASK)
    {
    case MB_ICONASTERISK:
    case MB_ICONHAND:
        stream = stderr;
        break;

    default:
        stream = stdout;
    }

    LPCWSTR prompt = NULL;
    LPCWSTR buttons = NULL;

    switch (uType & MB_TYPEMASK)
    {
    case MB_CANCELTRYCONTINUE:
        prompt = L"(C)ancel, (T)ry again, Contin(u)e?";
        buttons = L"CTU";
        break;

    case MB_RETRYCANCEL:
        prompt = L"(R)etry, (C)ancel?";
        buttons = L"RC";
        break;

    case MB_YESNO:
        prompt = L"(Y)es, (N)o?";
        buttons = L"YN";
        break;

    case MB_YESNOCANCEL:
        prompt = L"(Y)es, (N)o, (C)ancel?";
        buttons = L"YNC";
        break;

    case MB_ABORTRETRYIGNORE:
        prompt = L"(A)bort, (R)etry, (I)gnore?";
        buttons = L"ARI";
        break;

    case MB_OKCANCEL:
        prompt = L"(O)K, (C)ancel?";
        buttons = L"OC";
        break;
    }

    fwprintf(stream,
        L"\r\n"
        L" -- %s --\r\n"
        L"\n"
        L"%s\r\n",
        lpCaption ? lpCaption : L"***", lpText);

    fflush(stream);

    if (prompt == NULL || buttons == NULL)
    {
        return IDOK;
    }

    UINT defbtn = (uType & MB_DEFMASK) >> 8;
    if (defbtn >= wcslen(buttons))
    {
        defbtn = 0;
    }

    wchar_t answer;
    for (;;)
    {
        fwprintf(stream,
            L"%s %c\b",
            prompt, buttons[defbtn]);

        fflush(stream);

        answer = towupper(_fgetwchar());

        if (answer == '\r' || answer == '\n' || answer == 0)
        {
            answer = buttons[defbtn];
        }
        else if (wcschr(buttons, answer) == NULL)
        {
            continue;
        }

        switch (answer)
        {
        case 'O':
            return IDOK;

        case 'C':
            return IDCANCEL;

        case 'A':
            return IDABORT;

        case 'R':
            return IDRETRY;

        case 'I':
            return IDIGNORE;

        case 'Y':
            return IDYES;

        case 'N':
            return IDNO;

        case 'T':
            return IDTRYAGAIN;

        case 'U':
            return IDCONTINUE;
        }
    }
}
