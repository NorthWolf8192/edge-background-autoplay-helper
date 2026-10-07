[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testExe = Join-Path $projectRoot 'dist\EdgeBackgroundHelper.exe'
if (-not (Test-Path -LiteralPath $testExe)) { throw 'Build the project before running tests.' }
$testAssembly = [Reflection.Assembly]::LoadFrom($testExe)
$testType = $testAssembly.GetType('EdgeBackgroundHelper.HelperContext')
$testTabMethod = $testType.GetMethod('IsNewTabTitle', [Reflection.BindingFlags]'NonPublic, Static')
$testWindowMethod = $testType.GetMethod('IsNewTabWindowTitle', [Reflection.BindingFlags]'NonPublic, Static')
$testChineseTitle = [Text.RegularExpressions.Regex]::Unescape('\u65b0\u5efa\u6807\u7b7e\u9875')
$testTabCases = @(
    @{Title='New tab'; Expected=$true}
    @{Title='New tab - Microsoft Edge'; Expected=$true}
    @{Title=$testChineseTitle; Expected=$true}
    @{Title=($testChineseTitle + ' - Microsoft Edge'); Expected=$true}
    @{Title='Inbox - Microsoft Edge'; Expected=$false}
    @{Title='New tab article - Microsoft Edge'; Expected=$false}
    @{Title='Shopping'; Expected=$false}
)
foreach ($testCase in $testTabCases) {
    $testActual = $testTabMethod.Invoke($null, @($testCase.Title))
    if ($testActual -ne $testCase.Expected) { throw ('Tab title check failed: ' + $testCase.Title) }
}
$testAggregateTitle = $testChineseTitle + [Text.RegularExpressions.Regex]::Unescape(' \u548c\u53e6\u5916 10 \u4e2a\u9875\u9762 - \u4e2a\u4eba - Microsoft\u200b Edge')
$testWindowCases = @(
    @{Title=$testAggregateTitle; Expected=$true}
    @{Title=([Text.RegularExpressions.Regex]::Unescape('\u2068') + $testAggregateTitle + [Text.RegularExpressions.Regex]::Unescape('\u2069')); Expected=$true}
    @{Title='New tab and 3 other tabs - Personal - Microsoft Edge'; Expected=$true}
    @{Title='New tab and 1 other tab - Microsoft Edge'; Expected=$true}
    @{Title='New tab article - Microsoft Edge'; Expected=$false}
    @{Title='Inbox and 3 other tabs - Microsoft Edge'; Expected=$false}
    @{Title=($testChineseTitle + ' unexpected webpage title'); Expected=$false}
)
foreach ($testCase in $testWindowCases) {
    $testActual = $testWindowMethod.Invoke($null, @($testCase.Title))
    if ($testActual -ne $testCase.Expected) { throw ('Window title check failed: ' + $testCase.Title) }
}
if ($testTabMethod.Invoke($null, @($testAggregateTitle))) { throw 'A tab label must not accept the aggregate window title.' }
Write-Output 'PASS: 15 title rules. The helper was not started.'

