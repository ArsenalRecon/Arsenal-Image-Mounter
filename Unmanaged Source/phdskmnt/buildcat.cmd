setlocal

@echo on

set WORKDIR=%CD%

set SIGNTOOL="C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe" sign /a /n "Arsenal Consulting, Inc." /d "Arsenal Image Mounter Miniport" /du "http://arsenalrecon.com" /ac "..\GlobalSign Root CA.crt" /fd SHA256 /td sha256 /tr "http://rfc3161timestamp.globalsign.com/advanced"

if "%STAMPINF_DATE%"    == "" set STAMPINF_DATE=*
if "%STAMPINF_VERSION%" == "" set STAMPINF_VERSION=*
set STAMPINF=stampinf -d %STAMPINF_DATE% -v %STAMPINF_VERSION%

set PATH=C:\Program Files (x86)\Windows Kits\10\bin\x86;%PATH%

where stampinf || goto :eof
where inf2cat || goto :eof
where signtool || goto :eof

cd /d %WORKDIR%

for %%d in (Win2K WinXP) do if exist "..\dist\%1\%%d" (
  copy /y legacy.inf "..\dist\%1\%%d\phdskmnt.inf"
  %STAMPINF% -f "..\dist\%1\%%d\phdskmnt.inf" -a NTx86
)

for %%d in (CtlUnit) do (
  xcopy /y ctlunit.inf ..\dist\%1\%%d\
  %STAMPINF% -f "..\dist\%1\%%d\ctlunit.inf" -a NTx86
  inf2cat /driver:"..\dist\%1\%%d" /os:XP_X86,2000 || goto :eof
  %SIGNTOOL% "..\dist\%1\%%d\ctlunit.cat" || goto :eof
)

for %%d in (WinNET) do if exist "..\dist\%1\%%d" (
  copy /y phdskmnt2003.inf "..\dist\%1\%%d\phdskmnt.inf"
  %STAMPINF% -f "..\dist\%1\%%d\phdskmnt.inf" -a NTx86,NTia64,NTamd64
  inf2cat /driver:"..\dist\%1\%%d" /os:XP_X64,Server2003_X86,Server2003_X64,Server2003_IA64 || goto :eof
  %SIGNTOOL% "..\dist\%1\%%d\phdskmnt.cat" || goto :eof
)

for %%d in (WinLH) do if exist "..\dist\%1\%%d" (
  copy /y phdskmntVista.inf "..\dist\%1\%%d\phdskmnt.inf"
  %STAMPINF% -f "..\dist\%1\%%d\phdskmnt.inf" -a NTx86,NTia64,NTamd64
  inf2cat /driver:"..\dist\%1\%%d" /os:Vista_X86,Vista_X64,7_X86,7_X64,Server2008_X86,Server2008_X64,Server2008_IA64,Server2008R2_X64,Server2008R2_IA64 || goto :eof
  %SIGNTOOL% "..\dist\%1\%%d\phdskmnt.cat" || goto :eof
)

for %%d in (Win7) do if exist "..\dist\%1\%%d" (
  copy /y phdskmntWin7.inf "..\dist\%1\%%d\phdskmnt.inf"
  %STAMPINF% -f "..\dist\%1\%%d\phdskmnt.inf" -a NTx86,NTia64,NTamd64
  inf2cat /driver:"..\dist\%1\%%d" /os:Vista_X86,Vista_X64,7_X86,7_X64,Server2008_X86,Server2008_X64,Server2008_IA64,Server2008R2_X64,Server2008R2_IA64 || goto :eof
  %SIGNTOOL% "..\dist\%1\%%d\phdskmnt.cat" || goto :eof
)

for %%d in (Win8) do if exist "..\dist\%1\%%d" (
  copy /y phdskmnt.inf "..\dist\%1\%%d\phdskmnt.inf"
  %STAMPINF% -f "..\dist\%1\%%d\phdskmnt.inf" -a NTx86,NTamd64,NTARM
  inf2cat /driver:"..\dist\%1\%%d" /os:8_X86,8_X64,8_ARM,Server8_X64 || goto :eof
  %SIGNTOOL% "..\dist\%1\%%d\phdskmnt.cat" || goto :eof
)

for %%d in (Win8.1) do if exist "..\dist\%1\%%d" (
  copy /y phdskmnt.inf "..\dist\%1\%%d\phdskmnt.inf"
  %STAMPINF% -f "..\dist\%1\%%d\phdskmnt.inf" -a NTx86,NTamd64,NTARM,NTARM64
  inf2cat /driver:"..\dist\%1\%%d" /os:6_3_X86,6_3_X64,6_3_ARM,Server6_3_X64,10_VB_X86,10_VB_X64,10_VB_ARM64,10_NI_X64,10_NI_ARM64,ServerFE_X64,ServerFE_ARM64 || goto :eof
  %SIGNTOOL% "..\dist\%1\%%d\phdskmnt.cat" || goto :eof
)

endlocal

cd ..\dist\%1