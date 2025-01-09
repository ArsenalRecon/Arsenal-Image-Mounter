/*++

Copyright (C) Microsoft Corporation, 1991 - 1999

Module Name:

debug.c

Abstract:

CLASSPNP debug code and data

Environment:

kernel mode only

Notes:


Revision History:

--*/

#if DBG

#include "phdskmnt.h"

char *DbgGetScsiOpStr(PSCSI_REQUEST_BLOCK Srb)
{
    PCDB pCdb = (PCDB)Srb->Cdb;
    UCHAR scsiOp = pCdb->CDB6GENERIC.OperationCode;
    char* scsiOpStr = "(UNKNOWN)";

    switch (scsiOp) {

#undef MAKE_CASE             
#define MAKE_CASE(scsiOpCode) case scsiOpCode: scsiOpStr = #scsiOpCode; break;

        MAKE_CASE(SCSIOP_TEST_UNIT_READY)
            MAKE_CASE(SCSIOP_REWIND)    // aka SCSIOP_REZERO_UNIT
            MAKE_CASE(SCSIOP_REQUEST_BLOCK_ADDR)
            MAKE_CASE(SCSIOP_REQUEST_SENSE)
            MAKE_CASE(SCSIOP_FORMAT_UNIT)
            MAKE_CASE(SCSIOP_READ_BLOCK_LIMITS)
            MAKE_CASE(SCSIOP_INIT_ELEMENT_STATUS)   // aka SCSIOP_REASSIGN_BLOCKS
            MAKE_CASE(SCSIOP_RECEIVE)       // aka SCSIOP_READ6
            MAKE_CASE(SCSIOP_SEND)  // aka SCSIOP_WRITE6, SCSIOP_PRINT
            MAKE_CASE(SCSIOP_SLEW_PRINT)    // aka SCSIOP_SEEK6, SCSIOP_TRACK_SELECT
            MAKE_CASE(SCSIOP_SEEK_BLOCK)
            MAKE_CASE(SCSIOP_PARTITION)
            MAKE_CASE(SCSIOP_READ_REVERSE)
            MAKE_CASE(SCSIOP_FLUSH_BUFFER)      // aka SCSIOP_WRITE_FILEMARKS
            MAKE_CASE(SCSIOP_SPACE)
            MAKE_CASE(SCSIOP_INQUIRY)
            MAKE_CASE(SCSIOP_VERIFY6)
            MAKE_CASE(SCSIOP_RECOVER_BUF_DATA)
            MAKE_CASE(SCSIOP_MODE_SELECT)
            MAKE_CASE(SCSIOP_RESERVE_UNIT)
            MAKE_CASE(SCSIOP_RELEASE_UNIT)
            MAKE_CASE(SCSIOP_COPY)
            MAKE_CASE(SCSIOP_ERASE)
            MAKE_CASE(SCSIOP_MODE_SENSE)
            MAKE_CASE(SCSIOP_START_STOP_UNIT)   // aka SCSIOP_STOP_PRINT, SCSIOP_LOAD_UNLOAD
            MAKE_CASE(SCSIOP_RECEIVE_DIAGNOSTIC)
            MAKE_CASE(SCSIOP_SEND_DIAGNOSTIC)
            MAKE_CASE(SCSIOP_MEDIUM_REMOVAL)
            MAKE_CASE(SCSIOP_READ_FORMATTED_CAPACITY)
            MAKE_CASE(SCSIOP_READ_CAPACITY)
            MAKE_CASE(SCSIOP_READ)
            MAKE_CASE(SCSIOP_WRITE)
            MAKE_CASE(SCSIOP_SEEK)  // aka SCSIOP_LOCATE, SCSIOP_POSITION_TO_ELEMENT
            MAKE_CASE(SCSIOP_WRITE_VERIFY)
            MAKE_CASE(SCSIOP_VERIFY)
            MAKE_CASE(SCSIOP_SEARCH_DATA_HIGH)
            MAKE_CASE(SCSIOP_SEARCH_DATA_EQUAL)
            MAKE_CASE(SCSIOP_SEARCH_DATA_LOW)
            MAKE_CASE(SCSIOP_SET_LIMITS)
            MAKE_CASE(SCSIOP_READ_POSITION)
            MAKE_CASE(SCSIOP_SYNCHRONIZE_CACHE)
            MAKE_CASE(SCSIOP_COMPARE)
            MAKE_CASE(SCSIOP_COPY_COMPARE)
            MAKE_CASE(SCSIOP_WRITE_DATA_BUFF)
            MAKE_CASE(SCSIOP_READ_DATA_BUFF)
            MAKE_CASE(SCSIOP_CHANGE_DEFINITION)
            MAKE_CASE(SCSIOP_READ_SUB_CHANNEL)
            MAKE_CASE(SCSIOP_READ_TOC)
            MAKE_CASE(SCSIOP_READ_HEADER)
            MAKE_CASE(SCSIOP_PLAY_AUDIO)
            MAKE_CASE(SCSIOP_GET_CONFIGURATION)
            MAKE_CASE(SCSIOP_PLAY_AUDIO_MSF)
            MAKE_CASE(SCSIOP_PLAY_TRACK_INDEX)
            MAKE_CASE(SCSIOP_PLAY_TRACK_RELATIVE)
            MAKE_CASE(SCSIOP_GET_EVENT_STATUS)
            MAKE_CASE(SCSIOP_PAUSE_RESUME)
            MAKE_CASE(SCSIOP_LOG_SELECT)
            MAKE_CASE(SCSIOP_LOG_SENSE)
            MAKE_CASE(SCSIOP_STOP_PLAY_SCAN)
            MAKE_CASE(SCSIOP_READ_DISK_INFORMATION)
            MAKE_CASE(SCSIOP_READ_TRACK_INFORMATION)
            MAKE_CASE(SCSIOP_RESERVE_TRACK_RZONE)
            MAKE_CASE(SCSIOP_SEND_OPC_INFORMATION)
            MAKE_CASE(SCSIOP_MODE_SELECT10)
            MAKE_CASE(SCSIOP_MODE_SENSE10)
            MAKE_CASE(SCSIOP_CLOSE_TRACK_SESSION)
            MAKE_CASE(SCSIOP_READ_BUFFER_CAPACITY)
            MAKE_CASE(SCSIOP_SEND_CUE_SHEET)
            MAKE_CASE(SCSIOP_PERSISTENT_RESERVE_IN)
            MAKE_CASE(SCSIOP_PERSISTENT_RESERVE_OUT)
            MAKE_CASE(SCSIOP_REPORT_LUNS)
            MAKE_CASE(SCSIOP_BLANK)
            MAKE_CASE(SCSIOP_SEND_KEY)
            MAKE_CASE(SCSIOP_REPORT_KEY)
            MAKE_CASE(SCSIOP_MOVE_MEDIUM)
            MAKE_CASE(SCSIOP_LOAD_UNLOAD_SLOT)  // aka SCSIOP_EXCHANGE_MEDIUM
            MAKE_CASE(SCSIOP_SET_READ_AHEAD)
            MAKE_CASE(SCSIOP_READ_DVD_STRUCTURE)
            MAKE_CASE(SCSIOP_SEND_VOLUME_TAG)
            MAKE_CASE(SCSIOP_READ_ELEMENT_STATUS)
            MAKE_CASE(SCSIOP_READ_CD_MSF)
            MAKE_CASE(SCSIOP_SCAN_CD)
            MAKE_CASE(SCSIOP_SET_CD_SPEED)
            MAKE_CASE(SCSIOP_PLAY_CD)
            MAKE_CASE(SCSIOP_MECHANISM_STATUS)
            MAKE_CASE(SCSIOP_READ_CD)
            MAKE_CASE(SCSIOP_SEND_DVD_STRUCTURE)
            MAKE_CASE(SCSIOP_INIT_ELEMENT_RANGE)
#ifdef SCSIOP_SECURITY_PROTOCOL_IN
            MAKE_CASE(SCSIOP_SECURITY_PROTOCOL_IN)
#endif
#ifdef SCSIOP_SECURITY_PROTOCOL_OUT
            MAKE_CASE(SCSIOP_SECURITY_PROTOCOL_OUT)
#endif
    }

    return scsiOpStr;
}


