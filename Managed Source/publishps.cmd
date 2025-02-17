dotnet restore

rd /s /q publish\PowerShell

for %%a in (net48 net8.0 net9.0) do (
  dotnet publish --no-restore -c Release -f %%a -o publish\PowerShell\%%a Arsenal.ImageMounter.PowerShell
  robocopy /MIR ..\lib publish\PowerShell\%%a\lib
)

pushd publish

del /s *.pdb *.xml

7z a -r AIM_PowerShell.7z PowerShell

popd
