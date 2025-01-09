How to build everything for distribution
----------------------------------------

0. Update version numbers in phdskmntver.h and batch build all in VS solution, or run msbld in solution dir

1. In aimwrfltr, awealloc, deviodrv, deviosvc and phdskmnt, run bldchk and bldfre in "neutral" command line window, to build and sign driver for each architecture

2. Run pubcmd.cmd to copy cli and api files to dist directories.

3. Run catchk or catfre in "DDK" or "neutral" command line window, to build and sign cat for each architecture

4. In ..\dist\fre, run mkcab.cmd to build and sign ..\dist\fre\cab\phdskmnt.cab file. Upload it to Microsoft for signing.

5. Download results file and extract Win8.1 subfolder over existing Win10 folder in ..\dist\fre, and api and cli folders
   over existing folders respectivley.

6. In ..\dist\chk or ..\dist\fre, run upd7z to freshen files in archives

7. Run syncaimsource to freshen GitHub directory

8. Build .NET solution to freshen integrated setup packages

9. Check in to GitHub!