char *DbgGetSrbStatusStr(PSCSI_REQUEST_BLOCK Srb)
{
    char *srbStatStr = "?";

    switch (Srb->SrbStatus) {

#undef MAKE_CASE
#define MAKE_CASE(srbStat) \
                        case srbStat: \
                            srbStatStr = #srbStat; \
                            break; \
                        case srbStat|SRB_STATUS_QUEUE_FROZEN: \
                            srbStatStr = #srbStat "|SRB_STATUS_QUEUE_FROZEN"; \
                            break; \
                        case srbStat|SRB_STATUS_AUTOSENSE_VALID: \
                            srbStatStr = #srbStat "|SRB_STATUS_AUTOSENSE_VALID"; \
                            break; \
                        case srbStat|SRB_STATUS_QUEUE_FROZEN|SRB_STATUS_AUTOSENSE_VALID: \
                            srbStatStr = #srbStat "|SRB_STATUS_QUEUE_FROZEN|SRB_STATUS_AUTOSENSE_VALID"; \
                            break; 

        MAKE_CASE(SRB_STATUS_PENDING)
            MAKE_CASE(SRB_STATUS_SUCCESS)
            MAKE_CASE(SRB_STATUS_ABORTED)
            MAKE_CASE(SRB_STATUS_ABORT_FAILED)
            MAKE_CASE(SRB_STATUS_ERROR)
            MAKE_CASE(SRB_STATUS_BUSY)
            MAKE_CASE(SRB_STATUS_INVALID_REQUEST)
            MAKE_CASE(SRB_STATUS_INVALID_PATH_ID)
            MAKE_CASE(SRB_STATUS_NO_DEVICE)
            MAKE_CASE(SRB_STATUS_TIMEOUT)
            MAKE_CASE(SRB_STATUS_SELECTION_TIMEOUT)
            MAKE_CASE(SRB_STATUS_COMMAND_TIMEOUT)
            MAKE_CASE(SRB_STATUS_MESSAGE_REJECTED)
            MAKE_CASE(SRB_STATUS_BUS_RESET)
            MAKE_CASE(SRB_STATUS_PARITY_ERROR)
            MAKE_CASE(SRB_STATUS_REQUEST_SENSE_FAILED)
            MAKE_CASE(SRB_STATUS_NO_HBA)
            MAKE_CASE(SRB_STATUS_DATA_OVERRUN)
            MAKE_CASE(SRB_STATUS_UNEXPECTED_BUS_FREE)
            MAKE_CASE(SRB_STATUS_PHASE_SEQUENCE_FAILURE)
            MAKE_CASE(SRB_STATUS_BAD_SRB_BLOCK_LENGTH)
            MAKE_CASE(SRB_STATUS_REQUEST_FLUSHED)
            MAKE_CASE(SRB_STATUS_INVALID_LUN)
            MAKE_CASE(SRB_STATUS_INVALID_TARGET_ID)
            MAKE_CASE(SRB_STATUS_BAD_FUNCTION)
            MAKE_CASE(SRB_STATUS_ERROR_RECOVERY)
            MAKE_CASE(SRB_STATUS_NOT_POWERED)
            MAKE_CASE(SRB_STATUS_INTERNAL_ERROR)
    }

    return srbStatStr;
}


