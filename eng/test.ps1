[CmdletBinding()]
param(
    [string] $Project = "tests/Cycles.Tests/Cycles.Tests.csproj",
    [string] $Configuration = "Debug",
    [string] $BaseOutputPath = (Join-Path ([System.IO.Path]::GetTempPath()) "cycles-test-bin"),
    [string] $Filter,
    [switch] $Restore,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArgs
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot $Project
$resolvedOutputPath = [System.IO.Path]::GetFullPath($BaseOutputPath)
if (-not $resolvedOutputPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
    $resolvedOutputPath += [System.IO.Path]::DirectorySeparatorChar
}

$arguments = @(
    "test",
    $projectPath,
    "--configuration",
    $Configuration,
    "-p:BaseOutputPath=$resolvedOutputPath"
)

if (-not $Restore) {
    $arguments += "--no-restore"
}

if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $arguments += @("--filter", $Filter)
}

if ($DotNetArgs) {
    $arguments += $DotNetArgs
}

Write-Host "dotnet $($arguments -join ' ')"
& dotnet @arguments
exit $LASTEXITCODE
