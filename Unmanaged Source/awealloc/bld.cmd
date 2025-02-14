for %%v in (W2K)              do for %%a in (x86)        do start autobld.cmd C:\WinDDK\6000\         %1 %%a %%v
for %%v in (WXP)              do for %%a in (x86)        do start autobld.cmd C:\WINDDK\7600.16385.1\ %1 %%a %%v no_oacr
for %%v in (WNet WLH)         do for %%a in (x86 x64 64) do start autobld.cmd C:\WINDDK\7600.16385.1\ %1 %%a %%v no_oacr
for %%v in (Win7)             do for %%a in (64)         do start autobld.cmd C:\WINDDK\7600.16385.1\ %1 %%a %%v no_oacr
@
for %%v in (Win7 Win8 Win8.1) do for %%a in (x86 x64)    do @call :copy %1 %2 %%v %%a
for %%v in (Win8 Win8.1)      do for %%a in (arm)        do @call :copy %1 %2 %%v %%a
for %%v in (Win8.1)           do for %%a in (arm64)      do @call :copy %1 %2 %%v %%a
@
@goto :eof

:copy
xcopy /d /y %3%2\%4\awealloc.sys ..\dist\%1\%3\%4\
xcopy /d /y %3%2\%4\awealloc.pdb ..\dist\%1\%3\%4\
@
