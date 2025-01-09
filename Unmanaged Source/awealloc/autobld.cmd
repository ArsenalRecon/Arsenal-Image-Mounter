set STARTDIR=%CD%
call %1bin\setenv.bat %*

set SIGNTOOL="C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe" sign /a /n "Arsenal Consulting, Inc." /d "Arsenal Image Mounter Miniport" /du "http://arsenalrecon.com" /ac "..\GlobalSign Root CA.crt" /fd SHA256 /td sha256 /tr "http://rfc3161timestamp.globalsign.com/advanced"

echo on
cd /d %STARTDIR%
build.exe %BUILD_DEFAULT% -cwZg || goto :eof
for /f %%b in ('dir /s/b obj%BUILD_ALT_DIR%\*.sys') do (
  %SIGNTOOL% "%%b" || goto :eof
  xcopy /d /y %%b "..\dist\%DDKBUILDENV%\%DDK_TARGET_OS%\%3\" || goto :eof
  xcopy /d /y %%~dpnb.pdb "..\dist\%DDKBUILDENV%\%DDK_TARGET_OS%\%3\" || goto :eof
)

exit