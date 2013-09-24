
### sources
### Definitions for use with WINDDK build.exe.
### 
### Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
### This source code is available under the terms of the Affero General Public
### License v3.
###
### Please see LICENSE.txt for full license terms, including the availability of
### proprietary exceptions.
### Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
###


LNG=$(LANGUAGE)
_INX=.
STAMP=stampinf -n -f $@ -a $(_BUILDARCH)

!IF $(_NT_TARGET_VERSION) >= 0x0502

$(OBJ_PATH)\$(O)\$(INF_NAME).inf: $(_INX)\$(INF_NAME).inx 
    copy $(_INX)\$(@B).inx $@
    $(STAMP)

!ELSE

$(OBJ_PATH)\$(O)\$(INF_NAME).inf: $(_INX)\legacy.inf 
    copy $(_INX)\legacy.inf $@
    copy $(_INX)\inst.cmd $(OBJ_PATH)\$(O)
    copy $(_INX)\uninst.cmd $(OBJ_PATH)\$(O)

!ENDIF

$(OBJ_PATH)\$O\hbaapi.mof $(OBJ_PATH)\$O\hbaapi.mfl: hbaapi.mof
## Pre-process .mof file.
    $(C_PREPROCESSOR_NAME) $(C_PREPROCESSOR_FLAGS) hbaapi.mof > $(OBJ_PATH)\$(O)\hbaapi.tmp

## Make a copy, for the mof-compilation step below
    copy $(OBJ_PATH)\$(O)\hbaapi.tmp .\hbaapi.mof

## Compile the base .mof file, which will include the new .mof file.
    mofcomp -WMI -B:$(OBJ_PATH)\$(O)\\phdskmnt.bmf phdskmnt.mof

## Build phdskmntwmi.h
    wmimofck -h.\inc\phdskmntwmi.h -m -u -w.\html $(OBJ_PATH)\$(O)\phdskmnt.bmf

clean:
    del phdskmntdt.h $(O)\phdskmnt.bmf

