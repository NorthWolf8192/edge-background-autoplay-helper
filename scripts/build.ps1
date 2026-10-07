[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or $PSVersionTable.PSVersion.Major -ne 5) {
    throw 'Use Windows PowerShell 5.1: powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1'
}
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildFolder = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $buildFolder -Force | Out-Null
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName WindowsBase
$buildReferences = @(
    [System.Windows.Forms.Form].Assembly.Location
    [System.Drawing.Point].Assembly.Location
    [System.Windows.Automation.AutomationElement].Assembly.Location
    [System.Windows.Automation.ControlType].Assembly.Location
    [System.Windows.Point].Assembly.Location
    [System.Xml.XmlDocument].Assembly.Location
    [System.Uri].Assembly.Location
    [System.Collections.Generic.HashSet[string]].Assembly.Location
) | Select-Object -Unique
$buildSource = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\Helper.cs'), [Text.Encoding]::UTF8)
$buildTemporaryExe = Join-Path $buildFolder ('.build-' + [Guid]::NewGuid().ToString('N') + '.exe')
$buildFinalExe = Join-Path $buildFolder 'EdgeBackgroundHelper.exe'
try {
    Add-Type -TypeDefinition $buildSource -ReferencedAssemblies $buildReferences -OutputAssembly $buildTemporaryExe -OutputType WindowsApplication
    Copy-Item -LiteralPath $buildTemporaryExe -Destination $buildFinalExe -Force
}
finally {
    if (Test-Path -LiteralPath $buildTemporaryExe) { Remove-Item -LiteralPath $buildTemporaryExe }
}
Write-Output ('Built: ' + $buildFinalExe)

