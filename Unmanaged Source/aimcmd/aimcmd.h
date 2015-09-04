
/// aimcmd.h
/// Common declarations for files in aimcmd.exe project.
/// 
/// Copyright (c) 2012-2015, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

// Prints out a FormatMessage style parameterized message to specified stream.
BOOL
ImScsiOemPrintF(FILE *Stream, LPCSTR Message, ...);

// Writes out to console a message followed by system error message
// corresponding to current "last error" code from Win32 API.
void
PrintLastError(LPCWSTR Prefix = NULL);

LPVOID
ImScsiCliAssertNotNull(LPVOID Ptr);

int
wmainSetup(int, wchar_t **argv);
