How to build everything for distribution
----------------------------------------

1. Run bldchk or bldfre in "neutral" command line window, to build and sign driver for each architecture

2. Run catchk or catfre in "DDK" or "neutral" command line window, to build and sign cat for each architecture
   This will also freshen setup applications from GitHub directory (so make sure to build setup solution in VS2013 first!)

3. In ..\dist\chk or ..\dist\fre, run upd7z to freshen files in archives and in case of "fre" copy to GitHub directory.


How to build and sign for current architecture and OS version
-------------------------------------------------------------

1. From a "DDK" command line window, run buildandsign. Or, to also copy to correct ..\dist directory, run copydist
   instead of buildandsign. This builds, signs and copies to ..\dist\chk or ..\dist\fre.

