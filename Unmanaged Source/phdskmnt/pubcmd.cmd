@echo off

xcopy /d/y ..\Release\aim_ll.exe ..\dist\fre\cli\x86\
xcopy /d/y ..\Release\aim_ll.pdb ..\dist\fre\cli\x86\
xcopy /d/y ..\Release\aimapi.dll ..\dist\fre\cli\x86\
xcopy /d/y ..\Release\aimapi.pdb ..\dist\fre\cli\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.cpl ..\dist\fre\cli\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.pdb ..\dist\fre\cli\x86\
xcopy /d/y ..\Release\aimapi.dll ..\dist\fre\api\x86\
xcopy /d/y ..\Release\aimapi.pdb ..\dist\fre\api\x86\
xcopy /d/y ..\Release\aimapi.lib ..\dist\fre\api\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.cpl ..\dist\fre\api\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.pdb ..\dist\fre\api\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.lib ..\dist\fre\api\x86\

xcopy /d/y ..\x64\Release\aim_ll.exe ..\dist\fre\cli\x64\
xcopy /d/y ..\x64\Release\aim_ll.pdb ..\dist\fre\cli\x64\
xcopy /d/y ..\x64\Release\aimapi.dll ..\dist\fre\cli\x64\
xcopy /d/y ..\x64\Release\aimapi.pdb ..\dist\fre\cli\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.cpl ..\dist\fre\cli\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.pdb ..\dist\fre\cli\x64\
xcopy /d/y ..\x64\Release\aimapi.dll ..\dist\fre\api\x64\
xcopy /d/y ..\x64\Release\aimapi.pdb ..\dist\fre\api\x64\
xcopy /d/y ..\x64\Release\aimapi.lib ..\dist\fre\api\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.cpl ..\dist\fre\api\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.pdb ..\dist\fre\api\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.lib ..\dist\fre\api\x64\

xcopy /d/y ..\arm\Release\aim_ll.exe ..\dist\fre\cli\arm\
xcopy /d/y ..\arm\Release\aim_ll.pdb ..\dist\fre\cli\arm\
xcopy /d/y ..\arm\Release\aimapi.dll ..\dist\fre\cli\arm\
xcopy /d/y ..\arm\Release\aimapi.pdb ..\dist\fre\cli\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.cpl ..\dist\fre\cli\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.pdb ..\dist\fre\cli\arm\
xcopy /d/y ..\arm\Release\aimapi.dll ..\dist\fre\api\arm\
xcopy /d/y ..\arm\Release\aimapi.pdb ..\dist\fre\api\arm\
xcopy /d/y ..\arm\Release\aimapi.lib ..\dist\fre\api\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.cpl ..\dist\fre\api\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.pdb ..\dist\fre\api\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.lib ..\dist\fre\api\arm\

xcopy /d/y ..\arm64\Release\aim_ll.exe ..\dist\fre\cli\arm64\
xcopy /d/y ..\arm64\Release\aim_ll.pdb ..\dist\fre\cli\arm64\
xcopy /d/y ..\arm64\Release\aimapi.dll ..\dist\fre\cli\arm64\
xcopy /d/y ..\arm64\Release\aimapi.pdb ..\dist\fre\cli\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.cpl ..\dist\fre\cli\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.pdb ..\dist\fre\cli\arm64\
xcopy /d/y ..\arm64\Release\aimapi.dll ..\dist\fre\api\arm64\
xcopy /d/y ..\arm64\Release\aimapi.pdb ..\dist\fre\api\arm64\
xcopy /d/y ..\arm64\Release\aimapi.lib ..\dist\fre\api\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.cpl ..\dist\fre\api\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.pdb ..\dist\fre\api\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.lib ..\dist\fre\api\arm64\

