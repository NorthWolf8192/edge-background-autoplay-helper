$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -AssemblyName WindowsBase
    $helperReferences = @(
        [System.Windows.Forms.Form].Assembly.Location
        [System.Drawing.Point].Assembly.Location
        [System.Windows.Automation.AutomationElement].Assembly.Location
        [System.Windows.Automation.ControlType].Assembly.Location
        [System.Windows.Point].Assembly.Location
        [System.Xml.XmlDocument].Assembly.Location
        [System.Uri].Assembly.Location
        [System.Collections.Generic.HashSet[string]].Assembly.Location
    ) | Select-Object -Unique
    $helperSource = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Helper.cs'), [System.Text.Encoding]::UTF8)
    Add-Type -TypeDefinition $helperSource -ReferencedAssemblies $helperReferences
    [EdgeBackgroundHelper.Program]::Run($PSScriptRoot)
}
catch {
    $helperError = $_ | Out-String
    $helperError | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'startup-error.txt') -Encoding UTF8
    try {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show(
            'Startup failed. See startup-error.txt in this folder.' + [Environment]::NewLine + $helperError,
            'Edge background helper'
        ) | Out-Null
    } catch { }
}

