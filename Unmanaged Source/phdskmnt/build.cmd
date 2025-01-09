if "%DDKBUILDENV%" == "" call C:\WinDDK\7600.16385.1\bin\setenv.bat C:\WinDDK\7600.16385.1\ chk x86 WXP
echo on
cd /d %~dp0
@echo Invoking: build.exe -cegiw -nmake -i %*
build.exe -cegiw -nmake -i %*
exit /b %ERRORLEVEL%