xcopy /d/y ..\Debug\aim_ll.exe ..\dist\chk\cli\x86\
xcopy /d/y ..\Debug\aim_ll.pdb ..\dist\chk\cli\x86\
xcopy /d/y ..\Debug\aimapi.dll ..\dist\chk\cli\x86\
xcopy /d/y ..\Debug\aimapi.pdb ..\dist\chk\cli\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.cpl ..\dist\chk\cli\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.pdb ..\dist\chk\cli\x86\
xcopy /d/y ..\Debug\aimapi.dll ..\dist\chk\api\x86\
xcopy /d/y ..\Debug\aimapi.pdb ..\dist\chk\api\x86\
xcopy /d/y ..\Debug\aimapi.lib ..\dist\chk\api\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.cpl ..\dist\chk\api\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.pdb ..\dist\chk\api\x86\
xcopy /d/y ..\..\..\imdisk\cpl\x86\imdisk.lib ..\dist\chk\api\x86\

xcopy /d/y ..\x64\Debug\aim_ll.exe ..\dist\chk\cli\x64\
xcopy /d/y ..\x64\Debug\aim_ll.pdb ..\dist\chk\cli\x64\
xcopy /d/y ..\x64\Debug\aimapi.dll ..\dist\chk\cli\x64\
xcopy /d/y ..\x64\Debug\aimapi.pdb ..\dist\chk\cli\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.cpl ..\dist\chk\cli\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.pdb ..\dist\chk\cli\x64\
xcopy /d/y ..\x64\Debug\aimapi.dll ..\dist\chk\api\x64\
xcopy /d/y ..\x64\Debug\aimapi.pdb ..\dist\chk\api\x64\
xcopy /d/y ..\x64\Debug\aimapi.lib ..\dist\chk\api\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.cpl ..\dist\chk\api\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.pdb ..\dist\chk\api\x64\
xcopy /d/y ..\..\..\imdisk\cpl\x64\imdisk.lib ..\dist\chk\api\x64\

xcopy /d/y ..\arm\Debug\aim_ll.exe ..\dist\chk\cli\arm\
xcopy /d/y ..\arm\Debug\aim_ll.pdb ..\dist\chk\cli\arm\
xcopy /d/y ..\arm\Debug\aimapi.dll ..\dist\chk\cli\arm\
xcopy /d/y ..\arm\Debug\aimapi.pdb ..\dist\chk\cli\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.cpl ..\dist\chk\cli\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.pdb ..\dist\chk\cli\arm\
xcopy /d/y ..\arm\Debug\aimapi.dll ..\dist\chk\api\arm\
xcopy /d/y ..\arm\Debug\aimapi.pdb ..\dist\chk\api\arm\
xcopy /d/y ..\arm\Debug\aimapi.lib ..\dist\chk\api\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.cpl ..\dist\chk\api\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.pdb ..\dist\chk\api\arm\
xcopy /d/y ..\..\..\imdisk\cpl\arm\imdisk.lib ..\dist\chk\api\arm\

xcopy /d/y ..\arm64\Debug\aim_ll.exe ..\dist\chk\cli\arm64\
xcopy /d/y ..\arm64\Debug\aim_ll.pdb ..\dist\chk\cli\arm64\
xcopy /d/y ..\arm64\Debug\aimapi.dll ..\dist\chk\cli\arm64\
xcopy /d/y ..\arm64\Debug\aimapi.pdb ..\dist\chk\cli\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.cpl ..\dist\chk\cli\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.pdb ..\dist\chk\cli\arm64\
xcopy /d/y ..\arm64\Debug\aimapi.dll ..\dist\chk\api\arm64\
xcopy /d/y ..\arm64\Debug\aimapi.pdb ..\dist\chk\api\arm64\
xcopy /d/y ..\arm64\Debug\aimapi.lib ..\dist\chk\api\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.cpl ..\dist\chk\api\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.pdb ..\dist\chk\api\arm64\
xcopy /d/y ..\..\..\imdisk\cpl\arm64\imdisk.lib ..\dist\chk\api\arm64\

xcopy /d/y ..\aimapi\aimapi.h ..\dist\fre\api\
xcopy /d/y ..\..\..\imdisk\inc\imdisk.h ..\dist\fre\api\

xcopy /d/y ..\aimapi\aimapi.h ..\dist\chk\api\
xcopy /d/y ..\..\..\imdisk\inc\imdisk.h ..\dist\chk\api\
