
/// drvsetup.cpp
/// Driver setup routines for command line use.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winioctl.h>
#include <setupapi.h>

#include <stdio.h>
#include <stdlib.h>

#include "..\aimapi\winstrct.hpp"

#include "..\phdskmnt\inc\ntumapi.h"
#include "..\phdskmnt\inc\common.h"
#include "..\aimapi\aimapi.h"

#include "aimcmd.h"

#include <imdisk.h>

int
wmainSetup(int, wchar_t **argv)
{
    enum
    {
        OP_MODE_UNKNOWN,
        OP_MODE_INSTALL,
        OP_MODE_UNINSTALL,
        OP_MODE_RESCAN
    } op_mode;

    if (_wcsicmp(argv[0], L"--install") == 0)
    {
        op_mode = OP_MODE_INSTALL;
    }
    else if (_wcsicmp(argv[0], L"--uninstall") == 0)
    {
        op_mode = OP_MODE_UNINSTALL;
    }
    else if (_wcsicmp(argv[0], L"--rescan") == 0)
    {
        op_mode = OP_MODE_RESCAN;
    }
    else
    {
        return -1;
    }

    BOOL reboot_required = FALSE;
    BOOL result = FALSE;
    if (op_mode == OP_MODE_INSTALL)
    {
        result = ImScsiInstallDriver(argv[1], NULL, &reboot_required);
    }
    else if (op_mode == OP_MODE_UNINSTALL)
    {
        puts("Removing devices...");

        int number_of_removed_devices = ImScsiRemoveDevices(NULL);

        if ((number_of_removed_devices == 0) &&
            (GetLastError() != NO_ERROR))
        {
            PrintLastError(
                L"Error removing devices. Reboot is required to finish uninstall.%n");
        }
        else
        {
            printf("%i device(s) removed.\n", number_of_removed_devices);
        }

        puts("Removing driver...");

        result = ImScsiRemoveDriver(&reboot_required);
    }
    else if (op_mode == OP_MODE_RESCAN)
    {
        puts("Rescanning adapter...");
        
        result = ImScsiRescanScsiAdapter();
    }
    else
    {
        fprintf(stderr,
            "Syntax:\n"
            "aim_ll --install source_directory\n"
            "aim_ll --uninstall\n"
            "aim_ll --rescan\n");

        return -1;
    }

    if (reboot_required)
    {
        puts("System restart is required to complete setup.");

        return 1;
    }
    else if (result)
    {
        puts("Finished successfully.");

        return 0;
    }
    else if (GetLastError() == ERROR_IN_WOW64)
    {
        ImScsiOemPrintF(stderr,
            "Setup failed. Use 64 bit version of this application to install on 64 bit versions of Windows.");

        return 2;
    }
    else
    {
        PrintLastError(L"Setup failed:");

        return 3;
    }
}
