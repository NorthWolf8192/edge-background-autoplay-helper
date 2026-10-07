[CmdletBinding()]
param([string]$Version = '0.2.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a version such as 0.2.0.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageDist = Join-Path $projectRoot 'dist'
$packageExe = Join-Path $packageDist 'EdgeBackgroundHelper.exe'
if (-not (Test-Path -LiteralPath $packageExe)) { throw 'Build the project before packaging.' }
$packageName = 'edge-background-autoplay-helper-v' + $Version + '-windows'
$packageFolder = Join-Path $packageDist $packageName
if (Test-Path -LiteralPath $packageFolder) { throw 'Package folder already exists; choose a new version or archive the existing package first.' }
New-Item -ItemType Directory -Path $packageFolder | Out-Null
Copy-Item -LiteralPath $packageExe -Destination (Join-Path $packageFolder 'EdgeBackgroundHelper.exe')
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\Helper.cs') -Destination (Join-Path $packageFolder 'Helper.cs')
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $packageFolder 'README.md')
foreach ($packageFile in @('launch.ps1', 'Start.vbs', 'Start.cmd')) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $projectRoot 'packaging') $packageFile) -Destination (Join-Path $packageFolder $packageFile)
}
$packageZip = Join-Path $packageDist ($packageName + '.zip')
Compress-Archive -LiteralPath $packageFolder -DestinationPath $packageZip -CompressionLevel Optimal
$packageHash = (Get-FileHash -LiteralPath $packageZip -Algorithm SHA256).Hash.ToLowerInvariant()
$packageChecksum = $packageZip + '.sha256'
[IO.File]::WriteAllText($packageChecksum, $packageHash + '  ' + [IO.Path]::GetFileName($packageZip) + [Environment]::NewLine, [Text.Encoding]::ASCII)
Write-Output ('Package: ' + $packageZip)
Write-Output ('SHA256: ' + $packageHash)

