[CmdletBinding()]
param(
    [string] $Project = "tests/Cycles.Tests/Cycles.Tests.csproj",
    [string] $Configuration = "Debug",
    [string] $BaseOutputPath = (Join-Path ([System.IO.Path]::GetTempPath()) "cycles-test-bin"),
    [string] $Filter,
    [switch] $RequireSqlIntegration,
    [string] $SqlResultsPath,
    [switch] $Restore,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArgs
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot $Project
$resolvedOutputPath = [System.IO.Path]::GetFullPath($BaseOutputPath)
$sqlConnectionEnvironmentVariable = "CYCLES_SQL_INTEGRATION_CONNECTION_STRING"
$sqlRequiredEnvironmentVariable = "CYCLES_REQUIRE_SQL_INTEGRATION"
$sqlCategoryFilter = "Category=SqlIntegration"
$hasSqlConnection = -not [string]::IsNullOrWhiteSpace($env:CYCLES_SQL_INTEGRATION_CONNECTION_STRING)

if ($RequireSqlIntegration -and -not $hasSqlConnection) {
    throw "SQL Server integration is required, but $sqlConnectionEnvironmentVariable is not configured."
}

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

$effectiveFilter = $Filter
if ($RequireSqlIntegration) {
    $effectiveFilter = if ([string]::IsNullOrWhiteSpace($effectiveFilter)) {
        $sqlCategoryFilter
    }
    else {
        "($effectiveFilter)&($sqlCategoryFilter)"
    }
}
elseif (-not $hasSqlConnection) {
    $sqlExclusionFilter = "Category!=SqlIntegration"
    $effectiveFilter = if ([string]::IsNullOrWhiteSpace($effectiveFilter)) {
        $sqlExclusionFilter
    }
    else {
        "($effectiveFilter)&($sqlExclusionFilter)"
    }

    Write-Host "SQL Server integration tests excluded: $sqlConnectionEnvironmentVariable is not configured."
}

if (-not [string]::IsNullOrWhiteSpace($effectiveFilter)) {
    $arguments += @("--filter", $effectiveFilter)
}

if ($RequireSqlIntegration) {
    if ([string]::IsNullOrWhiteSpace($SqlResultsPath)) {
        $SqlResultsPath = Join-Path $resolvedOutputPath "TestResults/sql-integration.trx"
    }

    $SqlResultsPath = [System.IO.Path]::GetFullPath($SqlResultsPath)
    $sqlResultsDirectory = Split-Path -Parent $SqlResultsPath
    $sqlResultsFileName = Split-Path -Leaf $SqlResultsPath
    New-Item -ItemType Directory -Force -Path $sqlResultsDirectory | Out-Null
    if (Test-Path -LiteralPath $SqlResultsPath) {
        Remove-Item -LiteralPath $SqlResultsPath
    }

    $arguments += @(
        "--logger",
        "trx;LogFileName=$sqlResultsFileName",
        "--results-directory",
        $sqlResultsDirectory
    )
}

if ($DotNetArgs) {
    $arguments += $DotNetArgs
}

$originalSqlRequired = [Environment]::GetEnvironmentVariable($sqlRequiredEnvironmentVariable)
$testExitCode = 1
try {
    if ($RequireSqlIntegration) {
        [Environment]::SetEnvironmentVariable($sqlRequiredEnvironmentVariable, "1")
    }

    Write-Host "dotnet $($arguments -join ' ')"
    & dotnet @arguments
    $testExitCode = $LASTEXITCODE

    if ($RequireSqlIntegration) {
        if (-not (Test-Path -LiteralPath $SqlResultsPath)) {
            throw "The required SQL integration run did not produce its result file at '$SqlResultsPath'."
        }

        $counterNode = (Select-Xml -Path $SqlResultsPath -XPath "//*[local-name()='Counters']" | Select-Object -First 1).Node
        if ($null -eq $counterNode) {
            throw "The SQL integration result file does not contain test counters."
        }

        $executed = [int]$counterNode.executed
        $passed = [int]$counterNode.passed
        $failed = [int]$counterNode.failed
        Write-Host "SQL integration evidence: executed=$executed passed=$passed failed=$failed result='$SqlResultsPath'"
        if ($executed -eq 0) {
            throw "The required SQL integration run executed zero tests."
        }
    }
}
finally {
    if ($null -eq $originalSqlRequired) {
        Remove-Item "Env:$sqlRequiredEnvironmentVariable" -ErrorAction SilentlyContinue
    }
    else {
        [Environment]::SetEnvironmentVariable($sqlRequiredEnvironmentVariable, $originalSqlRequired)
    }
}

exit $testExitCode
