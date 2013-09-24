
/// trace.h
/// Definitions for SCSI miniport trace functions.
/// 
/// Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

/**************************************************************************************************/     
/*                                                                                                */     
/* Copyright (c) 2008-2011 Microsoft Corporation.  All Rights Reserved.                           */     
/*                                                                                                */     
/**************************************************************************************************/    

#ifndef _TRACE_H_
#define _TRACE_H_

/*

    WPP_DEFINE_CONTROL_GUID specifies the GUID used for this filter.
    *** REPLACE THE GUID WITH YOUR OWN UNIQUE ID ***
    WPP_DEFINE_BIT allows setting debug bit masks to selectively print.

*/

// In order to send trace statements directly to the kernel 
// debugger, the following two #defines are required. It is
// recommended to always include these.
//
#define WPP_AUTOLOGGER        L"mpStor"
#define WPP_GLOBALLOGGER

// This definition is required. Here, you define the control
// GUID for your driver. This is the GUID any listening
// application interested in receiving trace events from your
// driver will register for.
//
// This is where you categorize your trace statements. A listening
// application may register to listen to only the events in which
// it is interested. So if a listening application only wishes to
// listen for Io and Enum events from your driver. All other
// trace statements will be disabled.
//

#define WPP_CONTROL_GUIDS                                                  \
    WPP_DEFINE_CONTROL_GUID(                                               \
        vvHBA,(C689C5E6,5219,4774,BE15,9B1F92F949FD),                      \
        WPP_DEFINE_BIT(MpDemoDebugError)         /* bit  0 = 0x00000001 */ \
        WPP_DEFINE_BIT(MpDemoDebugWarning)       /* bit  1 = 0x00000002 */ \
        WPP_DEFINE_BIT(MpDemoDebugTrace)         /* bit  2 = 0x00000004 */ \
        WPP_DEFINE_BIT(MpDemoDebugInfo)          /* bit  3 = 0x00000008 */ \
        WPP_DEFINE_BIT(MpDemoDebug04)            /* bit  4 = 0x00000010 */ \
        WPP_DEFINE_BIT(MpDemoDebug05)            /* bit  5 = 0x00000020 */ \
        WPP_DEFINE_BIT(MpDemoDebug06)            /* bit  6 = 0x00000040 */ \
        WPP_DEFINE_BIT(MpDemoDebug07)            /* bit  7 = 0x00000080 */ \
        WPP_DEFINE_BIT(MpDemoDebug08)            /* bit  8 = 0x00000100 */ \
        WPP_DEFINE_BIT(MpDemoDebug09)            /* bit  9 = 0x00000200 */ \
        WPP_DEFINE_BIT(MpDemoDebug10)            /* bit 10 = 0x00000400 */ \
        WPP_DEFINE_BIT(MpDemoDebug11)            /* bit 11 = 0x00000800 */ \
        WPP_DEFINE_BIT(MpDemoDebug12)            /* bit 12 = 0x00001000 */ \
        WPP_DEFINE_BIT(MpDemoDebug13)            /* bit 13 = 0x00002000 */ \
        WPP_DEFINE_BIT(MpDemoDebug14)            /* bit 14 = 0x00004000 */ \
        WPP_DEFINE_BIT(MpDemoDebug15)            /* bit 15 = 0x00008000 */ \
        WPP_DEFINE_BIT(MpDemoDebug16)            /* bit 16 = 0x00010000 */ \
        WPP_DEFINE_BIT(MpDemoDebug17)            /* bit 17 = 0x00020000 */ \
        WPP_DEFINE_BIT(MpDemoDebug18)            /* bit 18 = 0x00040000 */ \
        WPP_DEFINE_BIT(MpDemoDebug19)            /* bit 19 = 0x00080000 */ \
        WPP_DEFINE_BIT(MpDemoDebug20)            /* bit 20 = 0x00100000 */ \
        WPP_DEFINE_BIT(MpDemoDebug21)            /* bit 21 = 0x00200000 */ \
        WPP_DEFINE_BIT(MpDemoDebug22)            /* bit 22 = 0x00400000 */ \
        WPP_DEFINE_BIT(MpDemoDebug23)            /* bit 23 = 0x00800000 */ \
        WPP_DEFINE_BIT(MpDemoDebug24)            /* bit 24 = 0x01000000 */ \
        WPP_DEFINE_BIT(MpDemoDebug25)            /* bit 25 = 0x02000000 */ \
        WPP_DEFINE_BIT(MpDemoDebug26)            /* bit 26 = 0x04000000 */ \
        WPP_DEFINE_BIT(MpDemoDebug27)            /* bit 27 = 0x08000000 */ \
        WPP_DEFINE_BIT(MpDemoDebug28)            /* bit 28 = 0x10000000 */ \
        WPP_DEFINE_BIT(MpDemoDebug29)            /* bit 29 = 0x20000000 */ \
        WPP_DEFINE_BIT(MpDemoDebug30)            /* bit 30 = 0x40000000 */ \
        WPP_DEFINE_BIT(MpDemoDebug31)            /* bit 31 = 0x80000000 */ \
                           )
        
//
// The trace formatting engine understands how to format a
// SCSI_REQUEST_BLOCK and a REQUEST SENSE buffer. To use these
// custom formats, however, you must include the following two
// #defines.
//
#define WPP_LOGSRB(x) WPP_LOGPAIR((x)->Length, (x))
#define WPP_LOGSENSE(x) WPP_LOGPAIR((sizeof(SENSE_DATA)), (x))

//
// The following #defines are optional levels you may utilize to
// further control the level of trace output a listening 
// application will receive from your driver.  You may define up
// to 32 levels to be used in conjunction with the above flag 
// bits to control trace output.
//
#define DbgLvlErr    0x00000001
#define DbgLvlWrn    0x00000002
#define DbgLvlInfo   0x00000003
#define DbgLvlLoud   0x00000004

//
// To use levels and flag bits together to control tracing, the
// following two #defines are required.  By default, WPP tracing
// only uses the flag bits defined with the control GUID 
//
#define WPP_LEVEL_FLAGS_LOGGER(lvl,flags) WPP_LEVEL_LOGGER(flags)
#define WPP_LEVEL_FLAGS_ENABLED(lvl,flags) (WPP_LEVEL_ENABLED(flags) && WPP_CONTROL(WPP_BIT_ ## flags).Level & lvl)

//*********************************************************
// MACRO: DoStorageTraceEtw
//
// begin_wpp config
// USEPREFIX (DoStorageTraceEtw,"%!STDPREFIX!");
// FUNC DoStorageTraceEtw(LVL, FLG, MSG, ...);
// end_wpp

#define WPP_LVL_FLG_PRE(LVL, FLG)
#define WPP_LVL_FLG_POST(LVL, FLG) 
#define WPP_LVL_FLG_ENABLED(LVL, FLG) WPP_LEVEL_FLAGS_ENABLED(LVL,FLG)
#define WPP_LVL_FLG_LOGGER(LVL, FLG) WPP_LEVEL_FLAGS_LOGGER(LVL,FLG)

//
// To handle NULL strings in trace arguements
//
#define WPP_CHECK_FOR_NULL_STRING 
              
#endif // _TRACE_H_
        
