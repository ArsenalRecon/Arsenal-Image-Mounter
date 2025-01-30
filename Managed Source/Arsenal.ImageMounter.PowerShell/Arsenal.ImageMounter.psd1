#
# PowerShell module manifest for module 'Arsenal.ImageMounter'
#
#
#  
#  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https//#www.ArsenalRecon.com>
#  This source code and API are available under the terms of the Affero General Public
#  License v3.
# 
#  Please see LICENSE.txt for full license terms, including the availability of
#  proprietary exceptions.
#  Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
# 
#

@{

# Script module or binary module file associated with this manifest
RootModule = 'Arsenal.ImageMounter.PowerShell.dll'

# Version number of this module.
ModuleVersion = '3.11.298.0'

# ID used to uniquely identify this module
GUID = '60377397-9935-41b1-8e33-16c3e302e884'

# Author of this module
Author = 'Arsenal Recon'

# Company or vendor of this module
CompanyName = 'https://ArsenalRecon.com'

# Copyright statement for this module
Copyright = 'Copyright © Arsenal Recon 2024-2025'

# Description of the functionality provided by this module
Description = 'PowerShell module for Arsenal ImageMounter'

# Minimum version of the Windows PowerShell engine required by this module
PowerShellVersion = '2.0'

# Name of the Windows PowerShell host required by this module
PowerShellHostName = ''

# Minimum version of the Windows PowerShell host required by this module
PowerShellHostVersion = ''

# Minimum version of the .NET Framework required by this module
DotNetFrameworkVersion = ''

# Minimum version of the common language runtime (CLR) required by this module
CLRVersion = ''

# Processor architecture (None, X86, Amd64, IA64) required by this module
ProcessorArchitecture = 'None'

# Modules that must be imported into the global environment prior to importing this module
RequiredModules = @()

# Assemblies that must be loaded prior to importing this module
RequiredAssemblies = @('Arsenal.ImageMounter.PowerShell.dll')

# Script files (.ps1) that are run in the caller's environment prior to importing this module
ScriptsToProcess = @()

# Type files (.ps1xml) to be loaded when importing this module
TypesToProcess = @()
#TypesToProcess = @('Arsenal.ImageMounter.Types.ps1xml')

# Format files (.ps1xml) to be loaded when importing this module
FormatsToProcess = @()
#FormatsToProcess = @('Arsenal.ImageMounter.Format.ps1xml')

# Modules to import as nested modules of the module specified in ModuleToProcess
NestedModules = @()

# Functions to export from this module
FunctionsToExport = '*'

# Cmdlets to export from this module
CmdletsToExport = @('New-AimVirtualDisk', 'Remove-AimVirtualDisk', 'Get-AimDiskDevice', 'Get-AimDiskVolumes', 'Get-AimVolumeMountPoints')

# Variables to export from this module
VariablesToExport = '*'

# Aliases to export from this module
AliasesToExport = '*'

# List of all files packaged with this module
FileList = @('Arsenal.ImageMounter.psd1',
	'aim_cli.deps.json',
	'aim_cli.dll',
	'Arsenal.ImageMounter.dll',
	'Arsenal.ImageMounter.PowerShell.deps.json',
	'Arsenal.ImageMounter.PowerShell.dll',
	'Arsenal.ImageMounter.xml',
	'DiscUtils.Core.dll',
	'DiscUtils.Dmg.dll',
	'DiscUtils.Fat.dll',
	'DiscUtils.Iso9660.dll',
	'DiscUtils.Ntfs.dll',
	'DiscUtils.OpticalDisk.dll',
	'DiscUtils.Streams.dll',
	'DiscUtils.Udf.dll',
	'DiscUtils.Vdi.dll',
	'DiscUtils.Vhd.dll',
	'DiscUtils.Vhdx.dll',
	'DiscUtils.Vmdk.dll',
	'DiscUtils.Xva.dll',
	'LTRData.Extensions.dll',
	'LTRData.Extensions.Native.dll',
	'lzfse-net.dll',
	'Microsoft.ApplicationInsights.dll',
	'Microsoft.Bcl.AsyncInterfaces.dll',
	'Microsoft.Bcl.HashCode.dll',
	'Microsoft.Win32.Registry.AccessControl.dll',
	'Newtonsoft.Json.dll',
	'System.CodeDom.dll',
	'System.Configuration.ConfigurationManager.dll',
	'System.Diagnostics.EventLog.dll',
	'System.DirectoryServices.dll',
	'System.Management.dll',
	'System.Security.Cryptography.Pkcs.dll',
	'System.Security.Cryptography.ProtectedData.dll',
	'System.Security.Permissions.dll',
	'System.Windows.Extensions.dll')

# Private data to pass to the module specified in ModuleToProcess
PrivateData = ''

}
