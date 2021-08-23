
/// utils.c
/// Retrieves registry parameter values in a structure, and set default
/// values on those fields not defined in registry.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#include "phdskmnt.h"

VOID
#pragma warning(suppress: 6101)
MpQueryRegParameters(
__in __deref    PUNICODE_STRING pRegistryPath,
__out __deref   pMP_REG_INFO    pRegInfo
)
/*++

Routine Description:

This routine is called from DriverEntry to get parameters from the registry.  If the registry query
fails, default values are used.

Return Value:

None

--*/
{
    MP_REG_INFO defRegInfo;

    // Set default values.

    defRegInfo.NumberOfBuses = DEFAULT_NUMBER_OF_BUSES;
    defRegInfo.InitiatorID = DEFAULT_INITIATOR_ID;

    RtlInitUnicodeString(&defRegInfo.VendorId, VENDOR_ID);
    RtlInitUnicodeString(&defRegInfo.ProductId, PRODUCT_ID);
    RtlInitUnicodeString(&defRegInfo.ProductRevision, PRODUCT_REV);

    // The initialization of lclRtlQueryRegTbl is put into a subordinate block so that the initialized Buffer members of Unicode strings
    // in defRegInfo will be used.

    {
        NTSTATUS                 status;

#pragma warning(push)
#pragma warning(disable : 4204)
#pragma warning(disable : 4221)

        RTL_QUERY_REGISTRY_TABLE lclRtlQueryRegTbl[] = {
            // The Parameters entry causes the registry to be searched under that subkey for the subsequent set of entries.
            { NULL, RTL_QUERY_REGISTRY_SUBKEY | RTL_QUERY_REGISTRY_NOEXPAND, L"Parameters", NULL, (ULONG_PTR)NULL, NULL, (ULONG_PTR)NULL },

            { NULL, RTL_QUERY_REGISTRY_DIRECT | RTL_QUERY_REGISTRY_NOEXPAND, L"NumberOfBuses", &pRegInfo->NumberOfBuses, REG_DWORD, &defRegInfo.NumberOfBuses, sizeof(ULONG) },
            { NULL, RTL_QUERY_REGISTRY_DIRECT | RTL_QUERY_REGISTRY_NOEXPAND, L"InitiatorID", &pRegInfo->InitiatorID, REG_DWORD, &defRegInfo.InitiatorID, sizeof(ULONG) },
            { NULL, RTL_QUERY_REGISTRY_DIRECT | RTL_QUERY_REGISTRY_NOEXPAND, L"VendorId", &pRegInfo->VendorId, REG_SZ, defRegInfo.VendorId.Buffer, 0 },
            { NULL, RTL_QUERY_REGISTRY_DIRECT | RTL_QUERY_REGISTRY_NOEXPAND, L"ProductId", &pRegInfo->ProductId, REG_SZ, defRegInfo.ProductId.Buffer, 0 },
            { NULL, RTL_QUERY_REGISTRY_DIRECT | RTL_QUERY_REGISTRY_NOEXPAND, L"ProductRevision", &pRegInfo->ProductRevision, REG_SZ, defRegInfo.ProductRevision.Buffer, 0 },

            // The null entry denotes the end of the array.                                                                    
            { NULL, 0, NULL, NULL, (ULONG_PTR)NULL, NULL, (ULONG_PTR)NULL },
        };

#pragma warning(pop)

        status = RtlQueryRegistryValues(
            RTL_REGISTRY_ABSOLUTE | RTL_REGISTRY_OPTIONAL,
            pRegistryPath->Buffer,
            lclRtlQueryRegTbl,
            NULL,
            NULL
            );

        if (!NT_SUCCESS(status)) {                    // A problem?
            pRegInfo->NumberOfBuses = defRegInfo.NumberOfBuses;
            pRegInfo->InitiatorID = defRegInfo.InitiatorID;
            RtlCopyUnicodeString(&pRegInfo->VendorId, &defRegInfo.VendorId);
            RtlCopyUnicodeString(&pRegInfo->ProductId, &defRegInfo.ProductId);
            RtlCopyUnicodeString(&pRegInfo->ProductRevision, &defRegInfo.ProductRevision);
        }
    }
}                                                     // End MpQueryRegParameters().