char *DbgGetSenseCodeStr(PSCSI_REQUEST_BLOCK Srb)
{
    char *senseCodeStr = "?";

    if (Srb->SrbStatus & SRB_STATUS_AUTOSENSE_VALID) {
        PSENSE_DATA senseData;
        UCHAR senseCode;

        ASSERT(Srb->SenseInfoBuffer);
        senseData = (PSENSE_DATA)Srb->SenseInfoBuffer;
        senseCode = senseData->SenseKey & 0xf;

        switch (senseCode) {

#undef MAKE_CASE             
#define MAKE_CASE(snsCod) case snsCod: senseCodeStr = #snsCod; break;

            MAKE_CASE(SCSI_SENSE_NO_SENSE)
                MAKE_CASE(SCSI_SENSE_RECOVERED_ERROR)
                MAKE_CASE(SCSI_SENSE_NOT_READY)
                MAKE_CASE(SCSI_SENSE_MEDIUM_ERROR)
                MAKE_CASE(SCSI_SENSE_HARDWARE_ERROR)
                MAKE_CASE(SCSI_SENSE_ILLEGAL_REQUEST)
                MAKE_CASE(SCSI_SENSE_UNIT_ATTENTION)
                MAKE_CASE(SCSI_SENSE_DATA_PROTECT)
                MAKE_CASE(SCSI_SENSE_BLANK_CHECK)
                MAKE_CASE(SCSI_SENSE_UNIQUE)
                MAKE_CASE(SCSI_SENSE_COPY_ABORTED)
                MAKE_CASE(SCSI_SENSE_ABORTED_COMMAND)
                MAKE_CASE(SCSI_SENSE_EQUAL)
                MAKE_CASE(SCSI_SENSE_VOL_OVERFLOW)
                MAKE_CASE(SCSI_SENSE_MISCOMPARE)
                MAKE_CASE(SCSI_SENSE_RESERVED)
        }
    }

    return senseCodeStr;
}


