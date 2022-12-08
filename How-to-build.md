How to build unmanaged source, including drivers and native libraries
---------------------------------------------------------------------

* It is possible to build driver, native API DLL and native command line
  tools with Windows Driver Kit 7.0.
  https://msdn.microsoft.com/en-us/library/windows/hardware/ff557573.aspx
  If you don't need to build the driver project, you can simply build each
  of the directories with user mode components instead.

* There is also a Visual Studio solution where you can build these components
  in Visual Studio 2017-2022 with WDK extensions and toolchains installed.

How to build managed source, including graphical and command line tools for
mounting images, installing driver, converting between image formats etc
---------------------------------------------------------------------------

* It is possible to build all of this with .NET 6.0 or 7.0 SDK.
  https://dotnet.microsoft.com/en-us/download/dotnet/


* It is also possible to build all of this with Visual Studio 2022.

