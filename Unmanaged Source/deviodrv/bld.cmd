for %%v in (W2K)              do for %%a in (x86)         do start autobld.cmd C:\WinDDK\6000\         %1 %%a %%v
for %%v in (WXP)              do for %%a in (x86)         do start autobld.cmd C:\WINDDK\7600.16385.1\ %1 %%a %%v no_oacr
for %%v in (WNET WLH Win7)    do for %%a in (x86 x64 64)  do start autobld.cmd C:\WINDDK\7600.16385.1\ %1 %%a %%v no_oacr
@
for %%v in (Win8 Win8.1)      do for %%a in (x86 x64 arm) do @call :copy %1 %2 %%v %%a
for %%v in (Win8.1)           do for %%a in (arm64)       do @call :copy %1 %2 %%v %%a
@
@goto :eof

:copy
xcopy /d /y %2\%4\deviodrv.sys ..\dist\%1\%3\%4\
xcopy /d /y %2\%4\deviodrv.pdb ..\dist\%1\%3\%4\
@
