
for %%a in (net48 net8.0 net9.0) do (
  dotnet publish -c Release -f %%a -o publish\PowerShell\%%a Arsenal.ImageMounter.PowerShell
  robocopy /MIR ..\lib publish\PowerShell\%%a\lib
)

pushd publish

7z a -r AIM_PowerShell.7z PowerShell

popd