char *DbgGetAdditionalSenseCodeStr(PSCSI_REQUEST_BLOCK Srb)
{
    char *adSenseCodeStr = "?";

    if (Srb->SrbStatus & SRB_STATUS_AUTOSENSE_VALID) {
        PSENSE_DATA senseData;
        UCHAR adSenseCode;

        ASSERT(Srb->SenseInfoBuffer);
        senseData = (PSENSE_DATA)Srb->SenseInfoBuffer;
        adSenseCode = senseData->AdditionalSenseCode;

        switch (adSenseCode) {

#undef MAKE_CASE             
#define MAKE_CASE(adSnsCod) case adSnsCod: adSenseCodeStr = #adSnsCod; break;

            MAKE_CASE(SCSI_ADSENSE_NO_SENSE)
                MAKE_CASE(SCSI_ADSENSE_LUN_NOT_READY)
                MAKE_CASE(SCSI_ADSENSE_TRACK_ERROR)
                MAKE_CASE(SCSI_ADSENSE_SEEK_ERROR)
                MAKE_CASE(SCSI_ADSENSE_REC_DATA_NOECC)
                MAKE_CASE(SCSI_ADSENSE_REC_DATA_ECC)
                MAKE_CASE(SCSI_ADSENSE_ILLEGAL_COMMAND)
                MAKE_CASE(SCSI_ADSENSE_ILLEGAL_BLOCK)
                MAKE_CASE(SCSI_ADSENSE_INVALID_CDB)
                MAKE_CASE(SCSI_ADSENSE_INVALID_LUN)
                MAKE_CASE(SCSI_ADSENSE_WRITE_PROTECT)   // aka SCSI_ADWRITE_PROTECT
                MAKE_CASE(SCSI_ADSENSE_MEDIUM_CHANGED)
                MAKE_CASE(SCSI_ADSENSE_BUS_RESET)
                MAKE_CASE(SCSI_ADSENSE_INVALID_MEDIA)
                MAKE_CASE(SCSI_ADSENSE_NO_MEDIA_IN_DEVICE)
                MAKE_CASE(SCSI_ADSENSE_POSITION_ERROR)
                MAKE_CASE(SCSI_ADSENSE_OPERATOR_REQUEST)
                MAKE_CASE(SCSI_ADSENSE_FAILURE_PREDICTION_THRESHOLD_EXCEEDED)
                MAKE_CASE(SCSI_ADSENSE_COPY_PROTECTION_FAILURE)
                MAKE_CASE(SCSI_ADSENSE_VENDOR_UNIQUE)
                MAKE_CASE(SCSI_ADSENSE_MUSIC_AREA)
                MAKE_CASE(SCSI_ADSENSE_DATA_AREA)
                MAKE_CASE(SCSI_ADSENSE_VOLUME_OVERFLOW)
        }
    }

    return adSenseCodeStr;
}


