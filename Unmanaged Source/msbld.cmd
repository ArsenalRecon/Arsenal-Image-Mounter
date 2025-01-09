set MSBUILD_EXE="C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe"
for %%c in (Debug Release) do (
	for %%v in (Win7 Win8 Win8.1) do for %%a in (Win32 x64)  do ( %MSBUILD_EXE% /m /p:configuration="%%v %%c" /p:platform=%%a || goto :eof )
	for %%v in (Win8 Win8.1)      do for %%a in (ARM)        do ( %MSBUILD_EXE% /m /p:configuration="%%v %%c" /p:platform=%%a || goto :eof )
	for %%v in (Win8.1)           do for %%a in (ARM64)      do ( %MSBUILD_EXE% /m /p:configuration="%%v %%c" /p:platform=%%a || goto :eof )
)
@