char *DbgGetAdditionalSenseCodeQualifierStr(PSCSI_REQUEST_BLOCK Srb)
{
    char *adSenseCodeQualStr = "?";

    if (Srb->SrbStatus & SRB_STATUS_AUTOSENSE_VALID) {
        PSENSE_DATA senseData;
        UCHAR adSenseCode;
        UCHAR adSenseCodeQual;

        ASSERT(Srb->SenseInfoBuffer);
        senseData = (PSENSE_DATA)Srb->SenseInfoBuffer;
        adSenseCode = senseData->AdditionalSenseCode;
        adSenseCodeQual = senseData->AdditionalSenseCodeQualifier;

        switch (adSenseCode) {

#undef MAKE_CASE             
#define MAKE_CASE(adSnsCodQual) case adSnsCodQual: adSenseCodeQualStr = #adSnsCodQual; break;

        case SCSI_ADSENSE_LUN_NOT_READY:
            switch (adSenseCodeQual) {
                MAKE_CASE(SCSI_SENSEQ_CAUSE_NOT_REPORTABLE)
                    MAKE_CASE(SCSI_SENSEQ_BECOMING_READY)
                    MAKE_CASE(SCSI_SENSEQ_INIT_COMMAND_REQUIRED)
                    MAKE_CASE(SCSI_SENSEQ_MANUAL_INTERVENTION_REQUIRED)
                    MAKE_CASE(SCSI_SENSEQ_FORMAT_IN_PROGRESS)
                    MAKE_CASE(SCSI_SENSEQ_REBUILD_IN_PROGRESS)
                    MAKE_CASE(SCSI_SENSEQ_RECALCULATION_IN_PROGRESS)
                    MAKE_CASE(SCSI_SENSEQ_OPERATION_IN_PROGRESS)
                    MAKE_CASE(SCSI_SENSEQ_LONG_WRITE_IN_PROGRESS)
            }
            break;
        case SCSI_ADSENSE_NO_SENSE:
            switch (adSenseCodeQual) {
                MAKE_CASE(SCSI_SENSEQ_FILEMARK_DETECTED)
                    MAKE_CASE(SCSI_SENSEQ_END_OF_MEDIA_DETECTED)
                    MAKE_CASE(SCSI_SENSEQ_SETMARK_DETECTED)
                    MAKE_CASE(SCSI_SENSEQ_BEGINNING_OF_MEDIA_DETECTED)
            }
            break;
        case SCSI_ADSENSE_ILLEGAL_BLOCK:
            switch (adSenseCodeQual) {
                MAKE_CASE(SCSI_SENSEQ_ILLEGAL_ELEMENT_ADDR)
            }
            break;
        case SCSI_ADSENSE_POSITION_ERROR:
            switch (adSenseCodeQual) {
                MAKE_CASE(SCSI_SENSEQ_DESTINATION_FULL)
                    MAKE_CASE(SCSI_SENSEQ_SOURCE_EMPTY)
            }
            break;
        case SCSI_ADSENSE_INVALID_MEDIA:
            switch (adSenseCodeQual) {
                MAKE_CASE(SCSI_SENSEQ_INCOMPATIBLE_MEDIA_INSTALLED)
                    MAKE_CASE(SCSI_SENSEQ_UNKNOWN_FORMAT)
                    MAKE_CASE(SCSI_SENSEQ_INCOMPATIBLE_FORMAT)
                    MAKE_CASE(SCSI_SENSEQ_CLEANING_CARTRIDGE_INSTALLED)
            }
            break;
        case SCSI_ADSENSE_OPERATOR_REQUEST:
            switch (adSenseCodeQual) {
                MAKE_CASE(SCSI_SENSEQ_STATE_CHANGE_INPUT)
                    MAKE_CASE(SCSI_SENSEQ_MEDIUM_REMOVAL)
                    MAKE_CASE(SCSI_SENSEQ_WRITE_PROTECT_ENABLE)
                    MAKE_CASE(SCSI_SENSEQ_WRITE_PROTECT_DISABLE)
            }
            break;
        case SCSI_ADSENSE_COPY_PROTECTION_FAILURE:
            switch (adSenseCodeQual) {
                MAKE_CASE(SCSI_SENSEQ_AUTHENTICATION_FAILURE)
                    MAKE_CASE(SCSI_SENSEQ_KEY_NOT_PRESENT)
                    MAKE_CASE(SCSI_SENSEQ_KEY_NOT_ESTABLISHED)
                    MAKE_CASE(SCSI_SENSEQ_READ_OF_SCRAMBLED_SECTOR_WITHOUT_AUTHENTICATION)
                    MAKE_CASE(SCSI_SENSEQ_MEDIA_CODE_MISMATCHED_TO_LOGICAL_UNIT)
                    MAKE_CASE(SCSI_SENSEQ_LOGICAL_UNIT_RESET_COUNT_ERROR)
            }
            break;
        }
    }

    return adSenseCodeQualStr;
}


#endif 


